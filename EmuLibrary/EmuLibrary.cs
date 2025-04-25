using EmuLibrary.RomTypes;
using EmuLibrary.RomTypes.ISOInstaller;
using EmuLibrary.Settings;
using EmuLibrary.PlayniteCommon;
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
            
            // Fix ISO games with incorrect PluginId
            FixISOGamesPluginId();

            // Log that we'll use Playnite's metadata system
            Logger.Info("Using Playnite's built-in metadata system for game information and assets");
            
            // Check if auto metadata is enabled
            if (Settings.AutoRequestMetadata)
            {
                Logger.Info("Auto-download metadata for imported games is enabled");
            }

            Settings.Mappings.ToList().ForEach(mapping =>
            {
                _scanners.Values.ToList().ForEach(scanner =>
                {
                    var oldGameIdFormat = PlayniteApi.Database.Games.Where(game => game.PluginId == scanner.LegacyPluginId && !game.GameId.StartsWith("!")).ToList();
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

                // Check if this is a valid mapping with a supported RomType
                if (!Enum.IsDefined(typeof(RomType), mapping.RomType))
                {
                    Logger.Warn($"Unsupported RomType {mapping.RomType} for mapping {mapping.MappingId}, skipping.");
                    continue;
                }
                
                // Skip any legacy or invalid mapping types
                if (!_scanners.ContainsKey(mapping.RomType))
                {
                    Logger.Warn($"No scanner registered for RomType {mapping.RomType}, skipping mapping {mapping.MappingId}.");
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
                        .FirstOrDefault(p => p.Name == "PC" || p.Name == "Windows" || p.Name == "PC (Windows)");
                        
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
                        
                        // Try to use ANY platform as a fallback
                        var anyPlatform = Playnite.Database.Platforms.FirstOrDefault(p => p != null);
                        if (anyPlatform != null)
                        {
                            mapping.PlatformId = anyPlatform.SpecificationId ?? anyPlatform.Id.ToString();
                            Logger.Info($"Using fallback platform: {anyPlatform.Name} (ID: {mapping.PlatformId})");
                        }
                        else
                        {
                            Logger.Warn($"Could not find ANY platform in database. This will prevent games from appearing correctly.");
                            
                            // Create a fallback platform ID if needed
                            if (string.IsNullOrEmpty(mapping.PlatformId))
                            {
                                mapping.PlatformId = "pc_windows"; // Try a common specification ID
                                Logger.Info("Set generic PC platform ID as fallback");
                            }
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
                            var platformNames = string.Join(", ", g.Platforms.OfType<MetadataNameProperty>().Select(p => p.Name));
                            Logger.Info($"Platforms: {platformNames}");
                        }
                        
                        if (g.Tags != null && g.Tags.Any())
                        {
                            var tagNames = string.Join(", ", g.Tags.OfType<MetadataNameProperty>().Select(t => t.Name));
                            Logger.Info($"Tags: {tagNames}");
                        }
                    }
                    
                    // CRITICAL FIX: For ISO games only, ensure we import and set PluginId directly
                    // For other game types, let Playnite handle the import process
                    
                    // For ISO games, process import directly for better control
                    if (mapping.RomType == RomType.ISOInstaller)
                    {
                        // We can't yield inside a try/catch, so handle exceptions without breaking the yield
                        bool importSuccess = false;
                        
                        // Try importing the game metadata and updating the plugin ID
                        try
                        {
                            // Check if we already have a game with this name to avoid duplicates
                            var existingGames = PlayniteApi.Database.Games
                                .Where(existing => existing.Name == g.Name && existing.PluginId == Id)
                                .ToList();
                                
                            if (existingGames.Any())
                            {
                                // Remove existing games with the same name to avoid duplicates
                                Logger.Warn($"Removing {existingGames.Count} existing games with name '{g.Name}' to avoid duplicates");
                                PlayniteApi.Database.Games.Remove(existingGames);
                            }
                            
                            // Import the game metadata to get a Game object
                            var game = PlayniteApi.Database.ImportGame(g);
                            
                            // Use utility method to ensure PluginId is set correctly
                            EnsurePluginId(game);
                            
                            // Request metadata for the game if needed
                            // This allows Playnite's metadata providers to fill in missing information
                            // such as covers, backgrounds, descriptions from sources like SteamGridDB
                            if (Settings.AutoRequestMetadata)
                            {
                                Logger.Info($"Auto-requesting metadata for ISO game: {game.Name}");
                                try
                                {
                                    // Note: We can't directly trigger metadata download through the API
                                    // User will need to manually request metadata through the UI
                                    
                                    // Log for troubleshooting purposes
                                    Logger.Info($"Game '{game.Name}' added to library. User can download metadata from the context menu.");
                                }
                                catch (Exception mdEx)
                                {
                                    Logger.Error($"Error requesting metadata for game '{game.Name}': {mdEx.Message}");
                                }
                            }
                            
                            // Notify user of ISO game addition to verify visibility
                            Playnite.Notifications.Add(
                                $"EmuLibrary-ISO-GameAdded-{Guid.NewGuid()}",
                                $"Added ISO game: {g.Name}",
                                NotificationType.Info);
                            
                            // Log success for troubleshooting
                            Logger.Info($"ISO game '{g.Name}' successfully imported with ID: {game.Id}, PluginId: {game.PluginId}");
                            
                            importSuccess = true;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error importing ISO game {g.Name}: {ex.Message}");
                        }
                        
                        // For ISO games, only return if import failed
                        // This prevents duplicate games from appearing
                        if (!importSuccess)
                        {
                            yield return g;
                        }
                    }
                    else
                    {
                        // For non-ISO games, return normally and let Playnite handle the import
                        yield return g;
                    }
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

            // Ensure the installed game has the correct PluginId
            if (args.Game != null && args.Game.GameId != null && args.Game.GameId.StartsWith("!"))
            {
                EnsurePluginId(args.Game);
                
                try 
                {
                    // For ISO installer games, ensure paths are preserved
                    if (args.Game.GameId.StartsWith("!"))
                    {
                        var elInfo = args.Game.GetELGameInfo();
                        if (elInfo?.RomType == RomType.ISOInstaller)
                        {
                            var isoInfo = elInfo as ISOInstaller.ISOInstallerGameInfo;
                            Logger.Info($"Preserving ISO paths after installation for {args.Game.Name}: SourcePath={isoInfo.SourcePath}, InstallerFullPath={isoInfo.InstallerFullPath}");
                            
                            // If we have a valid game with paths
                            if (!string.IsNullOrEmpty(isoInfo.SourcePath) && !string.IsNullOrEmpty(isoInfo.InstallerFullPath))
                            {
                                // Add to game properties to ensure persistent storage
                                args.Game.OtherActions = new System.Collections.ObjectModel.ObservableCollection<GameAction>()
                                {
                                    new GameAction()
                                    {
                                        Name = "Browse ISO Source",
                                        Path = isoInfo.InstallerFullPath,
                                        Type = GameActionType.File
                                    }
                                };
                                
                                // Request metadata refresh to apply changes if needed
                                bool isAutoMetadata = Settings.AutoRequestMetadata;
                                Logger.Info($"Auto-download metadata for game: {isAutoMetadata}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error preserving ISO paths: {ex.Message}");
                }
            }

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
            
            // Add menu item to clear hardcoded descriptions from existing games
            yield return new MainMenuItem()
            {
                Action = (arags) => ClearHardcodedDescriptions(),
                Description = "Clear hardcoded descriptions...",
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
            
            // Add a test menu item for directly importing ISO games
            yield return new MainMenuItem()
            {
                Action = (arags) => TestDirectImportISOGames(),
                Description = "Debug: Test direct ISO import...",
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
                        ourGameInfos.ToList().ForEach(ggi => ggi.gameInfo.BrowseToSource());
                    },
                    Description = "Browse to Source...",
                    MenuSection = "EmuLibrary"
                };
                
                // Menu item for installer-type games that are not installed
                var uninstalledInstallerGames = ourGameInfos
                    .Where(ggi => (ggi.gameInfo.RomType == RomType.PCInstaller || 
                                  ggi.gameInfo.RomType == RomType.ISOInstaller) && 
                                  !ggi.game.IsInstalled);
                
                if (uninstalledInstallerGames.Any())
                {
                    yield return new GameMenuItem()
                    {
                        Action = (arags) =>
                        {
                            uninstalledInstallerGames.ToList().ForEach(ggi => 
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
                                    
                                    // Use the utility method to ensure PluginId is set correctly
                                    // CRITICAL: PluginId must be set for the game to appear in Playnite's UI
                                    EnsurePluginId(game);
                                    
                                    Logger.Info($"Added game: {game.Name} (ID: {game.GameId}, PluginId: {game.PluginId})");
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
        
        // Test method for directly importing ISO games to diagnose UI visibility issues
        private void TestDirectImportISOGames()
        {
            try
            {
                // Show dialog to select an ISO file directly
                var isoFilePath = Playnite.Dialogs.SelectFile("ISO Files|*.iso;*.ISO;*.bin;*.BIN;*.cue;*.CUE;*.img;*.IMG;*.mdf;*.MDF;*.mds;*.MDS|All Files|*.*");
                
                if (string.IsNullOrEmpty(isoFilePath))
                {
                    Logger.Info("ISO import test canceled - no file selected");
                    return;
                }
                
                Logger.Info($"Creating test game from ISO file: {isoFilePath}");
                
                // Extract game name from directory or file name
                var parentDir = Path.GetDirectoryName(isoFilePath);
                var parentFolderName = Path.GetFileName(parentDir);
                var fileName = Path.GetFileNameWithoutExtension(isoFilePath);
                
                var gameName = !string.IsNullOrEmpty(parentFolderName) ? 
                    parentFolderName.Replace("-", " ") : 
                    fileName.Replace("-", " ");
                
                // Ensure game name is cleaned up
                gameName = gameName.Trim();
                Logger.Info($"Using game name: {gameName}");
                
                // Find PC platform
                var pcPlatform = Playnite.Database.Platforms
                    .FirstOrDefault(p => p.Name == "PC" || p.Name == "Windows" || p.Name == "PC (Windows)");
                    
                if (pcPlatform == null)
                {
                    Logger.Error("Could not find PC platform in database. This will prevent the game from appearing correctly.");
                    Playnite.Dialogs.ShowErrorMessage("Could not find PC platform in database. This will prevent the game from appearing correctly.", "Platform Missing");
                    
                    // Attempt to create the platform if it doesn't exist
                    var result = Playnite.Dialogs.ShowMessage(
                        "Would you like to create a PC platform in your database?",
                        "Create Platform",
                        System.Windows.MessageBoxButton.YesNo);
                        
                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        pcPlatform = new Platform("PC (Windows)");
                        pcPlatform.SpecificationId = "pc_windows";
                        Playnite.Database.Platforms.Add(pcPlatform);
                        Logger.Info($"Created new PC platform with ID: {pcPlatform.Id}");
                    }
                    else
                    {
                        return;
                    }
                }
                
                // Create a test mapping for the ISO file
                var mapping = new EmulatorMapping()
                {
                    MappingId = Guid.NewGuid(),
                    RomType = RomType.ISOInstaller,
                    SourcePath = Path.GetDirectoryName(isoFilePath),
                    Enabled = true,
                    PlatformId = pcPlatform.SpecificationId ?? pcPlatform.Id.ToString()
                };
                
                Logger.Info($"Created test mapping with platform ID: {mapping.PlatformId}");
                
                // Create game info object
                var info = new RomTypes.ISOInstaller.ISOInstallerGameInfo()
                {
                    MappingId = mapping.MappingId,
                    SourcePath = Path.GetFileName(isoFilePath),
                    InstallerFullPath = isoFilePath,
                    InstallDirectory = null
                };
                
                // Create game metadata
                var metadata = new GameMetadata
                {
                    Source = EmuLibrary.SourceName,
                    Name = gameName,
                    IsInstalled = false,
                    GameId = info.AsGameId(),
                    // PluginId is set on Game objects after import, not on GameMetadata
                    Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(pcPlatform.Name) },
                    InstallSize = (ulong)new FileInfo(isoFilePath).Length,
                    GameActions = new List<GameAction>() 
                    { 
                        new GameAction()
                        {
                            Name = "Install Game",
                            Type = GameActionType.URL,
                            Path = "",
                            IsPlayAction = false
                        }
                    }
                };
                
                // Don't set description to allow metadata providers to fill it
                // This is a test import, so we'll add a tag to identify it instead
                
                // Add tags to identify the ISO type
                metadata.Tags = new HashSet<MetadataProperty>() { 
                    new MetadataNameProperty("ISO Installer"),
                    new MetadataNameProperty("PC Game"),
                    new MetadataNameProperty("TEST")
                };
                
                // Store additional information in Properties
                metadata.AddProperty("ISOFile", isoFilePath);
                metadata.AddProperty("SourcePath", mapping.SourcePath);
                
                // Log details before import
                Logger.Info($"TEST IMPORT - Game: {metadata.Name}");
                Logger.Info($"TEST IMPORT - GameId: {metadata.GameId}");
                Logger.Info($"TEST IMPORT - PluginId: {Id} (will be set after import)");
                var platformNames = string.Join(", ", metadata.Platforms.OfType<MetadataNameProperty>().Select(p => p.Name));
                Logger.Info($"TEST IMPORT - Platform: {platformNames}");
                
                // Ask user to confirm import
                var importResult = Playnite.Dialogs.ShowMessage(
                    $"Ready to import test game:\n\n" +
                    $"Name: {metadata.Name}\n" +
                    $"Platform: {pcPlatform.Name}\n" +
                    $"ISO File: {Path.GetFileName(isoFilePath)}\n\n" +
                    "Do you want to proceed with the import?",
                    "Import Test Game",
                    System.Windows.MessageBoxButton.YesNo);
                    
                if (importResult == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        // Import the game to the database
                        Logger.Info("Importing game to database...");
                        
                        // Declare game variable at this scope so it's available throughout the try block
                        Game game = null;
                        
                        // First, check if this game already exists by GameId to avoid duplicates
                        var existingGame = PlayniteApi.Database.Games
                            .FirstOrDefault(g => g.GameId == metadata.GameId);
                            
                        if (existingGame != null)
                        {
                            Logger.Warn($"Game with GameId {metadata.GameId} already exists in the database. Updating instead of creating new.");
                            
                            // Update the existing game
                            existingGame.Name = metadata.Name;
                            existingGame.Description = metadata.Description;
                            
                            // Clear existing platforms and add new ones
                            if (existingGame.Platforms != null)
                            {
                                existingGame.Platforms.Clear();
                            }
                            
                            if (metadata.Platforms != null)
                            {
                                foreach (var platform in metadata.Platforms.OfType<MetadataNameProperty>())
                                {
                                    existingGame.Platforms.Add(new Platform(platform.Name));
                                }
                            }
                            
                            // Clear existing tags and add new ones
                            if (existingGame.Tags != null)
                            {
                                existingGame.Tags.Clear();
                            }
                            
                            if (metadata.Tags != null)
                            {
                                foreach (var tag in metadata.Tags.OfType<MetadataNameProperty>())
                                {
                                    existingGame.Tags.Add(new Tag(tag.Name));
                                }
                            }
                                
                            // Ensure PluginId is set correctly
                            if (existingGame.PluginId != Id)
                            {
                                Logger.Warn($"Existing game had incorrect PluginId: {existingGame.PluginId}, updating to: {Id}");
                                existingGame.PluginId = Id;
                            }
                            
                            // Update in database
                            PlayniteApi.Database.Games.Update(existingGame);
                            Logger.Info($"Updated existing game with ID: {existingGame.Id}");
                            
                            // Use the existing game for the rest of the code
                            game = existingGame;
                            
                            // Show to user for debugging
                            Playnite.Dialogs.ShowMessage($"Updated existing game with ID: {game.Id}\nName: {game.Name}\nPluginId: {game.PluginId}", "Game Updated");
                        }
                        else
                        {
                            // Method 1: Use ImportGame which converts GameMetadata to Game
                            Logger.Info("Importing as new game using ImportGame method");
                            
                            // Diagnostic: Print GameId before import
                            Logger.Info($"Before import - GameId: {metadata.GameId}");
                            
                            game = PlayniteApi.Database.ImportGame(metadata);
                            
                            // Log the game after import
                            Logger.Info($"After import - Game ID: {game.Id}");
                            Logger.Info($"After import - PluginId: {game.PluginId}");
                            Logger.Info($"After import - GameId: {game.GameId}");
                            
                            // Check if Platform was properly set
                            if (game.Platforms == null || !game.Platforms.Any())
                            {
                                Logger.Error("Platform was not set on imported game!");
                                
                                // Try to fix it - must use collection methods since Platforms is read-only
                                game.Platforms.Add(new Platform(pcPlatform.Name));
                                Logger.Info($"Manually added platform: {pcPlatform.Name}");
                            }
                            else
                            {
                                Logger.Info($"Platforms after import: {string.Join(", ", game.Platforms.Select(p => p.Name))}");
                            }
                            
                            // Critical - ensure PluginId is set to match our plugin
                            EnsurePluginId(game);
                            
                            // Show to user for debugging
                            Playnite.Dialogs.ShowMessage($"Created new game with ID: {game.Id}\nName: {game.Name}\nPluginId: {game.PluginId}", "Game Created");
                        }
                        
                        // Update the temporary mapping to persist it
                        Settings.Mappings.Add(mapping);
                        
                        // Save settings
                        var settingsPath = Path.Combine(GetPluginUserDataPath(), "config.json");
                        var serializedSettings = Newtonsoft.Json.JsonConvert.SerializeObject(Settings);
                        File.WriteAllText(settingsPath, serializedSettings);
                        
                        Playnite.Notifications.Add(
                            "EmuLibrary-Test-GameImported", 
                            $"Test game '{gameName}' was imported. It should now appear in your library.",
                            NotificationType.Info);
                            
                        // Try to search for the game to verify it exists
                        var importedGame = PlayniteApi.Database.Games.FirstOrDefault(g => g.Id == game.Id);
                        if (importedGame != null)
                        {
                            Logger.Info($"Successfully verified game in database: {importedGame.Name}");
                        }
                        else
                        {
                            Logger.Error("Could not find the imported game in database! This suggests a synchronization issue.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error importing game: {ex.Message}");
                        Playnite.Dialogs.ShowErrorMessage($"Error importing game: {ex.Message}", "Import Error");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in test ISO import: {ex.Message}");
                Playnite.Dialogs.ShowErrorMessage($"Error in test ISO import: {ex.Message}", "Import Error");
            }
        }
        
        private void RemoveSuperUninstalledGames(bool promptUser, CancellationToken ct)
        {
            try
            {
                // Get games marked for removal from active scanners
                var toRemove = _scanners.Values.SelectMany(s => s.GetUninstalledGamesMissingSourceFiles(ct)).ToList();
                // No special cases for game removal
                
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
        
        /// <summary>
        /// Utility method to ensure PluginId is set correctly on a Game object after import
        /// This is critical for games to appear in the Playnite UI
        /// </summary>
        private void EnsurePluginId(Game game)
        {
            if (game == null)
            {
                Logger.Error("Cannot set PluginId on null game object");
                return;
            }
            
            if (game.PluginId != Id)
            {
                Logger.Info($"Setting PluginId for game '{game.Name}' to {Id} (was: {game.PluginId})");
                game.PluginId = Id;
                
                // Update the game in the database to save the PluginId
                try
                {
                    PlayniteApi.Database.Games.Update(game);
                    Logger.Info($"Successfully updated PluginId for game '{game.Name}' in database");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to update PluginId for game '{game.Name}': {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Utility method to scan the database for ISO games with incorrect PluginId
        /// and fix them. This ensures games are visible in the UI.
        /// </summary>
        private void ClearHardcodedDescriptions()
        {
            try
            {
                Logger.Info("Scanning for games with hardcoded descriptions...");
                
                // Get all games belonging to this plugin
                var ourGames = PlayniteApi.Database.Games
                    .Where(g => g.PluginId == Id)
                    .ToList();
                    
                Logger.Info($"Found {ourGames.Count} games from this plugin");
                
                int fixedGames = 0;
                
                // Start a confirmation dialog
                var result = PlayniteApi.Dialogs.ShowMessage(
                    $"This will clear descriptions for games that have hardcoded texts like 'ISO installer game from...' so that Playnite metadata providers can fill them with proper descriptions.\n\n" +
                    $"Found {ourGames.Count} games to check. Continue?",
                    "Clear Hardcoded Descriptions",
                    System.Windows.MessageBoxButton.YesNo);
                    
                if (result != System.Windows.MessageBoxResult.Yes)
                {
                    Logger.Info("Operation cancelled by user");
                    return;
                }
                
                using (PlayniteApi.Database.BufferedUpdate())
                {
                    foreach (var game in ourGames)
                    {
                        // Check if the game has a hardcoded description
                        if (!string.IsNullOrEmpty(game.Description) && 
                            (game.Description.Contains("ISO installer game from") || 
                             game.Description.Contains("TEST: ISO installer game from")))
                        {
                            // Clear the hardcoded description
                            Logger.Info($"Clearing hardcoded description for game '{game.Name}'");
                            game.Description = null;
                            PlayniteApi.Database.Games.Update(game);
                            fixedGames++;
                        }
                    }
                }
                
                if (fixedGames > 0)
                {
                    Logger.Info($"Cleared descriptions for {fixedGames} games");
                    
                    // Show completion message
                    PlayniteApi.Dialogs.ShowMessage(
                        $"Successfully cleared descriptions for {fixedGames} games.\n\n" +
                        "To get proper descriptions, select these games in your library, right-click and select 'Download metadata' from the context menu.",
                        "Descriptions Cleared");
                        
                    // Add notification to inform user
                    Playnite.Notifications.Add(
                        "EmuLibrary-Descriptions-Cleared",
                        $"Cleared hardcoded descriptions for {fixedGames} games. Use 'Download metadata' to get proper descriptions.",
                        NotificationType.Info);
                }
                else
                {
                    Logger.Info("No games with hardcoded descriptions found");
                    
                    // Show completion message
                    PlayniteApi.Dialogs.ShowMessage(
                        "No games with hardcoded descriptions were found in your library.",
                        "No Changes Needed");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error clearing hardcoded descriptions: {ex.Message}");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"Error clearing descriptions: {ex.Message}",
                    "Error");
            }
        }
        
        private void FixISOGamesPluginId()
        {
            try
            {
                Logger.Info("Scanning for ISO games with incorrect PluginId...");
                
                // Get all games that might be ISO installer games based on their GameId
                var potentialISOGames = PlayniteApi.Database.Games
                    .Where(g => g.GameId != null && g.GameId.Contains("ISOInstaller"))
                    .ToList();
                    
                Logger.Info($"Found {potentialISOGames.Count} potential ISO games based on GameId");
                
                int fixedGames = 0;
                int pathsFixed = 0;
                
                using (PlayniteApi.Database.BufferedUpdate())
                {
                    foreach (var game in potentialISOGames)
                    {
                        if (game.PluginId != Id)
                        {
                            // Game has the wrong PluginId, fix it
                            Logger.Warn($"Fixing PluginId for ISO game '{game.Name}' from {game.PluginId} to {Id}");
                            game.PluginId = Id;
                            PlayniteApi.Database.Games.Update(game);
                            fixedGames++;
                        }
                        
                        // For all ISO games, ensure paths are preserved
                        try
                        {
                            // Get the game info and check if it's an ISO installer game
                            var gameInfo = game.GetELGameInfo();
                            if (gameInfo?.RomType == RomType.ISOInstaller)
                            {
                                var isoInfo = gameInfo as ISOInstallerGameInfo;
                                Logger.Info($"Checking ISO paths for {game.Name}: SourcePath={isoInfo.SourcePath}, InstallerFullPath={isoInfo.InstallerFullPath}");
                                
                                // If paths are incomplete, try to resolve them
                                if (string.IsNullOrEmpty(isoInfo.InstallerFullPath) || !File.Exists(isoInfo.InstallerFullPath))
                                {
                                    // Force path resolution via SourceFullPath property
                                    var sourcePath = isoInfo.SourceFullPath;
                                    if (!string.IsNullOrEmpty(sourcePath) && File.Exists(sourcePath) && 
                                        sourcePath != isoInfo.InstallerFullPath)
                                    {
                                        // Update the path info
                                        Logger.Info($"Updating ISO path for {game.Name} to: {sourcePath}");
                                        isoInfo.InstallerFullPath = sourcePath;
                                        game.GameId = isoInfo.AsGameId(); // Update serialized info
                                        PlayniteApi.Database.Games.Update(game);
                                        pathsFixed++;
                                    }
                                }
                            }
                        }
                        catch (Exception pathEx)
                        {
                            // This may not be an ISO game despite matching our filter
                            Logger.Debug($"Game {game.Name} is not an ISO installer game or has invalid data: {pathEx.Message}");
                        }
                    }
                }
                
                if (fixedGames > 0 || pathsFixed > 0)
                {
                    Logger.Info($"Fixed PluginId for {fixedGames} ISO games, fixed paths for {pathsFixed} games");
                    
                    // Add notification to inform user
                    Playnite.Notifications.Add(
                        "EmuLibrary-ISO-FixedPluginIds",
                        $"Fixed {fixedGames} ISO games that had incorrect plugin IDs. Fixed paths for {pathsFixed} ISO games. They should now appear in your library.",
                        NotificationType.Info);
                }
                else
                {
                    Logger.Info("No ISO games with incorrect PluginId found");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error fixing ISO games PluginId: {ex.Message}");
            }
        }
    }
}