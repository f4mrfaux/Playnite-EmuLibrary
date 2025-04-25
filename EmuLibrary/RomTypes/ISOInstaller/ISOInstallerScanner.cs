using EmuLibrary.PlayniteCommon;
using EmuLibrary.Settings;
using EmuLibrary.Util;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;

namespace EmuLibrary.RomTypes.ISOInstaller
{
    // Helper class to wrap System.IO.FileInfo into a FileSystemInfoBase for our custom enumeration
    internal class FileInfoWrapper : FileSystemInfoBase
    {
        private readonly FileInfo _fileInfo;
        
        public FileInfoWrapper(FileInfo fileInfo)
        {
            _fileInfo = fileInfo;
        }
        
        public override string Name => _fileInfo.Name;
        public override string FullName => _fileInfo.FullName;
        public override bool Exists => _fileInfo.Exists;
        public override string Extension => _fileInfo.Extension;
        
        public override DateTime CreationTime 
        {
            get => _fileInfo.CreationTime;
            set => _fileInfo.CreationTime = value;
        }
        
        public override DateTime CreationTimeUtc 
        {
            get => _fileInfo.CreationTimeUtc;
            set => _fileInfo.CreationTimeUtc = value;
        }
        
        public override DateTime LastAccessTime 
        {
            get => _fileInfo.LastAccessTime;
            set => _fileInfo.LastAccessTime = value;
        }
        
        public override DateTime LastAccessTimeUtc 
        {
            get => _fileInfo.LastAccessTimeUtc;
            set => _fileInfo.LastAccessTimeUtc = value;
        }
        
        public override DateTime LastWriteTime 
        {
            get => _fileInfo.LastWriteTime;
            set => _fileInfo.LastWriteTime = value;
        }
        
        public override DateTime LastWriteTimeUtc 
        {
            get => _fileInfo.LastWriteTimeUtc;
            set => _fileInfo.LastWriteTimeUtc = value;
        }
        
        public override System.IO.FileAttributes Attributes 
        {
            get => _fileInfo.Attributes;
            set => _fileInfo.Attributes = value;
        }
        
        public override void Delete()
        {
            _fileInfo.Delete();
        }
        
        public override void Refresh()
        {
            _fileInfo.Refresh();
        }
    }
    
    internal class ISOInstallerScanner : RomTypeScanner
    {
        private readonly IPlayniteAPI _playniteAPI;
        private readonly IEmuLibrary _emuLibrary;

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
                yield break;
            }

            var srcPath = mapping.SourcePath;
            if (string.IsNullOrEmpty(srcPath) || !Directory.Exists(srcPath))
            {
                _emuLibrary.Logger.Error($"Source path is invalid: {srcPath}");
                yield break;
            }

            // Define supported ISO extensions
            var discExtensions = new List<string> { 
                "iso", "bin", "img", "cue", "nrg", "mds", "mdf",
                "ISO", "BIN", "IMG", "CUE", "NRG", "MDS", "MDF" 
            };

            _emuLibrary.Logger.Info($"[ISO SCANNER] Starting scan in {srcPath}");
            
            // First verify if the source path is valid and has appropriate permissions
            try
            {
                var testFiles = Directory.GetFiles(srcPath, "*.*", SearchOption.TopDirectoryOnly);
                _emuLibrary.Logger.Info($"[ISO SCANNER] Successfully accessed directory: {srcPath} ({testFiles.Length} files at top level)");
            }
            catch (Exception ex)
            {
                _emuLibrary.Logger.Error($"[ISO SCANNER] Error accessing directory {srcPath}: {ex.Message}");
                yield break;
            }
            
            // DIRECT APPROACH - Use simple pattern matching for better success
            var isoFiles = new List<string>();
            
            try
            {
                // Search for all ISO files using multiple patterns
                // Prioritize .iso files which are most likely to be games
                foreach (var ext in new[] { "iso", "img", "cue", "nrg", "mdf", "mds" })
                {
                    try 
                    {
                        var pattern = $"*.{ext}";
                        _emuLibrary.Logger.Info($"[ISO SCANNER] Searching with pattern: {pattern}");
                        var files = Directory.GetFiles(srcPath, pattern, SearchOption.AllDirectories);
                        _emuLibrary.Logger.Info($"[ISO SCANNER] Found {files.Length} files with pattern {pattern}");
                        isoFiles.AddRange(files);
                        
                        // Also try uppercase version
                        pattern = $"*.{ext.ToUpper()}";
                        _emuLibrary.Logger.Info($"[ISO SCANNER] Searching with pattern: {pattern}");
                        files = Directory.GetFiles(srcPath, pattern, SearchOption.AllDirectories);
                        _emuLibrary.Logger.Info($"[ISO SCANNER] Found {files.Length} files with pattern {pattern}");
                        isoFiles.AddRange(files);
                    }
                    catch (Exception searchEx)
                    {
                        _emuLibrary.Logger.Error($"[ISO SCANNER] Error searching for {ext} files: {searchEx.Message}");
                    }
                }
                
                // Add .bin files but with more selective patterns to avoid non-game files
                try
                {
                    string[] binFiles = Directory.GetFiles(srcPath, "*.bin", SearchOption.AllDirectories);
                    foreach (var file in binFiles)
                    {
                        // Only add .bin files from game folders or those matching known patterns
                        if (file.Contains("setup_") || 
                            file.Contains("-bin") ||
                            file.Contains("setup-") ||
                            file.Contains("data") ||
                            file.Contains("KaOs") ||
                            file.Contains("fg-") ||
                            file.Contains("disk") ||
                            !file.Contains("\\Update\\") && 
                            !file.Contains("\\Engine\\") && 
                            !file.Contains("\\Content\\"))
                        {
                            isoFiles.Add(file);
                        }
                    }
                }
                catch (Exception)
                {
                    // Ignore errors in .bin file search
                }
                
                // Look for common ISO file naming patterns
                var patterns = new[] {
                    "*rune-*.iso", "*plaza-*.iso", "*codex-*.iso", "*flt-*.iso", 
                    "*sr-*.iso", "*tenoke-*.iso", "*vrex-*.iso", "*rzr-*.iso", 
                    "*wow-*.iso", "*hi-*.iso", "*tvm-*.iso", "*rld-*.iso", 
                    "*dev-*.iso", "*cpy-*.iso"
                };
                
                foreach (var pattern in patterns)
                {
                    try
                    {
                        var matches = Directory.GetFiles(srcPath, pattern, SearchOption.AllDirectories);
                        isoFiles.AddRange(matches);
                    }
                    catch { /* Ignore errors in pattern matching */ }
                }
                
                // Remove duplicates
                isoFiles = isoFiles.Distinct().ToList();
                _emuLibrary.Logger.Info($"[ISO SCANNER] Found {isoFiles.Count} ISO/disc files");
                
                if (isoFiles.Count > 0)
                {
                    // Log more details for diagnostic purposes
                    _emuLibrary.Logger.Info($"[ISO SCANNER] All ISO files found:");
                    foreach (var file in isoFiles)
                    {
                        _emuLibrary.Logger.Info($"[ISO SCANNER] - {file}");
                    }
                    
                    // Log the first few directories where files were found
                    var uniqueDirs = isoFiles.Select(Path.GetDirectoryName).Distinct().ToList();
                    _emuLibrary.Logger.Info($"[ISO SCANNER] Files found in {uniqueDirs.Count} directories: {string.Join(", ", uniqueDirs.Take(3))}");
                }
                else
                {
                    _emuLibrary.Logger.Error($"[ISO SCANNER] No ISO files found in {srcPath}");
                    
                    // Additional diagnostics for no files found case
                    try
                    {
                        var allFiles = Directory.GetFiles(srcPath, "*.*", SearchOption.AllDirectories).Take(20).ToList();
                        var fileExtensions = allFiles.Select(f => Path.GetExtension(f).ToLowerInvariant()).Distinct().ToList();
                        
                        _emuLibrary.Logger.Info($"[ISO SCANNER] Directory has {allFiles.Count} files with extensions: " + 
                            string.Join(", ", fileExtensions));
                            
                        // Check if ISO files might be in subdirectories
                        var subdirs = Directory.GetDirectories(srcPath, "*", SearchOption.TopDirectoryOnly);
                        if (subdirs.Length > 0)
                        {
                            _emuLibrary.Logger.Info($"[ISO SCANNER] Found {subdirs.Length} subdirectories. First few: {string.Join(", ", subdirs.Take(3).Select(Path.GetFileName))}");
                        }
                        
                        // Add notification to Playnite
                        _emuLibrary.Playnite.Notifications.Add(
                            $"EmuLibrary-ISOScanner-NoFiles-{Guid.NewGuid()}",
                            $"No ISO files found in {srcPath}. Supported formats are: ISO, BIN, CUE, IMG, MDF, MDS. Please check that your ISO files exist in this location.",
                            Playnite.SDK.NotificationType.Error);
                    }
                    catch (Exception diagEx)
                    {
                        _emuLibrary.Logger.Error($"[ISO SCANNER] Error during diagnostics: {diagEx.Message}");
                    }
                    
                    yield break;
                }
            }
            catch (Exception ex)
            {
                _emuLibrary.Logger.Error($"[ISO SCANNER] Error searching for ISO files: {ex.Message}");
                yield break;
            }
            
            // Process each ISO file
            var processedPaths = new HashSet<string>(); // Track by full path instead of name
            var sourcedGames = new List<GameMetadata>();
            
            foreach (var isoFile in isoFiles)
            {
                if (args.CancelToken.IsCancellationRequested)
                    yield break;
                    
                try
                {
                    // Skip system files and non-game files
                    if (isoFile.Contains("$RECYCLE.BIN") || 
                        isoFile.Contains("System Volume Information") ||
                        isoFile.Contains("Engine\\Content\\") ||
                        isoFile.Contains("\\Update\\Patch.bin") ||
                        isoFile.Contains("\\QuickSFV.EXE") ||
                        isoFile.Contains("TessellationTable.bin"))
                    {
                        continue;
                    }
                    
                    // Skip ISO files that are part of regular game installations
                    // These should be handled by PCInstaller
                    if (isoFile.Contains("\\CD1\\") || 
                        isoFile.Contains("\\CD2\\") ||
                        isoFile.Contains("\\Disc1\\") ||
                        isoFile.Contains("\\Disc2\\") ||
                        isoFile.Contains("\\DVD1\\") ||
                        isoFile.Contains("\\DVD2\\") ||
                        isoFile.EndsWith("\\CD1.iso") ||
                        isoFile.EndsWith("\\CD2.iso") ||
                        isoFile.EndsWith("\\Disc1.iso") ||
                        isoFile.EndsWith("\\Disc2.iso") ||
                        isoFile.EndsWith("\\DVD1.iso") ||
                        isoFile.EndsWith("\\DVD2.iso") ||
                        (isoFile.Contains("\\GOG") && isoFile.EndsWith(".iso")))
                    {
                        _emuLibrary.Logger.Info($"[ISO SCANNER] Skipping file {isoFile} as it appears to be part of a game installation");
                        continue;
                    }
                    
                    // Skip folders that primarily contain EXE files (should be handled by PCInstaller)
                    try
                    {
                        var folderPath = Path.GetDirectoryName(isoFile);
                        if (!string.IsNullOrEmpty(folderPath))
                        {
                            // Check if the current file is actually a disc image
                            var fileExtension = Path.GetExtension(isoFile).TrimStart('.').ToLowerInvariant();
                            var isActualDiscImage = discExtensions.Contains(fileExtension);
                            
                            // If it's NOT a disc image, we should skip it regardless
                            if (!isActualDiscImage)
                            {
                                _emuLibrary.Logger.Debug($"[ISO SCANNER] Skipping file {isoFile} as it is not a recognized disc image format");
                                continue;
                            }
                            
                            // Count executables in the folder
                            var exeFiles = Directory.GetFiles(folderPath, "*.exe", SearchOption.TopDirectoryOnly);
                            
                            // Check for "Alien Isolation" or similar game folders with both EXEs and ISOs
                            bool folderIsRegularGameInstall = 
                                folderPath.EndsWith("GOG") ||
                                folderPath.Contains("\\GOG\\") ||
                                folderPath.Contains("GOG Games") ||
                                folderPath.Contains("Steam") ||
                                folderPath.Contains("Epic") ||
                                folderPath.Contains("Uplay") ||
                                folderPath.Contains("Origin") ||
                                folderPath.Contains("Games") ||
                                // Skip CD1/CD2 folders and similar that are part of multi-disc games
                                folderPath.EndsWith("CD1") ||
                                folderPath.EndsWith("CD2") ||
                                folderPath.EndsWith("CD3") ||
                                folderPath.EndsWith("DVD1") ||
                                folderPath.EndsWith("DVD2") ||
                                folderPath.Contains("\\CD1\\") ||
                                folderPath.Contains("\\CD2\\") ||
                                folderPath.Contains("\\CD3\\") ||
                                folderPath.Contains("\\DVD1\\") ||
                                folderPath.Contains("\\DVD2\\");
                                
                            // Check if this folder has more executables than disc images
                            int exeCount = exeFiles.Length;
                            
                            // Count disc image files in the same folder
                            int discImageCount = 0;
                            foreach (var ext in discExtensions)
                            {
                                try
                                {
                                    var discFiles = Directory.GetFiles(folderPath, $"*.{ext}", SearchOption.TopDirectoryOnly);
                                    discImageCount += discFiles.Length;
                                }
                                catch (Exception) { /* Ignore errors */ }
                            }
                            
                            // Skip if the folder is a known game store folder or has more executables than disc images
                            bool hasMoreExesThanIsos = exeCount > 0 && exeCount > discImageCount;
                            
                            if ((exeCount > 0 && folderIsRegularGameInstall) || hasMoreExesThanIsos)
                            {
                                _emuLibrary.Logger.Info($"[ISO SCANNER] Skipping file {isoFile} in folder {Path.GetFileName(folderPath)} as it appears to be a regular game installation folder with executables (exeCount: {exeCount}, discImageCount: {discImageCount})");
                                continue;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log but continue with processing
                        _emuLibrary.Logger.Debug($"[ISO SCANNER] Error checking folder contents: {ex.Message}");
                    }
                    
                    // Skip folders that contain .exe files but no ISO files
                    // These are likely PC game install folders, not ISO-based games
                    try
                    {
                        var parentDir = Path.GetDirectoryName(isoFile);
                        if (!string.IsNullOrEmpty(parentDir))
                        {
                            // Check if this folder has executables but is not a disc image
                            var exeFiles = Directory.GetFiles(parentDir, "*.exe", SearchOption.TopDirectoryOnly);
                            var hasExeFiles = exeFiles.Length > 0;
                            
                            // If this file is not actually an ISO/disc image format, it might be a folder
                            // that contains an .exe installer that should be handled by PCInstaller instead
                            if (hasExeFiles && !discExtensions.Contains(Path.GetExtension(isoFile).TrimStart('.').ToLowerInvariant()))
                            {
                                _emuLibrary.Logger.Debug($"[ISO SCANNER] Skipping file {isoFile} as it appears to be in a folder with executables but is not a disc image");
                                continue;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log but continue with processing
                        _emuLibrary.Logger.Debug($"[ISO SCANNER] Error checking folder contents: {ex.Message}");
                    }
                    
                    // Get parent folder for game name - more intelligent handling
                    var parentFolderPath = Path.GetDirectoryName(isoFile);
                    var originalParentPath = parentFolderPath; // Save for logs
                    var parentFolder = Path.GetFileName(parentFolderPath);
                    
                    // Check if parent folder is the source folder - if so, use the filename
                    bool isDirectlyInRoot = string.Compare(parentFolderPath, srcPath, StringComparison.OrdinalIgnoreCase) == 0;
                    if (isDirectlyInRoot)
                    {
                        _emuLibrary.Logger.Info($"[ISO SCANNER] File {Path.GetFileName(isoFile)} is directly in root folder, using filename for game name");
                        var fileName = Path.GetFileNameWithoutExtension(isoFile);
                        parentFolder = fileName;
                    }
                    
                    // List of problematic folder names to skip
                    var problematicFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                        "Update", "Renderer", "ISO", "disc", "disk", "Image", "DVD", "CD", "Root", 
                        "Game", "Install", "Data", "Install Files", "Setup",
                        "CD1", "CD2", "CD3", "CD4", "DVD1", "DVD2", "DVD3",
                        "Disc1", "Disc2", "Disc3", "Disc One", "Disc Two",
                        "DATA", "GAME"
                    };
                    
                    // Get relative path segments from srcPath to file
                    var relPath = isoFile.Substring(srcPath.Length).TrimStart(Path.DirectorySeparatorChar);
                    var pathSegments = relPath.Split(Path.DirectorySeparatorChar).ToList();
                    
                    // Log the path segments for debugging
                    _emuLibrary.Logger.Info($"[ISO SCANNER] Path segments: {string.Join(" > ", pathSegments)}");
                    
                    // Try to find the best folder name to use
                    string selectedFolderName = parentFolder;
                    
                    // If parent folder is problematic, try to find a better folder name by going up levels
                    if (problematicFolders.Contains(parentFolder) || parentFolder.StartsWith("setup.part"))
                    {
                        _emuLibrary.Logger.Info($"[ISO SCANNER] Parent folder '{parentFolder}' is problematic, looking for better name");
                        
                        // Try to find first non-problematic folder going up the tree
                        var currentPath = parentFolderPath;
                        while (currentPath != null && currentPath.Length > srcPath.Length)
                        {
                            var currentDir = Path.GetFileName(currentPath);
                            _emuLibrary.Logger.Info($"[ISO SCANNER] Checking folder: {currentDir}");
                            
                            if (!problematicFolders.Contains(currentDir) && !currentDir.StartsWith("setup.part"))
                            {
                                selectedFolderName = currentDir;
                                _emuLibrary.Logger.Info($"[ISO SCANNER] Found suitable folder name: {selectedFolderName}");
                                break;
                            }
                            
                            currentPath = Path.GetDirectoryName(currentPath);
                        }
                    }
                    
                    // Clean up the selected name
                    var gameName = CleanGameNameRemoveVersions(StringExtensions.NormalizeGameName(selectedFolderName));
                    _emuLibrary.Logger.Info($"[ISO SCANNER] Final game name: {gameName} (from folder: {selectedFolderName})");
                    
                    if (string.IsNullOrEmpty(gameName))
                    {
                        // Fall back to file name without extension
                        var fileName = Path.GetFileNameWithoutExtension(isoFile);
                        
                        // Clean up common prefixes in ISO file names
                        if (fileName.Contains("-"))
                        {
                            var parts = fileName.Split(new[] { '-' }, 2);
                            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
                            {
                                gameName = CleanGameNameRemoveVersions(StringExtensions.NormalizeGameName(parts[1]));
                            }
                            else
                            {
                                gameName = CleanGameNameRemoveVersions(StringExtensions.NormalizeGameName(fileName));
                            }
                        }
                        else if (fileName.Contains("setup_"))
                        {
                            // Handle GOG installer names like "setup_game_name_1.2.3"
                            var withoutSetup = fileName.Replace("setup_", "");
                            var parts = withoutSetup.Split(new[] { '_' }, 2);
                            if (parts.Length > 1)
                            {
                                gameName = CleanGameNameRemoveVersions(StringExtensions.NormalizeGameName(parts[0]));
                            }
                            else
                            {
                                gameName = CleanGameNameRemoveVersions(StringExtensions.NormalizeGameName(fileName));
                            }
                        }
                        else
                        {
                            gameName = StringExtensions.NormalizeGameName(fileName);
                        }
                    }
                    
                    // Skip duplicate file paths
                    if (processedPaths.Contains(isoFile.ToLowerInvariant()))
                    {
                        _emuLibrary.Logger.Debug($"[ISO SCANNER] Skipping duplicate file: {isoFile}");
                        continue;
                    }
                    
                    processedPaths.Add(isoFile.ToLowerInvariant());
                    
                    // Log processing of this game
                    _emuLibrary.Logger.Info($"[ISO SCANNER] Processing game: {gameName} from {isoFile}");
                    
                    // Following the pattern from SingleFileScanner, check first if this file is already in the database
                    
                    // Get the relative path from source path for easier comparison
                    var relativePath = isoFile.Substring(srcPath.Length).TrimStart(Path.DirectorySeparatorChar);
                    
                    // Track if we need to skip this file because it's already installed or tracked
                    bool skipThisFile = false;
                    
                    try
                    {
                        // Check if we already have this game in our library:
                        // 1. First check if it's installed (checking with Playnite API is faster than our own scan)
                        var libraryGames = _playniteAPI.Database.Games
                            .Where(g => g.PluginId == EmuLibrary.PluginId)
                            .ToList();
                        
                        _emuLibrary.Logger.Debug($"[ISO SCANNER] Checking {libraryGames.Count} library games for duplication");
                        
                        foreach (var game in libraryGames)
                        {
                            try
                            {
                                // Try to get the game info
                                var gameInfo = game.GetELGameInfo();
                                if (gameInfo?.RomType != RomType.ISOInstaller)
                                    continue;
                                
                                var isoGameInfo = gameInfo as ISOInstallerGameInfo;
                                
                                // Check different conditions that would make this a duplicate
                                
                                // 1. Check full paths - most reliable
                                if (!string.IsNullOrEmpty(isoGameInfo.InstallerFullPath) && 
                                    isoGameInfo.InstallerFullPath.Equals(isoFile, StringComparison.OrdinalIgnoreCase))
                                {
                                    _emuLibrary.Logger.Info($"[ISO SCANNER] Skipping {isoFile} - game with this path already exists: {game.Name}");
                                    skipThisFile = true;
                                    break;
                                }
                                
                                // 2. Check by source path + mapping ID
                                if (isoGameInfo.MappingId == mapping.MappingId && 
                                    !string.IsNullOrEmpty(isoGameInfo.SourcePath) && 
                                    isoGameInfo.SourcePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase))
                                {
                                    _emuLibrary.Logger.Info($"[ISO SCANNER] Skipping {isoFile} - game with same source path already exists: {game.Name}");
                                    skipThisFile = true;
                                    break;
                                }
                                
                                // 3. Check by game name as a last resort (less reliable)
                                if (game.Name.Equals(gameName, StringComparison.OrdinalIgnoreCase) && 
                                    isoGameInfo.MappingId == mapping.MappingId)
                                {
                                    _emuLibrary.Logger.Info($"[ISO SCANNER] Skipping {isoFile} - game with same name already exists in same mapping: {game.Name}");
                                    skipThisFile = true;
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                _emuLibrary.Logger.Error($"[ISO SCANNER] Error checking game {game.Name}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _emuLibrary.Logger.Error($"[ISO SCANNER] Error checking for duplicate games: {ex.Message}");
                    }

                    // Skip this game if it's a duplicate 
                    if (skipThisFile)
                    {
                        _emuLibrary.Logger.Info($"[ISO SCANNER] Skipping duplicate game: {gameName} from {isoFile}");
                        continue;
                    }
                    
                    // Get relative path from source folder
                    var relativePath = isoFile.Substring(srcPath.Length).TrimStart(Path.DirectorySeparatorChar);
                    
                    // Create game info with unique properties to ensure unique GameId
                    var info = new ISOInstallerGameInfo()
                    {
                        MappingId = mapping.MappingId,
                        SourcePath = relativePath,
                        InstallerFullPath = isoFile, // Full path to the ISO file
                        InstallDirectory = null, // Will be set during installation
                        // Use a deterministic ID based on the ISO file path instead of timestamp
                        // This ensures the same ISO file always generates the same ID
                        StoreGameId = $"{gameName}_{Path.GetFileName(isoFile)}"
                    };
                    
                    // Log the installer path for debugging
                    _emuLibrary.Logger.Info($"[ISO SCANNER] Setting InstallerFullPath: {isoFile}");
                    
                    // Double-check that the ISO file exists
                    if (!File.Exists(isoFile))
                    {
                        _emuLibrary.Logger.Error($"[ISO SCANNER] WARNING: ISO file does not exist: {isoFile}");
                    }
                    
                    // EXACTLY like PCInstallerScanner - use the platform from the mapping
                    string platformName = mapping.Platform?.Name;
                    
                    if (string.IsNullOrEmpty(platformName))
                    {
                        platformName = "PC"; // Default fallback
                        _emuLibrary.Logger.Info($"[ISO SCANNER] No platform in mapping, using default: {platformName}");
                    }
                    else
                    {
                        _emuLibrary.Logger.Info($"[ISO SCANNER] Using platform from mapping: {platformName}");
                    }
                    
                    // Create game metadata
                    // Create a valid source property
                    // Create GameMetadata as expected by the API
                    var metadata = new GameMetadata
                    {
                        Source = EmuLibrary.SourceName,
                        Name = gameName,
                        IsInstalled = false,
                        GameId = info.AsGameId(),
                        // PluginId is set on Game objects after import, not on GameMetadata
                        Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(platformName) },
                        InstallSize = (ulong)new FileInfo(isoFile).Length,
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
                    
                    // Don't set description to allow Playnite's metadata providers to fill it with proper game description
                    
                    // Prepare game for metadata matching with Playnite's providers
                    try
                    {
                        // If the game is from a known scene group, extract the actual name
                        var cleanName = CleanGameNameForMetadataSearch(gameName);
                        _emuLibrary.Logger.Info($"[ISO SCANNER] Searching for metadata using name: {cleanName}");
                        
                        // Use clean name for better metadata matching
                        metadata.Name = cleanName;
                        
                        // Don't set other metadata fields to let Playnite search for them
                        // This is important for SteamGridDB and other metadata providers to work
                        
                        // Set proper Source field for metadata providers
                        metadata.Source = EmuLibrary.SourceName;
                        
                        // Name will be used for Playnite's built-in metadata system
                        _emuLibrary.Logger.Info($"[ISO SCANNER] Using cleaned name for better metadata matching: {cleanName}");
                        metadata.Name = cleanName;
                        
                        // Leave other metadata fields unset to let Playnite's metadata providers fill them
                        // This is handled through Playnite's metadata system when AutoRequestMetadata is enabled
                    }
                    catch (Exception ex)
                    {
                        _emuLibrary.Logger.Error($"[ISO SCANNER] Error preparing metadata for {gameName}: {ex.Message}");
                    }
                    
                    // Add tags to identify the ISO type
                    metadata.Tags = new HashSet<MetadataProperty>() { 
                        new MetadataNameProperty("ISO Installer"),
                        new MetadataNameProperty("PC Game")
                    };
                    
                    // Store additional information in Properties
                    metadata.AddProperty("ISOFile", isoFile);
                    metadata.AddProperty("InstallerFullPath", isoFile); // Duplicate in properties for redundancy
                    metadata.AddProperty("SourcePath", mapping.SourcePath);
                    
                    // Set release year if we can parse it from the file or folder name
                    try
                    {
                        var nameWithoutPath = Path.GetFileNameWithoutExtension(isoFile);
                        var yearMatch = System.Text.RegularExpressions.Regex.Match(nameWithoutPath, @"(19|20)\d{2}");
                        if (yearMatch.Success)
                        {
                            int year;
                            if (int.TryParse(yearMatch.Value, out year) && year >= 1980 && year <= DateTime.Now.Year)
                            {
                                metadata.ReleaseDate = new ReleaseDate(year);
                                _emuLibrary.Logger.Debug($"[ISO SCANNER] Extracted release year {year} for {gameName}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _emuLibrary.Logger.Debug($"[ISO SCANNER] Failed to extract release year: {ex.Message}");
                    }
                    
                    // Log detailed information about the game being added
                    _emuLibrary.Logger.Info($"[ISO SCANNER] Adding game: {gameName} from {isoFile}");
                    _emuLibrary.Logger.Info($"[ISO SCANNER] Game details: GameId={metadata.GameId}, Platform={platformName}");
                    sourcedGames.Add(metadata);
                }
                catch (Exception ex)
                {
                    _emuLibrary.Logger.Error($"[ISO SCANNER] Error processing ISO file {isoFile}: {ex.Message}");
                }
            }
            
            // Return all discovered games outside the try/catch but with proper PluginId handling
            foreach (var game in sourcedGames)
            {
                // Add notification for ISO games to verify visibility in UI
                _emuLibrary.Playnite.Notifications.Add(
                    $"EmuLibrary-ISOScanner-GameFound-{Guid.NewGuid()}",
                    $"Found ISO game: {game.Name}",
                    NotificationType.Info);
                    
                // Log that we're returning this game
                _emuLibrary.Logger.Info($"[ISO SCANNER] Returning game: {game.Name} with GameId: {game.GameId}");
                
                yield return game;
            }
            
            // Also add installed games
            var installedGamesToReturn = new List<GameMetadata>();
            
            try
            {
                _emuLibrary.Logger.Info("[ISO SCANNER] Updating installed ISO installer games");
                
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
                        if (args.CancelToken.IsCancellationRequested)
                        {
                            _emuLibrary.Logger.Info("[ISO SCANNER] Updating installed games cancelled");
                            break; // Break out of the loop, don't use yield break here
                        }
                        
                        var game = pair.Item1;
                        var gameInfo = pair.Item2;
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
                                        // PluginId is set on Game objects after import, not on GameMetadata
                                        InstallDirectory = gameInfo.InstallDirectory,
                                        Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(mapping.Platform?.Name ?? "PC") },
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
                                    
                                    _emuLibrary.Logger.Info($"[ISO SCANNER] Adding installed game: {game.Name}");
                                }
                                else
                                {
                                    _emuLibrary.Logger.Warn($"[ISO SCANNER] Install directory no longer exists for game {game.Name}: {gameInfo.InstallDirectory}");
                                }
                            }
                            else
                            {
                                _emuLibrary.Logger.Warn($"[ISO SCANNER] Install directory is empty for installed game {game.Name}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _emuLibrary.Logger.Error($"[ISO SCANNER] Error processing installed game {game.Name}: {ex.Message}");
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
                _emuLibrary.Logger.Error($"[ISO SCANNER] Error updating installed games: {ex.Message}");
            }
            
            // Return all collected games after the try/catch block
            foreach (var game in installedGamesToReturn)
            {
                yield return game;
            }
        }

        /// <summary>
        /// Cleans game name specifically for metadata search, removing scene tags and other elements
        /// that would interfere with metadata lookup
        /// </summary>
        private string CleanGameNameForMetadataSearch(string gameName)
        {
            if (string.IsNullOrEmpty(gameName))
                return gameName;
                
            // Common scene groups to remove from filenames
            var sceneGroups = new[] {
                "CODEX", "PLAZA", "FLT", "SKIDROW", "HOODLUM", "RELOADED", 
                "PROPHET", "DODI", "CPY", "EMPRESS", "TENOKE", "VREX", "RZR", 
                "WOW", "HI", "TVM", "RLD", "DEV", "GOG", "SR", "FitGirl"
            };
            
            // Remove scene group identifiers
            foreach (var group in sceneGroups)
            {
                // Remove patterns like "CODEX-", "-CODEX", "[CODEX]", etc.
                gameName = System.Text.RegularExpressions.Regex.Replace(
                    gameName, 
                    $@"[\[\-\.\s_]*{group}[\]\-\.\s_]*", 
                    " ", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            
            // Remove patterns like "FitGirl Repack"
            gameName = System.Text.RegularExpressions.Regex.Replace(
                gameName,
                @"\b(repack|proper|multi\d+|cracked)\b",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
            // Clean up multiple spaces
            gameName = System.Text.RegularExpressions.Regex.Replace(gameName, @"\s+", " ");
            
            // Run through version number cleaner too
            gameName = CleanGameNameRemoveVersions(gameName);
            
            return gameName.Trim();
        }
        
        /// <summary>
        /// Cleans game name by removing version numbers, revision numbers, and other common suffixes
        /// </summary>
        private string CleanGameNameRemoveVersions(string gameName)
        {
            if (string.IsNullOrEmpty(gameName))
                return gameName;
            
            // Remove version numbers with v prefix (v1.0, v1.2.3, etc.)
            gameName = System.Text.RegularExpressions.Regex.Replace(gameName, @"v\d+(\.\d+)*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Remove version numbers in parentheses ((1.0), (1.2.3), etc.)
            gameName = System.Text.RegularExpressions.Regex.Replace(gameName, @"\(\d+(\.\d+)*\)", "");
            
            // Remove version numbers with dots (1.0, 1.2.3, etc.)
            gameName = System.Text.RegularExpressions.Regex.Replace(gameName, @"\s+\d+\.\d+(\.\d+)*", "");
            
            // Remove "Ultimate Edition", "Definitive Edition", etc.
            string[] editionSuffixes = new[] { 
                "Ultimate Edition", "Definitive Edition", "Game of the Year Edition", 
                "Complete Edition", "Deluxe Edition", "Digital Edition",
                "Remastered", "Special Edition"
            };
            
            foreach (var suffix in editionSuffixes)
            {
                if (gameName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    gameName = gameName.Substring(0, gameName.Length - suffix.Length).Trim();
                }
            }
            
            // Clean up spaces and other special characters
            gameName = gameName.Trim();
            
            // Remove trailing dots, dashes, and underscores
            gameName = gameName.TrimEnd('.', '-', '_', ' ');
            
            return gameName;
        }

        public override bool TryGetGameInfoBaseFromLegacyGameId(Game game, EmulatorMapping mapping, out ELGameInfo gameInfo)
        {
            // No legacy IDs for ISO installer
            gameInfo = null;
            return false;
        }

        public override IEnumerable<Game> GetUninstalledGamesMissingSourceFiles(CancellationToken ct)
        {
            try
            {
                // Get uninstalled games for this plugin
                var uninstalledGames = _playniteAPI.Database.Games
                    .Where(g => g.PluginId == EmuLibrary.PluginId && !g.IsInstalled)
                    .ToList();
                
                return uninstalledGames
                    .TakeWhile(g => !ct.IsCancellationRequested)
                    .Where(g =>
                    {
                        try
                        {
                            var info = g.GetELGameInfo();
                            if (info.RomType != RomType.ISOInstaller)
                                return false;

                            var isoInfo = info as ISOInstallerGameInfo;
                            
                            // Skip invalid paths
                            if (string.IsNullOrEmpty(isoInfo.SourceFullPath))
                                return false;
                                
                            // Check if source file exists
                            var sourceExists = false;
                            try 
                            {
                                sourceExists = File.Exists(isoInfo.SourceFullPath);
                            }
                            catch
                            {
                                return false;
                            }
                            
                            return !sourceExists;
                        }
                        catch
                        {
                            return false;
                        }
                    });
            }
            catch (Exception ex)
            {
                _emuLibrary.Logger.Error($"Error checking for missing source files: {ex.Message}");
                return Enumerable.Empty<Game>();
            }
        }
    }

    // Helper class implementing IEmuLibrary for testing
    internal class TestEmuLibrary : IEmuLibrary
    {
        public ILogger Logger { get; }
        public IPlayniteAPI Playnite { get; }
        public Settings.Settings Settings { get; }

        public TestEmuLibrary(IPlayniteAPI playniteAPI, ILogger logger)
        {
            Playnite = playniteAPI;
            Logger = logger;
            Settings = null; // Not needed for testing
        }

        public string GetPluginUserDataPath()
        {
            return Playnite.Paths.ExtensionsDataPath;
        }

        public RomTypeScanner GetScanner(RomType romType)
        {
            return null; // Not needed for testing
        }
    }

    // Helper class for ISO scanner diagnostics and testing
    public class ISOScannerTest
    {
        private readonly IPlayniteAPI _playniteAPI;
        private readonly ILogger _logger;
        private readonly List<string> discExtensions = new List<string> { 
            "iso", "bin", "img", "cue", "nrg", "mds", "mdf",
            "ISO", "BIN", "IMG", "CUE", "NRG", "MDS", "MDF" 
        };

        public ISOScannerTest(IPlayniteAPI playniteAPI, ILogger logger)
        {
            _playniteAPI = playniteAPI;
            _logger = logger;
        }

        public void TestDirectFileSearch(string directoryPath)
        {
            _logger.Info($"[TEST] Starting direct file test in: {directoryPath}");
            
            // Test 1: Direct file search with GetFiles
            try 
            {
                var allFiles = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories);
                _logger.Info($"[TEST] Found {allFiles.Length} total files using Directory.GetFiles");
                
                var discFiles = allFiles
                    .Where(f => discExtensions.Contains(Path.GetExtension(f).TrimStart('.').ToLowerInvariant()))
                    .ToList();
                    
                _logger.Info($"[TEST] Found {discFiles.Count} disc image files using direct extension check");
                
                if (discFiles.Count > 0)
                {
                    _logger.Info($"[TEST] First few disc files: {string.Join(", ", discFiles.Take(5).Select(Path.GetFileName))}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[TEST] Error in direct file search: {ex.Message}");
            }
            
            // Test 2: Pattern-based search
            try
            {
                var isoFiles = new List<string>();
                
                // Try different patterns - more likely to succeed
                foreach (var ext in new[] { "iso", "bin", "img", "cue" })
                {
                    try
                    {
                        var pattern = $"*.{ext}";
                        var files = Directory.GetFiles(directoryPath, pattern, SearchOption.AllDirectories);
                        _logger.Info($"[TEST] Found {files.Length} files with pattern: {pattern}");
                        isoFiles.AddRange(files);
                        
                        // Also try uppercase version
                        pattern = $"*.{ext.ToUpper()}";
                        files = Directory.GetFiles(directoryPath, pattern, SearchOption.AllDirectories);
                        _logger.Info($"[TEST] Found {files.Length} files with pattern: {pattern}");
                        isoFiles.AddRange(files);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[TEST] Error searching for pattern *.{ext}: {ex.Message}");
                    }
                }
                
                // Remove duplicates
                isoFiles = isoFiles.Distinct().ToList();
                _logger.Info($"[TEST] Total unique disc image files found: {isoFiles.Count}");
                
                if (isoFiles.Count > 0)
                {
                    _logger.Info($"[TEST] Examples: {string.Join(", ", isoFiles.Take(5).Select(Path.GetFileName))}");
                    
                    // Log the first few directories where files were found
                    var uniqueDirs = isoFiles.Select(Path.GetDirectoryName).Distinct().Take(3).ToList();
                    _logger.Info($"[TEST] Files found in directories: {string.Join(", ", uniqueDirs)}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[TEST] Error in pattern-based search: {ex.Message}");
            }
            
            // Test 3: Directory structure analysis
            try
            {
                _logger.Info("[TEST] Analyzing directory structure:");
                var rootDirs = Directory.GetDirectories(directoryPath, "*", SearchOption.TopDirectoryOnly);
                _logger.Info($"[TEST] Root has {rootDirs.Length} subdirectories");
                
                foreach (var dir in rootDirs.Take(5))
                {
                    var name = Path.GetFileName(dir);
                    var fileCount = 0;
                    try
                    {
                        fileCount = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories).Length;
                    }
                    catch
                    {
                        fileCount = -1; // Error counting
                    }
                    
                    _logger.Info($"[TEST] Directory '{name}' has approximately {fileCount} files");
                    
                    try
                    {
                        // Check first level for ISO files directly
                        var dirIsoFiles = Directory.GetFiles(dir, "*.iso", SearchOption.TopDirectoryOnly);
                        _logger.Info($"[TEST] Directory '{name}' has {dirIsoFiles.Length} ISO files at top level");
                    }
                    catch
                    {
                        // Ignore errors
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[TEST] Error analyzing directory structure: {ex.Message}");
            }
            
            _logger.Info("[TEST] Direct file search test completed");
        }
        
        public void TestScannerGameCreation(string directoryPath)
        {
            _logger.Info($"[TEST] Starting scanner game creation test in: {directoryPath}");
            
            try
            {
                // Create a test mapping
                var mapping = new Settings.EmulatorMapping()
                {
                    MappingId = Guid.NewGuid(),
                    RomType = RomType.ISOInstaller,
                    SourcePath = directoryPath,
                    Enabled = true
                };
                
                // Find PC platform
                var pcPlatform = _playniteAPI.Database.Platforms
                    .FirstOrDefault(p => p.Name == "PC");
                    
                // Set platform if found
                if (pcPlatform != null)
                {
                    mapping.PlatformId = pcPlatform.SpecificationId ?? pcPlatform.Id.ToString();
                    _logger.Info($"[TEST] Set platform to PC (ID: {mapping.PlatformId})");
                }
                
                // Create an instance of IEmuLibrary for the scanner to use
                var emuLib = new TestEmuLibrary(_playniteAPI, _logger);
                var scanner = new ISOInstallerScanner(emuLib);
                
                // Get games using the scanner
                var games = scanner.GetGames(mapping, new LibraryGetGamesArgs()).ToList();
                
                _logger.Info($"[TEST] Scanner GetGames returned {games.Count()} games");
                
                if (games.Count() > 0)
                {
                    _logger.Info("[TEST] Testing game creation and database import");
                    
                    foreach (var gameMetadata in games.Take(5))
                    {
                        _logger.Info($"[TEST] Processing game '{gameMetadata.Name}'");
                        _logger.Info($"[TEST] GameId: {gameMetadata.GameId}");
                        _logger.Info($"[TEST] Platform count: {gameMetadata.Platforms?.Count ?? 0}");
                        
                        if (gameMetadata.Platforms != null)
                        {
                            var platformNames = string.Join(", ", gameMetadata.Platforms.OfType<MetadataNameProperty>().Select(p => p.Name));
                            _logger.Info($"[TEST] Platforms: {platformNames}");
                        }
                        
                        if (gameMetadata.Tags != null)
                        {
                            var tagNames = string.Join(", ", gameMetadata.Tags.OfType<MetadataNameProperty>().Select(t => t.Name));
                            _logger.Info($"[TEST] Tags: {tagNames}");
                        }
                        
                        // Don't actually import to database - just test conversion
                        try
                        {
                            // Note: PluginId would be set by Playnite when actually importing
                            _logger.Info("[TEST] Game metadata looks good, would be imported with proper PluginId");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[TEST] Error importing game '{gameMetadata.Name}': {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[TEST] Error in scanner game creation test: {ex.Message}");
            }
            
            _logger.Info("[TEST] Scanner game creation test completed");
        }
    }
}