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

                // This line is redundant with the earlier _scanners.Add (around line 99)
                // _scanners.Add(rt, scanner as RomTypeScanner);
            }
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            base.OnApplicationStarted(args);

            // Check for ArchiveInstaller mappings and notify the user 
            var archiveInstallerMappings = Settings.Mappings?.Where(m => m.RomType == RomType.ArchiveInstaller).ToList();
            if (archiveInstallerMappings != null && archiveInstallerMappings.Any())
            {
                // Show a more prominent one-time notification about ArchiveInstaller removal
                var message = "ArchiveInstaller functionality has been removed from EmuLibrary. "
                    + $"Found {archiveInstallerMappings.Count} ArchiveInstaller mapping(s) which have been automatically disabled. "
                    + "Please extract your archives manually and use ISOInstaller with the extracted ISO files instead.";
                
                Logger.Warn(message);
                
                // Add user notification
                Playnite.Notifications.Add(
                    "EmuLibrary-ArchiveInstaller-Removed",
                    message,
                    NotificationType.Error);
            }

            // Initialize the SteamGridDB service if enabled
            if (Settings.EnableSteamGridDbMatching && !string.IsNullOrEmpty(Settings.SteamGridDbApiKey))
            {
                try
                {
                    // Create the service with the WebRequest-based implementation
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

            // Check for null mappings
            if (Settings.Mappings == null)
            {
                Logger.Error("No mappings found in settings. Please create a mapping for ISO installer in the settings.");
                yield break;
            }
            
            // Log all mappings for diagnostic purposes
            Logger.Info($"Found {Settings.Mappings.Count} total mappings in settings");
            foreach (var m in Settings.Mappings)
            {
                Logger.Info($"Mapping: ID={m.MappingId}, Type={m.RomType}, Enabled={m.Enabled}, Path={m.SourcePath}");
            }
            
            var enabledMappings = Settings.Mappings.Where(m => m.Enabled).ToList();
            Logger.Info($"Found {enabledMappings.Count} enabled mappings");
            
            // Look specifically for ISOInstaller mappings
            var isoMappings = enabledMappings.Where(m => m.RomType == RomType.ISOInstaller).ToList();
            Logger.Info($"Found {isoMappings.Count} enabled ISO installer mappings");
            
            // Check if ISO mappings exist and are properly configured
            if (isoMappings.Count == 0)
            {
                Logger.Warn("No enabled ISO installer mappings found. If you want to use ISO installers, please add an ISO mapping in settings.");
                if (Settings.ShowIsoMappingHelp)
                {
                    Playnite.Notifications.Add(
                        "EmuLibrary-ISOInstaller-NoMappings",
                        "No ISO installer mappings found. To add one, go to Settings > EmuLibrary > \"Add ISO Mapping\" button.",
                        NotificationType.Info);
                }
            }
            else
            {
                foreach (var mapping in isoMappings)
                {
                    Logger.Info($"ISO mapping found: ID={mapping.MappingId}, Path={mapping.SourcePath}, Platform={mapping.Platform?.Name ?? "<none>"}");
                    if (mapping.Platform == null)
                    {
                        Logger.Warn($"ISO mapping {mapping.MappingId} has no platform selected. It should be set to PC or another platform.");
                        Playnite.Notifications.Add(
                            $"EmuLibrary-ISOInstaller-NoPlatform-{mapping.MappingId}",
                            $"ISO Installer mapping for {mapping.SourcePath} has no platform selected. Please set it to PC in the settings.",
                            NotificationType.Error);
                    }
                }
            }
            
            foreach (var mapping in enabledMappings)
            {
                if (args.CancelToken.IsCancellationRequested)
                    yield break;

                // Skip ArchiveInstaller mappings - functionality has been removed
                if (mapping.RomType == RomType.ArchiveInstaller)
                {
                    Logger.Warn($"ArchiveInstaller functionality has been removed. Skipping mapping for {mapping.SourcePath}. Please extract your archive files manually and use ISOInstaller with the extracted ISO files instead.");
                    
                    // Add user notification to ensure they see this message
                    Playnite.Notifications.Add(
                        $"EmuLibrary-ArchiveInstaller-{mapping.MappingId}", 
                        $"ArchiveInstaller mapping for {mapping.SourcePath} was skipped because this functionality has been removed. Please extract your archives manually and use ISOInstaller with the extracted ISO files instead.", 
                        NotificationType.Error);
                    
                    continue;
                }
                
                // PCInstaller and ISOInstaller don't require an emulator
                if (mapping.Emulator == null && 
                    mapping.RomType != RomType.PCInstaller && 
                    mapping.RomType != RomType.ISOInstaller)
                {
                    Logger.Warn($"Emulator {mapping.EmulatorId} not found, skipping.");
                    continue;
                }

                // PCInstaller and ISOInstaller don't require an emulator profile
                if (mapping.EmulatorProfile == null && 
                    mapping.RomType != RomType.PCInstaller && 
                    mapping.RomType != RomType.ISOInstaller)
                {
                    Logger.Warn($"Emulator profile {mapping.EmulatorProfileId} for emulator {mapping.EmulatorId} not found, skipping.");
                    continue;
                }
                
                // Handle ISO and PC installer platform logic
                if (mapping.RomType == RomType.PCInstaller || mapping.RomType == RomType.ISOInstaller)
                {
                    // Always try to find PC platform for ISO/PC installers - this is critical
                    Logger.Info($"Ensuring PC platform for {mapping.RomType} mapping: {mapping.MappingId}");
                    
                    // Try to find PC platform
                    var pcPlatform = Playnite.Database.Platforms
                        .FirstOrDefault(p => p.Name == "PC" || p.Name == "Windows");
                        
                    if (pcPlatform != null)
                    {
                        // Always update to the latest platform ID
                        mapping.PlatformId = pcPlatform.SpecificationId ?? pcPlatform.Id.ToString();
                        // Don't set Platform property directly, PlatformId is used to resolve it
                        Logger.Info($"Set platform to {pcPlatform.Name} (ID: {mapping.PlatformId})");
                    }
                    else
                    {
                        Logger.Warn($"Could not find PC platform in database. This may prevent games from appearing correctly.");
                        
                        // Create a fallback platform ID if needed
                        if (string.IsNullOrEmpty(mapping.PlatformId))
                        {
                            mapping.PlatformId = "PC"; // Generic ID
                            Logger.Info("Set generic PC platform ID as fallback");
                        }
                    }
                }
                // For all other rom types, platform is required
                else if (mapping.Platform == null)
                {
                    Logger.Warn($"Platform {mapping.PlatformId} not found, skipping.");
                    continue;
                }


                if (!_scanners.TryGetValue(mapping.RomType, out RomTypeScanner scanner))
                {
                    Logger.Warn($"Rom type {mapping.RomType} not supported, skipping. Available scanner types: {string.Join(", ", _scanners.Keys)}");
                    continue;
                }

                Logger.Info($"Starting scan for mapping: ID={mapping.MappingId}, Type={mapping.RomType}, Path={mapping.SourcePath}");
                int gameCount = 0;
                
                foreach (var g in scanner.GetGames(mapping, args))
                {
                    gameCount++;
                    // Log more details about the game being returned to help with debugging
                    Logger.Info($"Found game {gameCount}: {g.Name} (ID: {g.GameId}, Installed: {g.IsInstalled})");
                    
                    // Ensure the game has the critical GameId property
                    if (string.IsNullOrEmpty(g.GameId))
                    {
                        Logger.Error($"Game {g.Name} has an empty GameId - this will prevent it from appearing in Playnite");
                    }
                    
                    // Check for valid platforms
                    if (g.Platforms == null || !g.Platforms.Any())
                    {
                        Logger.Warn($"Game {g.Name} has no platforms");
                    }
                    
                    // Log game details for ISO games
                    if (mapping.RomType == RomType.ISOInstaller)
                    {
                        Logger.Info($"ISO game details - Name: {g.Name}, Source: {g.Source}, Platform count: {g.Platforms?.Count ?? 0}");
                        
                        if (g.Platforms != null)
                        {
                            Logger.Info($"Platforms: {string.Join(", ", g.Platforms)}");
                        }
                        
                        if (g.Tags != null && g.Tags.Any())
                        {
                            Logger.Info($"Tags: {string.Join(", ", g.Tags)}");
                        }
                    }
                    
                    // Return the game to Playnite
                    yield return g;
                }
                
                Logger.Info($"Finished scan for mapping {mapping.MappingId}, found {gameCount} games");
                
                // Special check for ISO scanner with no results
                if (mapping.RomType == RomType.ISOInstaller && gameCount == 0)
                {
                    Logger.Error($"ISO Scanner found 0 games for mapping {mapping.MappingId}. This might indicate a configuration problem.");
                    Playnite.Notifications.Add(
                        $"EmuLibrary-ISOInstaller-NoGames-{mapping.MappingId}", 
                        $"ISO Scanner found 0 games for {mapping.SourcePath}. Please check your ISO Installer settings and ensure your ISO files are in the correct location.", 
                        NotificationType.Error);
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
            
            // Add a debug menu item for directly running the ISO Scanner
            yield return new MainMenuItem()
            {
                Action = (arags) => RunDirectISOScan(),
                Description = "Debug: Run ISO Scanner directly...",
                MenuSection = "EmuLibrary"
            };
            
            // Add a test menu item for ISO Scanner diagnostics
            yield return new MainMenuItem()
            {
                Action = (arags) => RunISOScannerDiagnostics(),
                Description = "Debug: Run ISO Scanner diagnostics...",
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

        // Debug method to directly run the ISO scanner
        private void RunDirectISOScan()
        {
            try
            {
                // Show dialog to select folder
                var sourcePath = Playnite.Dialogs.SelectFolder();
                    
                if (string.IsNullOrEmpty(sourcePath))
                {
                    Logger.Info("ISO scan canceled - no folder selected");
                    return;
                }
                
                Logger.Info($"Creating ISO mapping for folder: {sourcePath}");
                
                // Ask user if they want to add a permanent mapping
                var result = Playnite.Dialogs.ShowMessage(
                    "Would you like to add a permanent ISO mapping in settings?\n\n" +
                    "This will allow Playnite to automatically detect ISO games from your selected folder.",
                    "Add ISO Mapping", System.Windows.MessageBoxButton.YesNo);
                    
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    // Find PC platform
                    var pcPlatform = Playnite.Database.Platforms
                        .FirstOrDefault(p => p.Name == "PC");
                        
                    // Create the mapping
                    var mapping = new EmulatorMapping()
                    {
                        MappingId = Guid.NewGuid(),
                        RomType = RomType.ISOInstaller,
                        SourcePath = sourcePath,
                        Enabled = true
                    };
                    
                    // Set platform if found
                    if (pcPlatform != null)
                    {
                        mapping.PlatformId = pcPlatform.SpecificationId ?? pcPlatform.Id.ToString();
                        Logger.Info($"Set platform to PC (ID: {mapping.PlatformId})");
                    }
                    
                    // Add the mapping to settings
                    Settings.Mappings.Add(mapping);
                    
                        // Save settings manually - don't use Serialization class directly
                    var settingsPath = Path.Combine(GetPluginUserDataPath(), "config.json");
                    var serializedSettings = Newtonsoft.Json.JsonConvert.SerializeObject(Settings);
                    File.WriteAllText(settingsPath, serializedSettings);
                    
                    // Notify user
                    Playnite.Notifications.Add(
                        "EmuLibrary-ISO-MappingCreated", 
                        $"ISO mapping created for {sourcePath}. Playnite will scan this folder for ISO games on next library update.",
                        NotificationType.Info);
                        
                    // Update library
                    try 
                    {
                        // Just notify instead of trying to update the library
                        Playnite.Notifications.Add(
                            "EmuLibrary-ISO-RestartRequired", 
                            "Please restart Playnite or use the Update All Libraries feature to scan for games.",
                            NotificationType.Info);
                    }
                    catch 
                    {
                        Playnite.Notifications.Add(
                            "EmuLibrary-ISO-RestartRequired", 
                            "Please restart Playnite to scan the new ISO folder.",
                            NotificationType.Info);
                    }
                }
                else // Run direct scan
                {
                    Logger.Info("Running direct scan");
                    
                    Logger.Info($"Running direct ISO scan on folder: {sourcePath}");
                    
                    // Find PC platform
                    var pcPlatform = Playnite.Database.Platforms
                        .FirstOrDefault(p => p.Name == "PC");
                    
                    // Create temporary mapping for scan
                    var mapping = new EmulatorMapping()
                    {
                        MappingId = Guid.NewGuid(),
                        RomType = RomType.ISOInstaller,
                        SourcePath = sourcePath,
                        Enabled = true
                    };
                    
                    // Set platform if found
                    if (pcPlatform != null)
                    {
                        mapping.PlatformId = pcPlatform.SpecificationId ?? pcPlatform.Id.ToString();
                        // Don't set Platform property directly
                        Logger.Info($"Set platform to PC (ID: {mapping.PlatformId})");
                    }
                    
                    // Create scanner and run it directly
                    Logger.Info("Creating ISOInstallerScanner instance");
                    var scanner = new RomTypes.ISOInstaller.ISOInstallerScanner(this);
                    
                    Logger.Info("Starting scanner.GetGames");
                    var games = scanner.GetGames(mapping, new LibraryGetGamesArgs()).ToList();
                    
                    Logger.Info($"Scanner found {games.Count} games");
                    
                    if (games.Count == 0)
                    {
                        Playnite.Notifications.Add(
                            "EmuLibrary-ISO-NoGamesFound", 
                            $"No ISO games found in {sourcePath}. Make sure your ISO files have the correct extensions and are readable.",
                            NotificationType.Error);
                        return;
                    }
                    
                    // Log all found games
                    foreach (var game in games)
                    {
                        Logger.Info($"Found game: {game.Name} (ID: {game.GameId})");
                        if (game.Platforms != null)
                        {
                            Logger.Info($"  Platforms: {string.Join(", ", game.Platforms)}");
                        }
                    }
                    
                    // Ask if user wants to add these games to Playnite
                    var addResult = Playnite.Dialogs.ShowMessage(
                        $"Found {games.Count} ISO games in {sourcePath}.\n\n" +
                        "Would you like to add these games to Playnite?",
                        "Add ISO Games", System.Windows.MessageBoxButton.YesNo);
                        
                    if (addResult == System.Windows.MessageBoxResult.Yes)
                    {
                        Logger.Info("Adding games to Playnite database");
                        
                        // Add games to Playnite
                        using (Playnite.Database.BufferedUpdate())
                        {
                            foreach (var gameMetadata in games)
                            {
                                try
                                {
                                    // Convert GameMetadata to Game
                                    var game = PlayniteApi.Database.ImportGame(gameMetadata);
                                    
                                    // Set the PluginId to match our plugin
                                    game.PluginId = Id;
                                    // Update the game in the database
                                    PlayniteApi.Database.Games.Update(game);
                                    
                                    Logger.Info($"Added game: {game.Name} (ID: {game.GameId})");
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error($"Error adding game {gameMetadata.Name}: {ex.Message}");
                                }
                            }
                        }
                        
                        Playnite.Notifications.Add(
                            "EmuLibrary-ISO-GamesAdded", 
                            $"Added {games.Count} ISO games to Playnite.",
                            NotificationType.Info);
                    }
                    else
                    {
                        Logger.Info("User chose not to add games to Playnite");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in direct ISO scan: {ex.Message}");
                Playnite.Dialogs.ShowErrorMessage($"Error in ISO scanner: {ex.Message}", "ISO Scanner Error");
            }
        }
        
        // Diagnostic function for ISO scanner
        private void RunISOScannerDiagnostics()
        {
            try
            {
                // Show diagnostic options
                var options = new List<string>
                {
                    "Test direct file search",
                    "Test scanner game creation",
                    "Check existing games in database"
                };
                
                // Most basic approach - use a message box with buttons instead of a chooser
                var dialogResult = Playnite.Dialogs.ShowMessage(
                    "Select a diagnostic option:\n\n1. Test direct file search\n2. Test scanner game creation\n3. Check existing games in database", 
                    "ISO Scanner Diagnostics",
                    System.Windows.MessageBoxButton.YesNoCancel);
                
                // Convert dialog result to index
                int selectedIndex = -1;
                if (dialogResult == System.Windows.MessageBoxResult.Yes)
                    selectedIndex = 0;
                else if (dialogResult == System.Windows.MessageBoxResult.No)
                    selectedIndex = 1;
                else if (dialogResult == System.Windows.MessageBoxResult.Cancel)
                    selectedIndex = 2;
                    
                if (selectedIndex < 0)
                {
                    Logger.Info("ISO diagnostics canceled - no option selected");
                    return;
                }
                
                if (selectedIndex == 0 || selectedIndex == 1) // File search or game creation
                {
                    // Select directory to test
                    var sourcePath = Playnite.Dialogs.SelectFolder();
                    if (string.IsNullOrEmpty(sourcePath))
                    {
                        Logger.Info("ISO diagnostics canceled - no folder selected");
                        return;
                    }
                    
                    // We can directly use the scanner from our instance
                    var isoScanner = new RomTypes.ISOInstaller.ISOInstallerScanner(this);
                    
                    // Create test helper defined in the ISOInstallerScanner.cs file
                    var tester = new RomTypes.ISOInstaller.ISOScannerTest(PlayniteApi, Logger);
                    
                    if (selectedIndex == 0) // Direct file search
                    {
                        tester.TestDirectFileSearch(sourcePath);
                    }
                    else // Game creation
                    {
                        tester.TestScannerGameCreation(sourcePath);
                    }
                    
                    // Notify completion
                    Playnite.Notifications.Add(
                        "EmuLibrary-ISO-DiagnosticsComplete", 
                        "ISO Scanner diagnostics completed. Check the log file for results.",
                        NotificationType.Info);
                }
                else if (selectedIndex == 2) // Check existing games
                {
                    // Find games from this plugin
                    var ourGames = PlayniteApi.Database.Games
                        .Where(g => g.PluginId == Id)
                        .ToList();
                        
                    Logger.Info($"Found {ourGames.Count} games from EmuLibrary plugin");
                    
                    // Count by rom type
                    var romTypeCounts = new Dictionary<string, int>();
                    var loadErrors = 0;
                    
                    foreach (var game in ourGames)
                    {
                        try
                        {
                            var gameInfo = game.GetELGameInfo();
                            var romType = gameInfo.RomType.ToString();
                            
                            if (!romTypeCounts.ContainsKey(romType))
                                romTypeCounts[romType] = 0;
                                
                            romTypeCounts[romType]++;
                        }
                        catch
                        {
                            loadErrors++;
                        }
                    }
                    
                    // Build report
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"Games in database: {ourGames.Count}");
                    sb.AppendLine();
                    sb.AppendLine("Games by ROM type:");
                    
                    foreach (var kvp in romTypeCounts)
                    {
                        sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
                    }
                    
                    if (loadErrors > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"Failed to load {loadErrors} games");
                    }
                    
                    // Check specifically for ISO installer games
                    var isoGames = ourGames.Where(g => 
                        g.GameId != null && 
                        g.GameId.Contains("ISOInstaller")).ToList();
                        
                    sb.AppendLine();
                    sb.AppendLine($"ISO Installer games: {isoGames.Count}");
                    
                    if (isoGames.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("Sample ISO games:");
                        
                        foreach (var game in isoGames.Take(5))
                        {
                            sb.AppendLine($"- {game.Name}");
                            sb.AppendLine($"  ID: {game.Id}");
                            sb.AppendLine($"  GameId: {game.GameId}");
                            sb.AppendLine($"  Installed: {game.IsInstalled}");
                            
                            if (game.Platforms != null)
                            {
                                sb.AppendLine($"  Platforms: {string.Join(", ", game.Platforms.Select(p => p.Name))}");
                            }
                            
                            sb.AppendLine();
                        }
                    }
                    
                    // Show report
                    Playnite.Dialogs.ShowMessage(sb.ToString(), "EmuLibrary Game Report");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in ISO scanner diagnostics: {ex.Message}");
                Playnite.Dialogs.ShowErrorMessage($"Error in ISO scanner diagnostics: {ex.Message}", "Diagnostic Error");
            }
        }
        
        private void RemoveSuperUninstalledGames(bool promptUser, CancellationToken ct)
        {
            try
            {
                // Get games marked for removal from active scanners
                var toRemove = _scanners.Values.SelectMany(s => s.GetUninstalledGamesMissingSourceFiles(ct)).ToList();
                
                // Special handling for ArchiveInstaller games - protect them from auto-removal
                // since the scanner for ArchiveInstaller no longer exists
                var archiveInstallerGames = PlayniteApi.Database.Games
                    .Where(g => g.PluginId == PluginId && 
                               !g.IsInstalled && 
                               g.GameId.Contains("ArchiveInstaller"))
                    .ToList();
                
                if (archiveInstallerGames.Any())
                {
                    // If we're showing a prompt, inform the user about ArchiveInstaller games
                    if (promptUser && archiveInstallerGames.Count > 0)
                    {
                        Logger.Info($"Found {archiveInstallerGames.Count} uninstalled ArchiveInstaller games that will be preserved.");
                        
                        // Remove any ArchiveInstaller games from the toRemove list
                        toRemove = toRemove.Except(archiveInstallerGames).ToList();
                        
                        // Add special notification about ArchiveInstaller games
                        Playnite.Notifications.Add(
                            "EmuLibrary-ArchiveInstaller-GamesPreserved",
                            $"{archiveInstallerGames.Count} uninstalled ArchiveInstaller games have been preserved. Extract their archives manually and use ISOInstaller to continue using them.",
                            NotificationType.Info);
                    }
                }
                
                if (toRemove.Any())
                {
                    System.Windows.MessageBoxResult res;
                    if (promptUser)
                    {
                        res = PlayniteApi.Dialogs.ShowMessage(
                            $"Delete {toRemove.Count()} library entries?\n\n" +
                            "(This may take a while, during which Playnite will seem frozen.)", 
                            "Confirm deletion", 
                            System.Windows.MessageBoxButton.YesNo);
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
                    PlayniteApi.Dialogs.ShowMessage("No games to remove.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in RemoveSuperUninstalledGames");
                if (promptUser)
                {
                    PlayniteApi.Dialogs.ShowErrorMessage(
                        $"An error occurred while trying to remove uninstalled games: {ex.Message}", 
                        "Error");
                }
            }
        }
    }
}