using EmuLibrary.RomTypes;
using EmuLibrary.RomTypes.ISOInstaller;
using EmuLibrary.Settings;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Controls;

namespace EmuLibrary
{
    public class EmuLibrary : LibraryPlugin, IEmuLibrary
    {
        // LibraryPlugin fields
        public override Guid Id { get; } = PluginId;
        public override string Name => s_pluginName;
        public override string LibraryIcon => Icon;

        // IEmuLibrary fields
        public ILogger Logger => LogManager.GetLogger();
        public IPlayniteAPI Playnite { get; private set; }
        public Settings.Settings Settings { get; private set; }
        RomTypeScanner IEmuLibrary.GetScanner(RomType romType) => _scanners[romType];
        
        public new string GetPluginUserDataPath()
        {
            return PlayniteApi.Paths.ExtensionsDataPath;
        }

        private const string s_pluginName = "ISOlator";

        internal static readonly string Icon = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png");
        internal static readonly Guid PluginId = Guid.Parse("f0a33e7a-1f30-4761-b3ab-0fc73d54a7c3");
        internal static readonly MetadataNameProperty SourceName = new MetadataNameProperty(s_pluginName);

        private readonly Dictionary<RomType, RomTypeScanner> _scanners = new Dictionary<RomType, RomTypeScanner>();

        public EmuLibrary(IPlayniteAPI api) : base(api)
        {
            Playnite = api;

            // This must occur before we instantiate the Settings class
            InitializeRomTypeScanners();

            Settings = new Settings.Settings(this, this);
        }

        private void InitializeRomTypeScanners()
        {
            var romTypes = Enum.GetValues(typeof(RomType)).Cast<RomType>();
            foreach (var rt in romTypes)
            {
                var fieldInfo = rt.GetType().GetField(rt.ToString());
                var romInfo = fieldInfo.GetCustomAttributes(false).OfType<RomTypeInfoAttribute>().FirstOrDefault();
                if (romInfo == null)
                {
                    Logger.Warn($"Failed to find {nameof(RomTypeInfoAttribute)} for RomType {rt}. Skipping...");
                    continue;
                }

                // Hook up ProtoInclude on ELGameInfo for each RomType
                // Starts at field number 10 to not conflict with ELGameInfo's fields
                RuntimeTypeModel.Default[typeof(ELGameInfo)].AddSubType((int)rt + 10, romInfo.GameInfoType);

                var scanner = romInfo.ScannerType.GetConstructor(new Type[] { typeof(IEmuLibrary) })?.Invoke(new object[] { this });
                if (scanner == null)
                {
                    Logger.Error($"Failed to instantiate scanner for RomType {rt} (using {romInfo.ScannerType}).");
                    continue;
                }

                _scanners.Add(rt, scanner as RomTypeScanner);
            }
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            base.OnApplicationStarted(args);

            Settings.Mappings.ForEach(mapping =>
            {
                _scanners.Values.ForEach(scanner =>
                {
                    var oldGameIdFormat = PlayniteApi.Database.Games.Where(game => game.PluginId == scanner.LegacyPluginId && !game.GameId.StartsWith("!"));
                    if (oldGameIdFormat.Any())
                    {
                        Logger.Info($"Updating {oldGameIdFormat.Count()} games to new game id format for mapping {mapping.MappingId} ({mapping.Emulator.Name}/{mapping.EmulatorProfile.Name}/{mapping.SourcePath}).");
                        using (Playnite.Database.BufferedUpdate())
                        {
                            oldGameIdFormat.ForEach(game =>
                            {
                                if (scanner.TryGetGameInfoBaseFromLegacyGameId(game, mapping, out var gameInfo))
                                {
                                    game.GameId = gameInfo.AsGameId();
                                    game.PluginId = PluginId;
                                    PlayniteApi.Database.Games.Update(game);
                                }
                            });
                        }
                    }
                });
            });
            
            // Migrate existing games to stable GameId format (exclude installation-state fields)
            // This prevents duplicates when games transition from uninstalled to installed
            MigrateToStableGameIds();
        }
        
        /// <summary>
        /// Migrates existing games to stable GameId format that excludes installation-state fields.
        /// This ensures games maintain the same ID regardless of installation status, preventing duplicates.
        /// Playnite will automatically match games by GameId + PluginId, so we just need to update the IDs.
        /// IMPORTANT: Preserves installation state (InstallDirectory, PrimaryExecutable, IsInstalled) during migration.
        /// </summary>
        private void MigrateToStableGameIds()
        {
            try
            {
                // Find all games that need migration to stable format:
                // - Games in ProtoBuf format (!0...) that may have installation fields in their GameId
                // - Games in legacy pipe-separated format (ISOInstaller|..., etc.) that need to be migrated to stable ProtoBuf format
                // We check if the GameId changes when regenerated with AsGameId() (which excludes installation fields)
                var allPluginGames = PlayniteApi.Database.Games
                    .Where(g => g.PluginId == PluginId && g.GameId != null)
                    .ToList();
                
                if (!allPluginGames.Any())
                {
                    return;
                }
                
                var migratedCount = 0;
                var preservedInstallStateCount = 0;
                var skippedCount = 0;
                
                using (Playnite.Database.BufferedUpdate())
                {
                    foreach (var game in allPluginGames)
                    {
                        try
                        {
                            ELGameInfo currentGameInfo = null;
                            
                            // Handle both ProtoBuf format and legacy pipe-separated format
                            if (game.GameId.StartsWith("!0"))
                            {
                                // ProtoBuf format - can deserialize directly
                                currentGameInfo = game.GetELGameInfo();
                            }
                            else
                            {
                                // Legacy format (pipe-separated like "ISOInstaller|guid|path|installDir")
                                // Try to parse using extension methods that support legacy format
                                try
                                {
                                    // Try ISOInstaller format first
                                    if (game.GameId.Contains("|") && game.GameId.StartsWith("ISOInstaller"))
                                    {
                                        var isoInfo = game.GetISOInstallerGameInfo();
                                        currentGameInfo = isoInfo;
                                    }
                                    // Could add other legacy format parsers here if needed
                                    // For now, ISOInstaller is the main one with legacy format support
                                }
                                catch
                                {
                                    // If we can't parse it, skip this game
                                    skippedCount++;
                                    Logger.Debug($"Skipping game '{game.Name}' with unrecognized GameId format: {game.GameId.Substring(0, Math.Min(50, game.GameId.Length))}...");
                                    continue;
                                }
                                
                                if (currentGameInfo == null)
                                {
                                    skippedCount++;
                                    continue;
                                }
                            }
                            
                            // Preserve installation state before migration
                            string oldInstallDirectory = null;
                            string oldPrimaryExecutable = null;
                            bool hadInstallState = false;
                            
                            // Extract installation state from old GameInfo based on RomType
                            if (currentGameInfo.RomType == RomType.PCInstaller)
                            {
                                var pcInfo = currentGameInfo as RomTypes.PCInstaller.PCInstallerGameInfo;
                                if (pcInfo != null)
                                {
                                    oldInstallDirectory = pcInfo.InstallDirectory;
                                    oldPrimaryExecutable = pcInfo.PrimaryExecutable;
                                    hadInstallState = !string.IsNullOrEmpty(oldInstallDirectory);
                                }
                            }
                            else if (currentGameInfo.RomType == RomType.ISOInstaller)
                            {
                                var isoInfo = currentGameInfo as RomTypes.ISOInstaller.ISOInstallerGameInfo;
                                if (isoInfo != null)
                                {
                                    oldInstallDirectory = isoInfo.InstallDirectory;
                                    oldPrimaryExecutable = isoInfo.PrimaryExecutable;
                                    hadInstallState = !string.IsNullOrEmpty(oldInstallDirectory);
                                }
                            }
                            
                            // Generate new stable GameId (excludes installation fields)
                            var newGameId = currentGameInfo.AsGameId();
                            
                            // Check if GameId changed (means it had installation fields included)
                            if (game.GameId != newGameId)
                            {
                                // Preserve installation state BEFORE updating GameId
                                // (since the new GameId won't include these fields)
                                if (hadInstallState)
                                {
                                    // Update game's installation state on the Game object
                                    // This is what Playnite uses and what scanners read
                                    game.IsInstalled = true;
                                    game.InstallDirectory = oldInstallDirectory;
                                    
                                    // Update play action if PrimaryExecutable exists
                                    if (!string.IsNullOrEmpty(oldPrimaryExecutable))
                                    {
                                        var playAction = game.GameActions?.FirstOrDefault(a => a.IsPlayAction);
                                        if (playAction != null)
                                        {
                                            playAction.Path = oldPrimaryExecutable;
                                            playAction.WorkingDir = oldInstallDirectory;
                                            playAction.Name = "Play";
                                            playAction.Type = Playnite.SDK.Models.GameActionType.File;
                                        }
                                        else
                                        {
                                            // Create new play action if none exists
                                            game.GameActions = game.GameActions ?? new System.Collections.ObjectModel.ObservableCollection<Playnite.SDK.Models.GameAction>();
                                            game.GameActions.Add(new Playnite.SDK.Models.GameAction
                                            {
                                                Path = oldPrimaryExecutable,
                                                WorkingDir = oldInstallDirectory,
                                                Name = "Play",
                                                Type = Playnite.SDK.Models.GameActionType.File,
                                                IsPlayAction = true
                                            });
                                        }
                                    }
                                    
                                    preservedInstallStateCount++;
                                    Logger.Debug($"Preserved installation state for game '{game.Name}': InstallDirectory={oldInstallDirectory}, PrimaryExecutable={oldPrimaryExecutable}");
                                }
                                
                                // Update to stable GameId format (excludes installation fields)
                                // The installation state is preserved on the Game object above
                                game.GameId = newGameId;
                                
                                PlayniteApi.Database.Games.Update(game);
                                migratedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"Could not migrate game '{game.Name}' to stable GameId format: {ex.Message}");
                        }
                    }
                }
                
                if (migratedCount > 0)
                {
                    Logger.Info($"GameId migration complete: {migratedCount} games migrated to stable GameId format (installation state excluded from ID). {preservedInstallStateCount} games had installation state preserved. {skippedCount} games skipped (not in ProtoBuf format, handled by legacy migration).");
                }
                else if (skippedCount > 0)
                {
                    Logger.Debug($"GameId migration: {skippedCount} games skipped (not in ProtoBuf format, will be handled by legacy migration in OnApplicationStarted).");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during GameId migration: {ex.Message}");
            }
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            if (Playnite.ApplicationInfo.Mode == ApplicationMode.Fullscreen && !Settings.ScanGamesInFullScreen)
            {
                yield break;
            }

            foreach (var mapping in Settings.Mappings?.Where(m => m.Enabled))
            {
                if (args.CancelToken.IsCancellationRequested)
                    yield break;

                if (mapping.Emulator == null)
                {
                    Logger.Warn($"Emulator {mapping.EmulatorId} not found, skipping.");
                    continue;
                }

                if (mapping.EmulatorProfile == null)
                {
                    Logger.Warn($"Emulator profile {mapping.EmulatorProfileId} for emulator {mapping.EmulatorId} not found, skipping.");
                    continue;
                }

                if (mapping.Platform == null)
                {
                    Logger.Warn($"Platform {mapping.PlatformId} not found, skipping.");
                    continue;
                }

                if (!_scanners.TryGetValue(mapping.RomType, out RomTypeScanner scanner))
                {
                    Logger.Warn($"Rom type {mapping.RomType} not supported, skipping.");
                    continue;
                }

                foreach (var g in scanner.GetGames(mapping, args))
                {
                    yield return g;
                }
            }

            if (Settings.AutoRemoveUninstalledGamesMissingFromSource)
            {
                RemoveSuperUninstalledGames(false, args.CancelToken);
            }
        }

        public override ISettings GetSettings(bool firstRunSettings) => Settings;
        public override UserControl GetSettingsView(bool firstRunSettings) => new SettingsView();

        public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            if (args.Game.PluginId == Id)
            {
                yield return args.Game.GetELGameInfo().GetInstallController(args.Game, this);
            }
        }

        public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            if (args.Game.PluginId == Id)
            {
                yield return args.Game.GetELGameInfo().GetUninstallController(args.Game, this);
            }
        }

        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            base.OnGameInstalled(args);

            if (args.Game.PluginId == PluginId && Settings.NotifyOnInstallComplete)
            {
                Playnite.Notifications.Add(args.Game.GameId, $"Installation of \"{args.Game.Name}\" has completed", NotificationType.Info);
            }
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem()
            {
                Action = (arags) => RemoveSuperUninstalledGames(true, default),
                Description = "Remove uninstalled games with missing source file...",
                MenuSection = "ISOlator"
            };
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var ourGameInfos = args.Games.Select(game =>
            {
                if (game.PluginId != Id)
                    return (null, null);

                ELGameInfo gameInfo;
                try
                {
                    gameInfo = game.GetELGameInfo();
                }
                catch
                {
                    return (null, null);
                }

                return (game, gameInfo);
            }).Where(ggi => ggi.game != null);

            if (ourGameInfos.Any())
            {
                yield return new GameMenuItem()
                {
                    Action = (arags) =>
                    {
                        ourGameInfos.ForEach(ggi => ggi.gameInfo.BrowseToSource());
                    },
                    Description = "Browse to Source...",
                    MenuSection = "ISOlator"
                };
                
                // Menu item for PC Installer games that are not installed
                var uninstalledPCInstallers = ourGameInfos
                    .Where(ggi => ggi.gameInfo.RomType == RomType.PCInstaller && !ggi.game.IsInstalled);
                
                if (uninstalledPCInstallers.Any())
                {
                    yield return new GameMenuItem()
                    {
                        Action = (arags) =>
                        {
                            uninstalledPCInstallers.ForEach(ggi => 
                            {
                                ggi.game.IsInstalling = true;
                                var controller = ggi.gameInfo.GetInstallController(ggi.game, this);
                                controller.Install(new InstallActionArgs());
                            });
                        },
                        Description = "Install Game",
                        MenuSection = "ISOlator"
                    };
                }
                
                // Menu item for ISO Installer games that are not installed
                var uninstalledISOInstallers = ourGameInfos
                    .Where(ggi => ggi.gameInfo.RomType == RomType.ISOInstaller && !ggi.game.IsInstalled);
                
                if (uninstalledISOInstallers.Any())
                {
                    yield return new GameMenuItem()
                    {
                        Action = (arags) =>
                        {
                            uninstalledISOInstallers.ForEach(ggi => 
                            {
                                ggi.game.IsInstalling = true;
                                var controller = ggi.gameInfo.GetInstallController(ggi.game, this);
                                controller.Install(new InstallActionArgs());
                            });
                        },
                        Description = "Install ISO Game",
                        MenuSection = "ISOlator"
                    };
                }
                
                yield return new GameMenuItem()
                {
                    Action = (arags) =>
                    {
                        var text = ourGameInfos.Select(ggi => ggi.gameInfo.ToDescriptiveString(ggi.game))
                            .Aggregate((a, b) => $"{a}\n--------------------------------------------------------------------\n{b}");
                        Playnite.Dialogs.ShowSelectableString("Decoded GameId info for each selected game is shown below. This information can be useful for troubleshooting.", "EmuLibrary Game Info", text);
                    },
                    Description = "Show Debug Info...",
                    MenuSection = "ISOlator"
                };
            }
        }

        private void RemoveSuperUninstalledGames(bool promptUser, CancellationToken ct)
        {
            var toRemove = _scanners.Values.SelectMany(s => s.GetUninstalledGamesMissingSourceFiles(ct));
            if (toRemove.Any())
            {
                System.Windows.MessageBoxResult res;
                if (promptUser)
                {
                    res = PlayniteApi.Dialogs.ShowMessage($"Delete {toRemove.Count()} library entries?\n\n(This may take a while, during while Playnite will seem frozen.)", "Confirm deletion", System.Windows.MessageBoxButton.YesNo);
                }
                else
                {
                    res = System.Windows.MessageBoxResult.Yes;
                }

                if (res == System.Windows.MessageBoxResult.Yes)
                {
                    PlayniteApi.Database.Games.Remove(toRemove);
                }
            }
            else if (promptUser)
            {
                PlayniteApi.Dialogs.ShowMessage("Nothing to do.");
            }
        }
    }
}