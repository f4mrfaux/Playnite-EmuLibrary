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
        RomTypeScanner IEmuLibrary.GetScanner(RomType romType) 
        {
            if (_scanners.TryGetValue(romType, out var scanner))
            {
                return scanner;
            }
            Logger.Error($"Scanner for RomType {romType} not found. This may indicate a missing RomTypeInfo attribute or initialization failure.");
            return null;
        }
        
        public new string GetPluginUserDataPath()
        {
            return PlayniteApi.Paths.ExtensionsDataPath;
        }

        private const string s_pluginName = "EmuLibrary";

        internal static readonly string Icon = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png");
        internal static readonly Guid PluginId = Guid.Parse("41e49490-0583-4148-94d2-940c7c74f1d9");
        internal static readonly MetadataNameProperty SourceName = new MetadataNameProperty(s_pluginName);

        // Dictionary to store initialized scanners
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

                try 
                {
                    var constructor = romInfo.ScannerType.GetConstructor(new Type[] { typeof(IEmuLibrary) });
                    if (constructor == null)
                    {
                        Logger.Error($"Failed to find constructor for RomType {rt} (using {romInfo.ScannerType}). Expected constructor with IEmuLibrary parameter.");
                        continue;
                    }

                    var scanner = constructor.Invoke(new object[] { this });
                    if (scanner == null)
                    {
                        Logger.Error($"Failed to instantiate scanner for RomType {rt} (using {romInfo.ScannerType}).");
                        continue;
                    }
                    
                    var typedScanner = scanner as RomTypeScanner;
                    if (typedScanner != null) 
                    {
                        _scanners.Add(rt, typedScanner);
                        Logger.Info($"Successfully registered scanner for RomType {rt}: {romInfo.ScannerType.Name}");
                    }
                    else
                    {
                        Logger.Error($"Scanner for RomType {rt} could not be cast to RomTypeScanner");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Exception while instantiating scanner for RomType {rt} (using {romInfo.ScannerType}): {ex.Message}");
                    
                    if (ex.InnerException != null)
                    {
                        Logger.Error(ex.InnerException, $"Inner exception: {ex.InnerException.Message}");
                    }
                    
                    continue;
                }

                _scanners.Add(rt, scanner as RomTypeScanner);
            }
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            base.OnApplicationStarted(args);

            // Initialize the SteamGridDB service if enabled
            if (Settings.EnableSteamGridDbMatching && !string.IsNullOrEmpty(Settings.SteamGridDbApiKey))
            {
                try
                {
                    var steamGridService = new Util.SteamGridDbService(Logger, Settings.SteamGridDbApiKey);
                    Logger.Info("SteamGridDB service initialized successfully.");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to initialize SteamGridDB service: {ex.Message}");
                }
            }

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

                // PCInstaller, ISOInstaller, and ArchiveInstaller don't require an emulator
                if (mapping.Emulator == null && 
                    mapping.RomType != RomType.PCInstaller && 
                    mapping.RomType != RomType.ISOInstaller &&
                    mapping.RomType != RomType.ArchiveInstaller)
                {
                    Logger.Warn($"Emulator {mapping.EmulatorId} not found, skipping.");
                    continue;
                }

                // PCInstaller, ISOInstaller, and ArchiveInstaller don't require an emulator profile
                if (mapping.EmulatorProfile == null && 
                    mapping.RomType != RomType.PCInstaller && 
                    mapping.RomType != RomType.ISOInstaller &&
                    mapping.RomType != RomType.ArchiveInstaller)
                {
                    Logger.Warn($"Emulator profile {mapping.EmulatorProfileId} for emulator {mapping.EmulatorId} not found, skipping.");
                    continue;
                }
                
                // Skip this check for PCInstaller, ISOInstaller, and ArchiveInstaller - they can work without platform
                if (mapping.Platform == null && 
                    mapping.RomType != RomType.PCInstaller && 
                    mapping.RomType != RomType.ISOInstaller &&
                    mapping.RomType != RomType.ArchiveInstaller)
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

        public override IEnumerable<Playnite.SDK.Plugins.UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
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
                MenuSection = "EmuLibrary"
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
                    MenuSection = "EmuLibrary"
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
                        MenuSection = "EmuLibrary"
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
                        MenuSection = "EmuLibrary"
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
                    MenuSection = "EmuLibrary"
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