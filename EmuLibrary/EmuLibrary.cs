using EmuLibrary.RomTypes;
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
        public new string GetPluginUserDataPath() => Playnite.Paths.ConfigurationPath;

        private const string s_pluginName = "EmuLibrary PC Manager";

        internal static readonly string Icon = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png");
        internal static readonly Guid PluginId = Guid.Parse("87cab450-8935-4fa9-be88-e60d8a4ed9e1");
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
                    // If metadata download is enabled, set metadata to be automatically downloaded
                    if (Settings.EnableMetadataDownload)
                    {
                        // In newer Playnite SDK versions, IsMetadataRequestSourced may not exist
                        // We'll leave this as a comment for now
                        // g.IsMetadataRequestSourced = true;
                    }
                    
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
                Action = (menuArgs) => RemoveSuperUninstalledGames(true, default),
                Description = "Remove uninstalled games with missing source file...",
                MenuSection = "EmuLibrary PC Manager"
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
                    Action = (menuArgs) =>
                    {
                        ourGameInfos.ForEach(ggi => ggi.gameInfo.BrowseToSource());
                    },
                    Description = "Browse to Source...",
                    MenuSection = "EmuLibrary PC Manager"
                };
                yield return new GameMenuItem()
                {
                    Action = (menuArgs) =>
                    {
                        var text = ourGameInfos.Select(ggi => ggi.gameInfo.ToDescriptiveString(ggi.game))
                            .Aggregate((a, b) => $"{a}\n--------------------------------------------------------------------\n{b}");
                        Playnite.Dialogs.ShowSelectableString("Decoded GameId info for each selected game is shown below. This information can be useful for troubleshooting.", "EmuLibrary Game Info", text);
                    },
                    Description = "Show Debug Info...",
                    MenuSection = "EmuLibrary PC Manager"
                };
                
                // Only show the "Select Executable" option for installed PC installer games
                var pcInstallerGames = ourGameInfos.Where(ggi => ggi.game.IsInstalled && 
                                                         (ggi.gameInfo.RomType == RomType.PcInstaller || 
                                                          ggi.gameInfo.RomType == RomType.GogInstaller)).ToList();
                if (pcInstallerGames.Any())
                {
                    yield return new GameMenuItem()
                    {
                        Action = (execArgs) =>
                        {
                            foreach (var (game, gameInfo) in pcInstallerGames)
                            {
                                // Handle PC Installer games
                                if (gameInfo is RomTypes.PcInstaller.PcInstallerGameInfo pcInfo)
                                {
                                    string installDir = pcInfo.InstallDirectory;
                                    if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
                                    {
                                        Playnite.Notifications.Add(game.GameId, 
                                            $"Cannot select executable for {game.Name}: Installation directory not found.", 
                                            NotificationType.Error);
                                        continue;
                                    }
                                    
                                    // Show dialog to select executable
                                    var result = Playnite.Dialogs.SelectFile("Game Executable", "*.exe", installDir);
                                    if (!string.IsNullOrEmpty(result))
                                    {
                                        pcInfo.ExecutablePath = result;
                                        pcInfo.IsExecutablePathManuallySet = true;
                                        
                                        // Update game in Playnite database
                                        game.GameId = pcInfo.AsGameId();
                                        if (Playnite.MainView?.UIDispatcher != null)
                                        {
                                            Playnite.MainView.UIDispatcher.Invoke(() =>
                                            {
                                                Playnite.Database.Games.Update(game);
                                                
                                                // Update play action if needed
                                                var gameRom = new GameRom(game.Name, result);
                                                game.Roms = new System.Collections.ObjectModel.ObservableCollection<GameRom> { gameRom };
                                                Playnite.Database.Games.Update(game);
                                                
                                                Playnite.Notifications.Add(Guid.NewGuid().ToString(), 
                                                    $"Custom executable set for {game.Name}: {Path.GetFileName(result)}", 
                                                    NotificationType.Info);
                                            });
                                        }
                                    }
                                }
                                // Handle GOG Installer games if needed
                                else if (gameInfo is RomTypes.GogInstaller.GogInstallerGameInfo)
                                {
                                    // Implement similar functionality for GOG games if needed
                                }
                            }
                        },
                        Description = "Select Custom Executable...",
                        MenuSection = "EmuLibrary PC Manager"
                    };
                }
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