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
using System.IO.Abstractions;

namespace EmuLibrary.RomTypes.ISOInstaller
{
    internal class ISOInstallerScanner : RomTypeScanner
    {
        private readonly IPlayniteAPI _playniteAPI;
        private readonly IEmuLibrary _emuLibrary;
        private const int BATCH_SIZE = 100; // Process games in batches for better performance

        public override RomType RomType => RomType.ISOInstaller;
        public override Guid LegacyPluginId => EmuLibrary.PluginId;

        public ISOInstallerScanner(IEmuLibrary emuLibrary) : base(emuLibrary)
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

            // Define supported disc image extensions (both lowercase and uppercase for case insensitivity)
            // Only using .iso files as requested to avoid grabbing extracted content
            var discExtensions = new List<string> { 
                "iso", "ISO"
            };
            
            SafeFileEnumerator fileEnumerator;
            var gameMetadataBatch = new List<GameMetadata>();

            #region Import discovered disc images
            var sourcedGames = new List<GameMetadata>();
            try
            {
                fileEnumerator = new SafeFileEnumerator(srcPath, "*.*", SearchOption.AllDirectories);
                _emuLibrary.Logger.Info($"Scanning for disc images in {srcPath}");

                // Create a dictionary to cache normalized folder names for performance
                var normalizedNameCache = new Dictionary<string, string>();

                // Do a direct file search diagnostic to verify file presence
                try {
                    var directSearch = Directory.GetFiles(srcPath, "*.*", SearchOption.AllDirectories)
                        .Where(f => discExtensions.Contains(Path.GetExtension(f).TrimStart('.').ToLowerInvariant()))
                        .ToList();
                    
                    _emuLibrary.Logger.Info($"Direct search found {directSearch.Count} disc image files in {srcPath}");
                    
                    if (directSearch.Count > 0) {
                        _emuLibrary.Logger.Info($"Examples: {string.Join(", ", directSearch.Take(5).Select(Path.GetFileName))}");
                    }
                }
                catch (Exception ex) {
                    _emuLibrary.Logger.Error($"Error in direct file search: {ex.Message}");
                }

                // CRITICAL FIX: First detect directories that are likely extracted game content
                // These should be handled by PCInstaller instead of ISOInstaller
                Dictionary<string, List<string>> folderContents = new Dictionary<string, List<string>>();
                HashSet<string> extractedContentFolders = new HashSet<string>();
                
                // List of common file patterns found in extracted disc image content
                var extractedContentPatterns = new List<string> {
                    "setup.exe", "install.exe", "launcher.exe", "game.exe", "bin", "data", "INSTALL", "DATA", "redist"
                };

                // Pre-scan to identify extracted content folders (more likely to contain PC installers)
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
                        }
                        catch (Exception ex)
                        {
                            _emuLibrary.Logger.Error($"Error checking directory {dir}: {ex.Message}");
                        }
                    }
                    
                    _emuLibrary.Logger.Info($"Pre-scan found {extractedContentFolders.Count} potential extracted content folders out of {directories.Length} total directories");
                }
                catch (Exception ex)
                {
                    _emuLibrary.Logger.Error($"Error during pre-scan: {ex.Message}");
                }

                // Collect all ISO files first
                var isoFiles = new List<string>();
                
                foreach (var file in fileEnumerator)
                {
                    if (args.CancelToken.IsCancellationRequested)
                    {
                        _emuLibrary.Logger.Info("Scanning cancelled during file enumeration");
                        yield break;
                    }

                    // Check the file extension directly instead of iterating through each extension
                    string fileExtension = file.Extension?.TrimStart('.')?.ToLowerInvariant();
                    
                    _emuLibrary.Logger.Debug($"Checking if file {file.Name} has extension '{fileExtension}'");
                    
                    // Check if this file has a supported extension
                    if (!string.IsNullOrEmpty(fileExtension) && discExtensions.Contains(fileExtension.ToLowerInvariant()))
                    {
                        try
                        {
                            // Skip if this file is in an extracted content folder
                            var parentDir = Path.GetDirectoryName(file.FullName);
                            if (extractedContentFolders.Contains(parentDir))
                            {
                                _emuLibrary.Logger.Debug($"Skipping disc image in extracted content folder: {file.FullName}");
                                continue;
                            }
                            
                            isoFiles.Add(file.FullName);
                        }
                        catch (Exception ex)
                        {
                            _emuLibrary.Logger.Error($"Error processing disc image file {file.FullName}: {ex.Message}");
                        }
                    }
                }
                
                _emuLibrary.Logger.Info($"Found {isoFiles.Count} valid disc image files after filtering");
                
                if (isoFiles.Count == 0)
                {
                    _emuLibrary.Logger.Warn($"No valid disc image files found in {srcPath}");
                    
                    // Additional diagnostic to help troubleshoot when no files are found
                    try {
                        var allFiles = Directory.GetFiles(srcPath, "*.*", SearchOption.TopDirectoryOnly);
                        _emuLibrary.Logger.Debug($"Files in top directory ({allFiles.Length} total): {string.Join(", ", allFiles.Take(10).Select(Path.GetFileName))}");
                        
                        var allDirs = Directory.GetDirectories(srcPath, "*", SearchOption.TopDirectoryOnly);
                        _emuLibrary.Logger.Debug($"Directories in top level ({allDirs.Length} total): {string.Join(", ", allDirs.Take(10).Select(Path.GetFileName))}");
                        
                        if (allDirs.Length > 0) {
                            var firstDirFiles = Directory.GetFiles(allDirs[0], "*.*", SearchOption.TopDirectoryOnly);
                            _emuLibrary.Logger.Debug($"Files in first directory {Path.GetFileName(allDirs[0])} ({firstDirFiles.Length} total): {string.Join(", ", firstDirFiles.Take(10).Select(Path.GetFileName))}");
                        }
                    }
                    catch (Exception ex) {
                        _emuLibrary.Logger.Error($"Error during diagnostic logging: {ex.Message}");
                    }
                    
                    yield break;
                }

                // Group ISO files by directory to avoid duplicate entries
                var isoFilesByFolder = isoFiles
                    .GroupBy(f => Path.GetDirectoryName(f))
                    .ToDictionary(g => g.Key, g => g.ToList());
                
                _emuLibrary.Logger.Info($"Grouped disc images into {isoFilesByFolder.Count} directories");

                // Process each folder of ISO files
                foreach (var folder in isoFilesByFolder.Keys)
                {
                    if (args.CancelToken.IsCancellationRequested)
                    {
                        _emuLibrary.Logger.Info("Processing cancelled");
                        yield break;
                    }
                    
                    // Before processing, check if this folder has characteristics of an extracted game
                    bool skipAsExtractedContent = false;
                    try
                    {
                        // Count files and folders in this directory
                        int fileCount = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly).Length;
                        int folderCount = Directory.GetDirectories(folder, "*", SearchOption.TopDirectoryOnly).Length;
                        
                        // If too many files or folders, likely an extracted game
                        if (fileCount > 15 || folderCount > 5)
                        {
                            _emuLibrary.Logger.Debug($"Skipping folder with too many files/folders: {folder} (files: {fileCount}, folders: {folderCount})");
                            skipAsExtractedContent = true;
                        }
                        
                        // Check for common system files that indicate this is an extracted game
                        var folderName = Path.GetFileName(folder).ToLowerInvariant();
                        if (folderName == "system" || folderName == "windows" || folderName == "program files" || 
                            folderName == "users" || folderName == "games" || folderName == "desktop" || 
                            folderName == "documents" || folderName == "downloads")
                        {
                            _emuLibrary.Logger.Debug($"Skipping system folder: {folder}");
                            skipAsExtractedContent = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _emuLibrary.Logger.Error($"Error checking folder characteristics for {folder}: {ex.Message}");
                    }
                    
                    if (skipAsExtractedContent)
                    {
                        continue;
                    }

                    try
                    {
                        // Get all ISO files in this folder
                        var folderIsoFiles = isoFilesByFolder[folder];
                        
                        // Try to determine the game name from the folder
                        var folderName = Path.GetFileName(folder);
                        
                        // Use cached normalized name if available
                        string gameName;
                        if (!normalizedNameCache.TryGetValue(folderName, out gameName))
                        {
                            gameName = StringExtensions.NormalizeGameName(folderName);
                            normalizedNameCache[folderName] = gameName;
                        }
                        
                        if (string.IsNullOrEmpty(gameName))
                        {
                            // If folder name doesn't work, try the first ISO file name
                            gameName = Path.GetFileNameWithoutExtension(Path.GetFileName(folderIsoFiles.First()));
                            gameName = StringExtensions.NormalizeGameName(gameName);
                        }
                        
                        if (string.IsNullOrEmpty(gameName))
                        {
                            _emuLibrary.Logger.Warn($"Could not determine game name for folder {folder}");
                            continue;
                        }
                        
                        // Sort the ISO files to find the primary one
                        // Prioritize by: first disc, m3u file, smallest file (likely the main ISO not an update)
                        var primaryIsoFile = folderIsoFiles
                            .OrderBy(f => !Path.GetFileName(f).ToLowerInvariant().Contains("disc 1"))
                            .ThenBy(f => !Path.GetExtension(f).ToLowerInvariant().Equals(".m3u"))
                            .ThenBy(f => new FileInfo(f).Length)
                            .First();
                        
                        // Get relative path from source directory
                        var relativePath = primaryIsoFile.Substring(srcPath.Length).TrimStart(Path.DirectorySeparatorChar);
                        
                        // Create game info
                        var info = new ISOInstallerGameInfo()
                        {
                            MappingId = mapping.MappingId,
                            SourcePath = relativePath,
                            SourceBasePath = srcPath,
                            InstallDirectory = null, // Will be set during installation
                            ISOFiles = folderIsoFiles.Select(f => f.Substring(srcPath.Length).TrimStart(Path.DirectorySeparatorChar)).ToList()
                        };

                        var metadata = new GameMetadata()
                        {
                            Source = EmuLibrary.SourceName,
                            Name = gameName,
                            IsInstalled = false, // ISO installer games start as uninstalled
                            GameId = info.AsGameId(),
                            Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(mapping.Platform.Name) },
                            InstallSize = folderIsoFiles.Select(f => (ulong)new FileInfo(f).Length).Aggregate((a, b) => a + b),
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
                        
                        // Add ISO type tag
                        metadata.Tags = new HashSet<MetadataProperty>() { 
                            new MetadataNameProperty("ISO Installer") 
                        };
                        
                        gameMetadataBatch.Add(metadata);
                    }
                    catch (Exception ex)
                    {
                        _emuLibrary.Logger.Error($"Error processing folder {folder}: {ex.Message}");
                    }
                }

                // Add remaining games to the collection
                if (gameMetadataBatch.Count > 0)
                {
                    _emuLibrary.Logger.Debug($"Adding batch of {gameMetadataBatch.Count} ISO installer games");
                    sourcedGames.AddRange(gameMetadataBatch);
                    gameMetadataBatch.Clear();
                }
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
                _emuLibrary.Logger.Info("Updating installed ISO installer games");
                
                // Use BufferedUpdate for better performance
                using (_playniteAPI.Database.BufferedUpdate())
                {
                    // More efficient filtering upfront to reduce iteration
                    var installedGames = _playniteAPI.Database.Games
                        .Where(g => g.PluginId == EmuLibrary.PluginId && g.IsInstalled)
                        .Select(g => {
                            try {
                                var info = g.GetELGameInfo();
                                if (info.RomType == RomType.ISOInstaller) {
                                    return (g, info as ISOInstallerGameInfo);
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
                            yield break;
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
            // ISO installers are a new type, so there are no legacy game IDs to convert
            gameInfo = null;
            return false;
        }

        public override IEnumerable<Game> GetUninstalledGamesMissingSourceFiles(CancellationToken ct)
        {
            _emuLibrary.Logger.Info("Checking for ISO installer games with missing source files");
            
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
                        .TakeWhile(g => !ct.IsCancellationRequested)
                        .Where(g =>
                        {
                            try
                            {
                                var info = g.GetELGameInfo();
                                if (info.RomType != RomType.ISOInstaller)
                                    return false;

                                var isoInfo = info as ISOInstallerGameInfo;
                                var sourceExists = File.Exists(isoInfo.SourceFullPath);
                                
                                if (!sourceExists)
                                {
                                    _emuLibrary.Logger.Info($"Source file missing for game {g.Name}: {isoInfo.SourceFullPath}");
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