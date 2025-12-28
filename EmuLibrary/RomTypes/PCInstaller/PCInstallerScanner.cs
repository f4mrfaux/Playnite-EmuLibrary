using EmuLibrary.PlayniteCommon;
using EmuLibrary.Settings;
using EmuLibrary.Util;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace EmuLibrary.RomTypes.PCInstaller
{
    internal class PCInstallerScanner : RomTypeScanner
    {
        private readonly IPlayniteAPI _playniteAPI;
        private readonly IEmuLibrary _emuLibrary;
        private const int BATCH_SIZE = 100; // Process games in batches for better performance

        // Rate limiting for metadata API calls
        private static DateTime _lastMetadataCallTime = DateTime.MinValue;
        private static readonly object _metadataRateLimitLock = new object();
        private const int METADATA_API_DELAY_MS = 200; // Minimum 200ms between metadata API calls

        public override RomType RomType => RomType.PCInstaller;
        public override Guid LegacyPluginId => EmuLibrary.PluginId;

        public PCInstallerScanner(IEmuLibrary emuLibrary) : base(emuLibrary)
        {
            _playniteAPI = emuLibrary.Playnite;
            _emuLibrary = emuLibrary;
        }

        public override IEnumerable<GameMetadata> GetGames(EmulatorMapping mapping, LibraryGetGamesArgs args)
        {
            if (args.CancelToken.IsCancellationRequested)
            {
                _emuLibrary.Logger.Info("Scanning cancelled before starting");
                yield break;
            }

            // Validate mapping and emulator configuration
            if (mapping.EmulatorProfile == null)
            {
                _emuLibrary.Logger.Error($"No emulator profile specified for mapping {mapping.MappingId}");
                yield break;
            }

            // Validate paths
            var srcPath = mapping.SourcePath;
            var dstPath = mapping.DestinationPathResolved;

            if (string.IsNullOrEmpty(srcPath))
            {
                _emuLibrary.Logger.Error($"Source path is null or empty for mapping {mapping.MappingId}");
                yield break;
            }

            if (!Directory.Exists(srcPath))
            {
                _emuLibrary.Logger.Error($"Source directory does not exist: {srcPath}");
                yield break;
            }

            // Support EXE files, ISO files, and archive files (ZIP, RAR, 7Z) for unified PC game installer
            // PCInstaller now handles all PC game formats: EXE installers, ISO disc images, and archives containing either
            var installerExtensions = new List<string> { "exe", "iso", "zip", "rar", "7z", "7zip" };
            
            SafeFileEnumerator fileEnumerator;
            var gameMetadataBatch = new List<GameMetadata>();

            #region Import discovered installers
            var sourcedGames = new List<GameMetadata>();
            try
            {
                fileEnumerator = new SafeFileEnumerator(srcPath, "*.*", SearchOption.AllDirectories);
                _emuLibrary.Logger.Info($"Scanning for PC installers in {srcPath}");

                // Create a dictionary to cache normalized folder names for performance
                var normalizedNameCache = new Dictionary<string, string>();
                
                // CRITICAL: Detect directories that are likely extracted game content
                // These should be skipped to avoid duplicate entries
                HashSet<string> extractedContentFolders = new HashSet<string>();
                var extractedContentPatterns = new List<string> {
                    "setup.exe", "install.exe", "launcher.exe", "game.exe", "bin", "data", "INSTALL", "DATA", "redist"
                };
                
                // Pre-scan to identify extracted content folders
                try
                {
                    var directories = Directory.GetDirectories(srcPath, "*", SearchOption.AllDirectories);
                    foreach (var dir in directories)
                    {
                        if (args.CancelToken.IsCancellationRequested)
                            yield break;
                            
                        try
                        {
                            // Count files and folders in this directory
                            int fileCount = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly).Length;
                            int folderCount = Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly).Length;
                            
                            // If too many files or folders, likely an extracted game
                            if (fileCount > 15 || folderCount > 5)
                            {
                                extractedContentFolders.Add(dir);
                                _emuLibrary.Logger.Debug($"Detected potential extracted content: {dir} (files: {fileCount}, folders: {folderCount})");
                                continue;
                            }
                            
                            // Check for common extracted content patterns
                            bool hasExtractedContentPattern = false;
                            var dirFiles = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                                .Select(f => Path.GetFileName(f).ToLowerInvariant())
                                .ToList();
                                
                            foreach (var pattern in extractedContentPatterns)
                            {
                                if (dirFiles.Any(f => f.Contains(pattern.ToLowerInvariant())))
                                {
                                    hasExtractedContentPattern = true;
                                    _emuLibrary.Logger.Debug($"Detected extracted content pattern in {dir}: {pattern}");
                                    break;
                                }
                            }
                            
                            if (hasExtractedContentPattern)
                            {
                                extractedContentFolders.Add(dir);
                            }
                            
                            // Check for system folder names
                            var folderName = Path.GetFileName(dir).ToLowerInvariant();
                            if (folderName == "system" || folderName == "windows" || folderName == "program files" || 
                                folderName == "users" || folderName == "games" || folderName == "desktop" || 
                                folderName == "documents" || folderName == "downloads")
                            {
                                extractedContentFolders.Add(dir);
                            }
                        }
                        catch (Exception ex)
                        {
                            _emuLibrary.Logger.Error($"Error checking directory {dir}: {ex.Message}");
                        }
                    }
                    
                    _emuLibrary.Logger.Info($"Pre-scan found {extractedContentFolders.Count} potential extracted content folders");
                }
                catch (Exception ex)
                {
                    _emuLibrary.Logger.Error($"Error during pre-scan: {ex.Message}");
                }
                
                // Group files by folder for multi-disc support (especially for ISO files)
                var filesByFolder = new Dictionary<string, List<FileSystemInfoBase>>();
                var isoFilesByFolder = new Dictionary<string, List<FileSystemInfoBase>>();
                
                // First pass: collect all files and group by folder
                foreach (var file in fileEnumerator)
                {
                    if (args.CancelToken.IsCancellationRequested)
                    {
                        _emuLibrary.Logger.Info("Scanning cancelled during file enumeration");
                        yield break;
                    }

                    // Skip if file is in an extracted content folder
                    var parentFolderPath = Path.GetDirectoryName(file.FullName);
                    if (parentFolderPath != null && extractedContentFolders.Contains(parentFolderPath))
                    {
                        _emuLibrary.Logger.Debug($"Skipping file in extracted content folder: {file.FullName}");
                        continue;
                    }

                    foreach (var extension in installerExtensions)
                    {
                        if (args.CancelToken.IsCancellationRequested)
                            yield break;

                        if (HasMatchingExtension(file, extension))
                        {
                            if (parentFolderPath == null)
                            {
                                _emuLibrary.Logger.Warn($"Could not get parent directory for {file.FullName}, skipping");
                                continue;
                            }
                            
                            // Group ISO files separately for multi-disc support
                            if (extension.Equals("iso", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!isoFilesByFolder.ContainsKey(parentFolderPath))
                                {
                                    isoFilesByFolder[parentFolderPath] = new List<FileSystemInfoBase>();
                                }
                                isoFilesByFolder[parentFolderPath].Add(file);
                            }
                            else
                            {
                                // Group other files (EXE, archives) by folder
                                if (!filesByFolder.ContainsKey(parentFolderPath))
                                {
                                    filesByFolder[parentFolderPath] = new List<FileSystemInfoBase>();
                                }
                                filesByFolder[parentFolderPath].Add(file);
                            }
                        }
                    }
                }
                
                // Process ISO files grouped by folder (multi-disc support)
                foreach (var folder in isoFilesByFolder.Keys)
                {
                    if (args.CancelToken.IsCancellationRequested)
                        yield break;
                    
                    var folderIsoFiles = isoFilesByFolder[folder];
                    var folderName = Path.GetFileName(folder);
                    
                    // Use cached normalized name if available
                    string gameName;
                    if (!normalizedNameCache.TryGetValue(folderName, out gameName))
                    {
                        var patterns = EmuLibrary.Settings.EnableGameNameNormalization
                            ? EmuLibrary.Settings.GameNameNormalizationPatterns?.ToArray()
                            : null;
                        gameName = StringExtensions.NormalizeGameName(folderName, patterns);
                        
                        // Try metadata lookup
                        if (!string.IsNullOrEmpty(gameName) && gameName.Length > 3)
                        {
                            try
                            {
                                var metadataName = TryGetMetadataName(gameName, mapping.Platform);
                                if (!string.IsNullOrEmpty(metadataName) && 
                                    !string.Equals(metadataName, gameName, StringComparison.OrdinalIgnoreCase) &&
                                    metadataName.Length <= gameName.Length * 1.2)
                                {
                                    gameName = metadataName;
                                }
                            }
                            catch (Exception ex)
                            {
                                _emuLibrary.Logger.Debug($"Could not get metadata name for '{gameName}': {ex.Message}");
                            }
                        }
                        normalizedNameCache[folderName] = gameName;
                    }
                    
                    if (string.IsNullOrEmpty(gameName))
                    {
                        gameName = Path.GetFileNameWithoutExtension(folderIsoFiles[0].Name);
                        var patterns = EmuLibrary.Settings.EnableGameNameNormalization
                            ? EmuLibrary.Settings.GameNameNormalizationPatterns?.ToArray()
                            : null;
                        gameName = StringExtensions.NormalizeGameName(gameName, patterns);
                    }
                    
                    // Select primary ISO file (prioritize "disc 1", .m3u, smallest)
                    var primaryIsoFile = folderIsoFiles
                        .OrderBy(f => !Path.GetFileName(f.FullName).ToLowerInvariant().Contains("disc 1"))
                        .ThenBy(f => !Path.GetExtension(f.FullName).ToLowerInvariant().Equals(".m3u"))
                        .ThenBy(f => new FileInfo(f.FullName).Length)
                        .First();

                    if (!primaryIsoFile.FullName.StartsWith(srcPath, StringComparison.OrdinalIgnoreCase))
                    {
                        EmuLibrary.Logger.Warn($"Installer path '{primaryIsoFile.FullName}' doesn't start with expected source path '{srcPath}'. Skipping installer.");
                        continue;
                    }
                    var relativePath = primaryIsoFile.FullName.Substring(srcPath.Length).TrimStart(Path.DirectorySeparatorChar);
                    
                    var info = new PCInstallerGameInfo()
                    {
                        MappingId = mapping.MappingId,
                        SourcePath = relativePath,
                        InstallerFullPath = primaryIsoFile.FullName,
                        InstallDirectory = null,
                        StoreGameId = null,
                        InstallerType = null
                    };
                    
                    var metadata = new GameMetadata()
                    {
                        Source = EmuLibrary.SourceName,
                        Name = gameName,
                        IsInstalled = false,
                        GameId = info.AsGameId(),
                        Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(mapping.Platform?.Name ?? "PC") },
                        InstallSize = folderIsoFiles.Select(f => (ulong)new FileInfo(f.FullName).Length).Aggregate((a, b) => a + b),
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
                    
                    if (folderIsoFiles.Count > 1)
                    {
                        metadata.Tags = new HashSet<MetadataProperty>() { 
                            new MetadataNameProperty($"Multi-Disc ({folderIsoFiles.Count} discs)") 
                        };
                        _emuLibrary.Logger.Info($"Found multi-disc game: {gameName} with {folderIsoFiles.Count} ISO files");
                    }
                    
                    sourcedGames.Add(metadata);
                }
                
                // Process other files (EXE, archives) - can be individual or grouped
                foreach (var folder in filesByFolder.Keys)
                {
                    if (args.CancelToken.IsCancellationRequested)
                        yield break;
                    
                    var folderFiles = filesByFolder[folder];
                    var folderName = Path.GetFileName(folder);
                    
                    // Process each file in the folder (EXE and archives are typically one per game)
                    foreach (var file in folderFiles)
                    {
                        try
                        {
                            // Use cached normalized name if available
                            string gameName;
                            if (!normalizedNameCache.TryGetValue(folderName, out gameName))
                            {
                                var patterns = EmuLibrary.Settings.EnableGameNameNormalization
                                    ? EmuLibrary.Settings.GameNameNormalizationPatterns?.ToArray()
                                    : null;
                                gameName = StringExtensions.NormalizeGameName(folderName, patterns);
                                
                                // Try metadata lookup
                                if (!string.IsNullOrEmpty(gameName) && gameName.Length > 3)
                                {
                                    try
                                    {
                                        var metadataName = TryGetMetadataName(gameName, mapping.Platform);
                                        if (!string.IsNullOrEmpty(metadataName) && 
                                            !string.Equals(metadataName, gameName, StringComparison.OrdinalIgnoreCase) &&
                                            metadataName.Length <= gameName.Length * 1.2)
                                        {
                                            gameName = metadataName;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _emuLibrary.Logger.Debug($"Could not get metadata name for '{gameName}': {ex.Message}");
                                    }
                                }
                                normalizedNameCache[folderName] = gameName;
                            }
                            
                            if (string.IsNullOrEmpty(gameName))
                            {
                                _emuLibrary.Logger.Warn($"Game name is empty for {file.FullName}, using file name instead");
                                gameName = Path.GetFileNameWithoutExtension(file.Name);
                            }

                            if (!file.FullName.StartsWith(srcPath, StringComparison.OrdinalIgnoreCase))
                            {
                                _emuLibrary.Logger.Warn($"Installer path '{file.FullName}' doesn't start with expected source path '{srcPath}'. Skipping installer.");
                                continue;
                            }
                            var relativePath = file.FullName.Substring(srcPath.Length).TrimStart(Path.DirectorySeparatorChar);
                            
                            // Try to detect GOG installer by checking filename
                            bool isGogInstaller = file.Name.Contains("gog") || 
                                                 file.Name.Contains("setup_") || 
                                                 file.FullName.Contains("GOG") ||
                                                 folderName.Contains("GOG");
                            
                            // Try to extract store ID from filename or folder
                            string storeGameId = null;
                            string installerType = null;
                            
                            if (isGogInstaller)
                            {
                                installerType = "GOG";
                                
                                // GOG installers often contain an ID in the format setup_name_id
                                var filename = Path.GetFileNameWithoutExtension(file.Name);
                                if (!string.IsNullOrEmpty(filename) && filename.StartsWith("setup_"))
                                {
                                    var parts = filename.Split('_');
                                    if (parts != null && parts.Length >= 3)
                                    {
                                        storeGameId = parts[parts.Length - 1];
                                        // Verify it looks like a GOG ID (typically numeric)
                                        if (string.IsNullOrEmpty(storeGameId) || 
                                            !System.Text.RegularExpressions.Regex.IsMatch(storeGameId, @"^\d+$"))
                                        {
                                            storeGameId = null;
                                        }
                                    }
                                }
                            }
                            
                            var info = new PCInstallerGameInfo()
                            {
                                MappingId = mapping.MappingId,
                                SourcePath = relativePath,
                                InstallerFullPath = file.FullName,
                                InstallDirectory = null, // Will be set during installation
                                StoreGameId = storeGameId,
                                InstallerType = installerType
                            };

                            // Set proper platform based on installer type
                            string platformName = mapping.Platform?.Name;
                            if (string.IsNullOrEmpty(platformName))
                            {
                                platformName = "PC"; // Default fallback
                            }
                            
                            // Add store-specific platform if available
                            if (info.InstallerType == "GOG")
                            {
                                try
                                {
                                    // Try to set a more specific GOG platform if available
                                    var gogPlatform = _playniteAPI.Database.Platforms?
                                        .FirstOrDefault(p => p != null && 
                                                           (p.Name == "GOG" || 
                                                            (p.SpecificationId != null && p.SpecificationId == "pc_gog")));
                                    
                                    if (gogPlatform != null && !string.IsNullOrEmpty(gogPlatform.Name))
                                    {
                                        platformName = gogPlatform.Name;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _emuLibrary.Logger.Error($"Error getting GOG platform: {ex.Message}");
                                }
                            }
                            
                            var metadata = new GameMetadata()
                            {
                                Source = EmuLibrary.SourceName,
                                Name = gameName,
                                IsInstalled = false, // PC games start as uninstalled
                                GameId = info.AsGameId(),
                                Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(platformName) },
                                InstallSize = (ulong)new FileInfo(file.FullName).Length,
                                GameActions = new List<GameAction>() 
                                { 
                                    new GameAction()
                                    {
                                        Name = "Install Game",
                                        Type = GameActionType.URL,
                                        Path = "", // Will be updated after installation
                                        IsPlayAction = false
                                    }
                                }
                            };
                            
                            // Add metadata tags based on installer type
                            if (!string.IsNullOrEmpty(info.InstallerType))
                            {
                                metadata.Tags = new HashSet<MetadataProperty>() { 
                                    new MetadataNameProperty(info.InstallerType) 
                                };
                            }
                            
                            // Add metadata for EmuLibrary tracking
                            if (!string.IsNullOrEmpty(info.StoreGameId) && !string.IsNullOrEmpty(info.InstallerType))
                            {
                                metadata.Tags = metadata.Tags ?? new HashSet<MetadataProperty>();
                                metadata.Tags.Add(new MetadataNameProperty($"{info.InstallerType}:{info.StoreGameId}"));
                                _emuLibrary.Logger.Debug($"Added store metadata for {gameName}: {info.InstallerType} ID {info.StoreGameId}");
                            }
                            
                            sourcedGames.Add(metadata);
                        }
                        catch (Exception ex)
                        {
                            _emuLibrary.Logger.Error($"Error processing file {file.FullName}: {ex.Message}");
                        }
                    }
                }
                
                // Batch process all discovered games (already added to sourcedGames)
                _emuLibrary.Logger.Info($"Found {sourcedGames.Count} PC installer games (EXE, ISO, archives)");
            }
            catch (Exception ex)
            {
                _emuLibrary.Logger.Error($"Error scanning source directory {srcPath}: {ex.Message}");
            }
            
            // Return all discovered games outside the try/catch
            foreach (var game in sourcedGames)
            {
                yield return game;
            }
            #endregion
            
            #region Update installed games
            var installedGamesToReturn = new List<GameMetadata>();
            try
            {
                _emuLibrary.Logger.Info("Updating installed PC installer games");
                
                // Use BufferedUpdate for better performance
                using (_playniteAPI.Database.BufferedUpdate())
                {
                    // More efficient filtering upfront to reduce iteration
                    var installedGames = _playniteAPI.Database.Games
                        .Where(g => g.PluginId == EmuLibrary.PluginId && g.IsInstalled)
                        .Select(g => {
                            try {
                                var info = g.GetELGameInfo();
                                if (info.RomType == RomType.PCInstaller) {
                                    return (g, info as PCInstallerGameInfo);
                                }
                            } 
                            catch (Exception ex) {
                                _emuLibrary.Logger.Error($"Error getting game info for {g.Name}: {ex.Message}");
                            }
                            return (null, null);
                        })
                        .Where(pair => pair.Item1 != null);
                
                    foreach (var pair in installedGames)
                    {
                        var game = pair.Item1;
                        var gameInfo = pair.Item2;
                        if (args.CancelToken.IsCancellationRequested)
                        {
                            _emuLibrary.Logger.Info("Updating installed games cancelled");
                            yield break; // Will continue after the catch block
                        }
                        
                        GameMetadata gameMetadata = null;
                        try
                        {
                            if (!string.IsNullOrEmpty(gameInfo.InstallDirectory))
                            {
                                if (Directory.Exists(gameInfo.InstallDirectory))
                                {
                                    // Game is still installed, prepare it for returning later
                                    gameMetadata = new GameMetadata()
                                    {
                                        Source = EmuLibrary.SourceName,
                                        Name = game.Name,
                                        IsInstalled = true,
                                        GameId = gameInfo.AsGameId(),
                                        InstallDirectory = gameInfo.InstallDirectory,
                                        Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(mapping.Platform.Name) },
                                        GameActions = new List<GameAction>() 
                                        { 
                                            new GameAction()
                                            {
                                                Name = "Play",
                                                Type = GameActionType.File,
                                                Path = !string.IsNullOrEmpty(gameInfo.PrimaryExecutable) 
                                                    ? gameInfo.PrimaryExecutable 
                                                    : _playniteAPI.Database.Games.Get(game.Id)?.GameActions?.FirstOrDefault(a => a.IsPlayAction)?.Path 
                                                    ?? gameInfo.InstallDirectory,
                                                IsPlayAction = true
                                            }
                                        }
                                    };
                                }
                                else
                                {
                                    _emuLibrary.Logger.Warn($"Install directory no longer exists for game {game.Name}: {gameInfo.InstallDirectory}");
                                }
                            }
                            else
                            {
                                _emuLibrary.Logger.Warn($"Install directory is empty for installed game {game.Name}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _emuLibrary.Logger.Error($"Error processing installed game {game.Name}: {ex.Message}");
                        }
                        
                        // Metadata will be collected and returned outside the try/catch block
                        if (gameMetadata != null)
                        {
                            installedGamesToReturn.Add(gameMetadata);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _emuLibrary.Logger.Error($"Error updating installed games: {ex.Message}");
            }
            
            // Return all collected games after the try/catch block
            foreach (var game in installedGamesToReturn)
            {
                yield return game;
            }
            #endregion
        }

        public override bool TryGetGameInfoBaseFromLegacyGameId(Game game, EmulatorMapping mapping, out ELGameInfo gameInfo)
        {
            // PC installers are a new type, so there are no legacy game IDs to convert
            gameInfo = null;
            return false;
        }

        /// <summary>
        /// Attempts to get a cleaner game name from Playnite's metadata providers.
        /// This helps match release names to proper game titles.
        /// Rate-limited to prevent overwhelming metadata API servers.
        /// </summary>
        private string TryGetMetadataName(string normalizedName, EmulatedPlatform platform)
        {
            if (string.IsNullOrEmpty(normalizedName) || _playniteAPI == null)
            {
                return null;
            }

            // Apply rate limiting to avoid overwhelming metadata API servers (e.g., IGDB)
            lock (_metadataRateLimitLock)
            {
                var timeSinceLastCall = DateTime.Now - _lastMetadataCallTime;
                if (timeSinceLastCall.TotalMilliseconds < METADATA_API_DELAY_MS)
                {
                    var delayNeeded = METADATA_API_DELAY_MS - (int)timeSinceLastCall.TotalMilliseconds;
                    Thread.Sleep(delayNeeded);
                }
                _lastMetadataCallTime = DateTime.Now;
            }

            try
            {
                // Create a temporary game object for metadata lookup
                var tempGame = new Game
                {
                    Name = normalizedName,
                    PlatformId = platform?.Id
                };

                // Try to get metadata from available providers
                // Playnite's metadata providers can help clean up names
                var metadataProviders = _playniteAPI.Addons.Plugins
                    .Where(p => p is Playnite.SDK.Plugins.MetadataPlugin)
                    .Cast<Playnite.SDK.Plugins.MetadataPlugin>()
                    .ToList();

                if (metadataProviders.Count == 0)
                {
                    return null;
                }

                // Try the first available metadata provider (usually IGDB)
                foreach (var provider in metadataProviders)
                {
                    try
                    {
                        var metadataProvider = provider.GetMetadataProvider(
                            new Playnite.SDK.Plugins.MetadataRequestOptions
                            {
                                IsBackgroundDownload = true,
                                GameData = tempGame
                            });

                        if (metadataProvider != null)
                        {
                            // Try to get just the name field (lightweight operation)
                            var nameField = metadataProvider.GetName(
                                new Playnite.SDK.Plugins.GetMetadataFieldArgs
                                {
                                    GameData = tempGame,
                                    CancelToken = CancellationToken.None
                                });

                            if (!string.IsNullOrEmpty(nameField) && 
                                nameField.Length > 2 &&
                                !string.Equals(nameField, normalizedName, StringComparison.OrdinalIgnoreCase))
                            {
                                // Found a better name from metadata
                                return nameField;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Continue to next provider if this one fails
                        _emuLibrary.Logger.Debug($"Metadata provider {provider.Name} failed: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                // Don't throw, just return null if metadata lookup fails
                _emuLibrary.Logger.Debug($"Error in TryGetMetadataName: {ex.Message}");
            }

            return null;
        }

        public override IEnumerable<Game> GetUninstalledGamesMissingSourceFiles(CancellationToken ct)
        {
            _emuLibrary.Logger.Info("Checking for PC installer games with missing source files");
            
            try
            {
                // Use BufferedUpdate for better performance
                using (_playniteAPI.Database.BufferedUpdate())
                {
                    // First filter by plugin ID and installation status to reduce the collection size
                    var filteredGames = _playniteAPI.Database.Games
                        .Where(g => g.PluginId == EmuLibrary.PluginId && !g.IsInstalled)
                        .ToList();
                    
                    return filteredGames
                        .Where(g =>
                        {
                            // Check cancellation at each iteration
                            if (ct.IsCancellationRequested)
                                return false;

                            try
                            {
                                var info = g.GetELGameInfo();
                                if (info.RomType != RomType.PCInstaller)
                                    return false;

                                var pcInfo = info as PCInstallerGameInfo;
                                var sourceExists = File.Exists(pcInfo.SourceFullPath);

                                if (!sourceExists)
                                {
                                    _emuLibrary.Logger.Info($"Source file missing for game {g.Name}: {pcInfo.SourceFullPath}");
                                }

                                return !sourceExists;
                            }
                            catch (Exception ex)
                            {
                                _emuLibrary.Logger.Error($"Error checking source file for game {g.Name}: {ex.Message}");
                                return false;
                            }
                        });
                }
            }
            catch (Exception ex)
            {
                _emuLibrary.Logger.Error($"Error in GetUninstalledGamesMissingSourceFiles: {ex.Message}");
                return Enumerable.Empty<Game>();
            }
        }
    }
}