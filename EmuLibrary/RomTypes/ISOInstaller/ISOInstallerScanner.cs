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
        public override DateTime CreationTime => _fileInfo.CreationTime;
        public override DateTime LastAccessTime => _fileInfo.LastAccessTime;
        public override DateTime LastWriteTime => _fileInfo.LastWriteTime;
        public override System.IO.FileAttributes Attributes => _fileInfo.Attributes;
    }
    
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

            // For ISO installer disc images, emulator profile is optional since they're typically mounted and installed
            // No validation needed for emulator profile as these games run natively after installation

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

            // Only support ISO, BIN, IMG, and other disc image files - include case variations for better detection
            var discExtensions = new List<string> { "iso", "bin", "img", "cue", "nrg", "mds", "mdf", "ISO", "BIN", "IMG", "CUE", "NRG", "MDS", "MDF" };
            
            // Log the supported extensions
            _emuLibrary.Logger.Info($"Looking for disc image files with extensions: {string.Join(", ", discExtensions)}");
            
            // Add special test for filenames ending with .iso regardless of extension parsing
            try {
                _emuLibrary.Logger.Info("Performing special filename search with endsWith pattern...");
                
                // CRITICAL FIX: Check if we're scanning the drive root - if so, we need to do a more targeted approach
                bool isScanningDriveRoot = srcPath.Length <= 3 && srcPath.EndsWith(":\\", StringComparison.OrdinalIgnoreCase);
                List<string> simpleSearch;
                
                if (isScanningDriveRoot)
                {
                    _emuLibrary.Logger.Info($"Root drive scan detected ({srcPath}). Using targeted approach for better performance.");
                    
                    simpleSearch = new List<string>();
                    var rootPathDirInfo = new DirectoryInfo(srcPath);
                    var rootFolders = rootPathDirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly)
                        .Where(d => !d.Name.StartsWith("$") && // Skip system folders like $RECYCLE.BIN
                                   !d.Name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    
                    _emuLibrary.Logger.Info($"Found {rootFolders.Count} top-level directories to scan");
                    
                    // Search each first-level folder for ISO files directly - use searchPattern to be more efficient
                    foreach (var folder in rootFolders)
                    {
                        try
                        {
                            // Use separate GetFiles calls with wildcards for better performance
                            var isoFiles = Directory.GetFiles(folder.FullName, "*.iso", SearchOption.AllDirectories);
                            var binFiles = Directory.GetFiles(folder.FullName, "*.bin", SearchOption.AllDirectories);
                            var imgFiles = Directory.GetFiles(folder.FullName, "*.img", SearchOption.AllDirectories);
                            var cueFiles = Directory.GetFiles(folder.FullName, "*.cue", SearchOption.AllDirectories);
                            
                            simpleSearch.AddRange(isoFiles);
                            simpleSearch.AddRange(binFiles);
                            simpleSearch.AddRange(imgFiles);
                            simpleSearch.AddRange(cueFiles);
                            
                            var count = isoFiles.Length + binFiles.Length + imgFiles.Length + cueFiles.Length;
                            if (count > 0)
                            {
                                _emuLibrary.Logger.Info($"Found {count} disc image files in {folder.Name}: ISO={isoFiles.Length}, BIN={binFiles.Length}, IMG={imgFiles.Length}, CUE={cueFiles.Length}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _emuLibrary.Logger.Error($"Error scanning folder {folder.Name}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    // Original approach for non-root paths
                    simpleSearch = Directory.GetFiles(srcPath, "*.*", SearchOption.AllDirectories)
                        .Where(f => f.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) || 
                                   f.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) || 
                                   f.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
                
                _emuLibrary.Logger.Info($"Simple filename search found {simpleSearch.Count} disc image files in {srcPath}");
                
                if (simpleSearch.Count > 0) {
                    _emuLibrary.Logger.Info($"Simple search examples: {string.Join(", ", simpleSearch.Take(5))}");
                    
                    // Test extension parsing for these files
                    foreach (var file in simpleSearch.Take(3)) {
                        _emuLibrary.Logger.Info($"Testing extension parsing for: {file}");
                        _emuLibrary.Logger.Info($"  Path.GetExtension: '{Path.GetExtension(file)}'");
                        _emuLibrary.Logger.Info($"  Path.GetFileName: '{Path.GetFileName(file)}'");
                        _emuLibrary.Logger.Info($"  Trimmed extension: '{Path.GetExtension(file).TrimStart('.')}'");
                        _emuLibrary.Logger.Info($"  Lowercase: '{Path.GetExtension(file).TrimStart('.').ToLowerInvariant()}'");
                        _emuLibrary.Logger.Info($"  In list: {discExtensions.Contains(Path.GetExtension(file).TrimStart('.').ToLowerInvariant())}");
                    }
                }
            }
            catch (Exception ex) {
                _emuLibrary.Logger.Error($"Error in special filename search: {ex.Message}");
            }
            
            // Do a direct folder scan to see if any such files exist
            try {
                List<string> directSearch;
                bool isScanningDriveRoot = srcPath.Length <= 3 && srcPath.EndsWith(":\\", StringComparison.OrdinalIgnoreCase);
                
                if (isScanningDriveRoot)
                {
                    // For drive roots, we need to search each top-level folder individually
                    directSearch = new List<string>();
                    var rootPathDirInfo = new DirectoryInfo(srcPath);
                    var rootFolders = rootPathDirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly)
                        .Where(d => !d.Name.StartsWith("$") && // Skip system folders like $RECYCLE.BIN
                                   !d.Name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    
                    // Use direct pattern searches instead of full directory scans with filtering
                    foreach (var folder in rootFolders)
                    {
                        try
                        {
                            foreach (var ext in discExtensions.Distinct())
                            {
                                string pattern = $"*.{ext}";
                                var files = Directory.GetFiles(folder.FullName, pattern, SearchOption.AllDirectories);
                                directSearch.AddRange(files);
                                
                                if (files.Length > 0)
                                {
                                    _emuLibrary.Logger.Info($"Found {files.Length} {ext} files in {folder.Name}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _emuLibrary.Logger.Error($"Error in direct search for folder {folder.Name}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    // Original approach for non-root paths
                    directSearch = Directory.GetFiles(srcPath, "*.*", SearchOption.AllDirectories)
                        .Where(f => discExtensions.Contains(Path.GetExtension(f).TrimStart('.').ToLowerInvariant()))
                        .ToList();
                }
                
                _emuLibrary.Logger.Info($"Direct search found {directSearch.Count} disc image files in {srcPath}");
                
                if (directSearch.Count > 0) {
                    _emuLibrary.Logger.Info($"Examples: {string.Join(", ", directSearch.Take(5).Select(Path.GetFileName))}");
                }
                
                // Try to find specific example file mentioned by user
                string specificPath = Path.Combine(srcPath, "Octopath.Traveler", "cpy-ot.iso");
                if (File.Exists(specificPath)) {
                    _emuLibrary.Logger.Info($"FOUND SPECIFIC TEST FILE: {specificPath}");
                    // Get file info
                    var fileInfo = new FileInfo(specificPath);
                    _emuLibrary.Logger.Info($"File exists: Size={fileInfo.Length}, LastWrite={fileInfo.LastWriteTime}, Attributes={fileInfo.Attributes}");
                    _emuLibrary.Logger.Info($"File extension: '{Path.GetExtension(specificPath)}' - Length: {Path.GetExtension(specificPath)?.Length ?? 0}");
                } else {
                    _emuLibrary.Logger.Error($"SPECIFIC TEST FILE NOT FOUND: {specificPath}");
                }
            }
            catch (Exception ex) {
                _emuLibrary.Logger.Error($"Error in direct file search: {ex.Message}");
            }
            
            SafeFileEnumerator fileEnumerator;
            var gameMetadataBatch = new List<GameMetadata>();

            #region Import discovered ISO files
            var sourcedGames = new List<GameMetadata>();
            try
            {
                // CRITICAL FIX: Check if we're scanning the drive root
            bool isScanningDriveRoot = srcPath.Length <= 3 && srcPath.EndsWith(":\\", StringComparison.OrdinalIgnoreCase);
            
            // For drive roots, we'll use a different enumeration strategy
            if (isScanningDriveRoot)
            {
                _emuLibrary.Logger.Info($"Root drive scan detected for {srcPath}. Using optimized enumeration strategy.");
                // Still initialize the enumerator (we'll modify how it's used)
            }
            
            fileEnumerator = new SafeFileEnumerator(srcPath, "*.*", SearchOption.AllDirectories);
                _emuLibrary.Logger.Info($"Scanning for ISO disc images in {srcPath} with enhanced fuzzy matching");

                // Create a dictionary to cache normalized folder names for performance
                var normalizedNameCache = new Dictionary<string, string>();
                // Track processed game names to avoid duplicates from similar folder names
                var processedGameNames = new HashSet<string>();
                // Dictionary to detect ISO files that belong to the same game via fuzzy matching
                var gameNameGroups = new Dictionary<string, string>();
                
                // First scan - collect all potential game names from folder structure
                var folderNames = new HashSet<string>();
                try 
                {
                    var sourceDirInfo = new DirectoryInfo(srcPath);
                    foreach (var dir in sourceDirInfo.GetDirectories("*", SearchOption.AllDirectories))
                    {
                        var folderName = dir.Name;
                        if (!string.IsNullOrWhiteSpace(folderName))
                        {
                            folderNames.Add(folderName);
                        }
                    }
                    _emuLibrary.Logger.Info($"Found {folderNames.Count} potential game folders");
                }
                catch (Exception ex)
                {
                    _emuLibrary.Logger.Error($"Error collecting folder names: {ex.Message}");
                }

                // Group folders by directory structure first (parent-child relationships)
                var rootPathDirInfo = new DirectoryInfo(srcPath);
                var rootFolders = rootPathDirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly).ToList();
                
                // Process root folders first (game folders)
                foreach (var gameFolder in rootFolders)
                {
                    var gameFolderName = gameFolder.Name;
                    var normalizedName = StringExtensions.NormalizeGameName(gameFolderName);
                    
                    if (string.IsNullOrWhiteSpace(normalizedName))
                        continue;
                    
                    // Add root game folder
                    gameNameGroups[gameFolderName] = normalizedName;
                    
                    // Get all update/DLC subfolders for this game
                    var updateFolders = gameFolder.GetDirectories("*", SearchOption.TopDirectoryOnly).ToList();
                    _emuLibrary.Logger.Debug($"Found {updateFolders.Count} potential update folders for {gameFolderName}");
                    
                    foreach (var updateFolder in updateFolders)
                    {
                        // Mark this as an update folder by associating it with its parent
                        var updateName = updateFolder.Name;
                        gameNameGroups[updateName] = normalizedName + " [Update: " + updateName + "]";
                        _emuLibrary.Logger.Debug($"Associated update folder '{updateName}' with game '{normalizedName}'");
                    }
                }
                
                // Now apply fuzzy matching only for folders without a parent-child relationship
                var remainingFolders = folderNames.Where(f => !gameNameGroups.ContainsKey(f)).ToList();
                _emuLibrary.Logger.Debug($"Processing {remainingFolders.Count} remaining folders with fuzzy matching");
                
                foreach (var folderName in remainingFolders)
                {
                    var normalizedName = StringExtensions.NormalizeGameName(folderName);
                    if (string.IsNullOrWhiteSpace(normalizedName))
                        continue;
                        
                    // Check if this folder name is similar to any existing known game
                    bool matched = false;
                    foreach (var knownGame in gameNameGroups.Keys.ToList())
                    {
                        if (normalizedName.IsSimilarTo(knownGame, 0.8)) // 80% similarity threshold
                        {
                            // Add this folder name as a variant of the known game
                            gameNameGroups[folderName] = gameNameGroups[knownGame];
                            matched = true;
                            _emuLibrary.Logger.Debug($"Fuzzy match: '{folderName}' matches '{knownGame}'");
                            break;
                        }
                    }
                    
                    if (!matched)
                    {
                        // This is a new game name
                        gameNameGroups[folderName] = normalizedName;
                    }
                }
                
                _emuLibrary.Logger.Info($"Consolidated to {gameNameGroups.Values.Distinct().Count()} unique games via fuzzy matching");

                // For drive root scans, we'll directly enumerate each game directory rather than
                // using the general-purpose fileEnumerator, which can miss files in deep directories
                IEnumerable<FileSystemInfoBase> filesToProcess;
                
                if (isScanningDriveRoot)
                {
                    // Build a custom enumeration to efficiently find ISO files in game folders
                    var customFiles = new List<FileSystemInfoBase>();
                    var rootPathDirInfo = new DirectoryInfo(srcPath);
                    
                    // Get all top-level directories (these are likely game folders)
                    var rootFolders = rootPathDirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly)
                        .Where(d => !d.Name.StartsWith("$") && 
                               !d.Name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    
                    _emuLibrary.Logger.Info($"Root scan optimization: Processing {rootFolders.Count} potential game folders directly");
                    
                    // For each game folder, search specifically for disc image files
                    foreach (var gameFolder in rootFolders)
                    {
                        if (args.CancelToken.IsCancellationRequested)
                            break;
                            
                        try
                        {
                            // Look specifically for common disc image types
                            foreach (var pattern in new[] { "*.iso", "*.ISO", "*.bin", "*.BIN", "*.img", "*.IMG", "*.cue", "*.CUE", "*.mdf", "*.MDF" })
                            {
                                // First look directly in the game folder
                                foreach (var file in gameFolder.GetFiles(pattern, SearchOption.TopDirectoryOnly))
                                {
                                    customFiles.Add(new FileInfoWrapper(file));
                                }
                                
                                // Then check 1 level deeper (common for multi-part games)
                                foreach (var subFolder in gameFolder.GetDirectories())
                                {
                                    foreach (var file in subFolder.GetFiles(pattern, SearchOption.TopDirectoryOnly))
                                    {
                                        customFiles.Add(new FileInfoWrapper(file));
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _emuLibrary.Logger.Error($"Error scanning folder {gameFolder.Name}: {ex.Message}");
                        }
                    }
                    
                    _emuLibrary.Logger.Info($"Root scan optimization: Found {customFiles.Count} disc image files in game directories");
                    filesToProcess = customFiles;
                }
                else
                {
                    // For normal non-drive-root scans, use the standard file enumerator
                    filesToProcess = fileEnumerator;
                }
                
                foreach (var file in filesToProcess)
                {
                    if (args.CancelToken.IsCancellationRequested)
                    {
                        _emuLibrary.Logger.Info("Scanning cancelled during file enumeration");
                        yield break;
                    }
                    
                    // Skip Windows system directories and files
                    if (file.FullName.Contains("$RECYCLE.BIN") || 
                        file.FullName.Contains("System Volume Information") ||
                        file.FullName.StartsWith("S-1-5-") ||
                        file.Name.StartsWith("."))
                    {
                        _emuLibrary.Logger.Debug($"Skipping system file/directory: {file.FullName}");
                        continue;
                    }

                    // Check the file extension directly instead of iterating through each extension
                    string fileExtension = file.Extension?.TrimStart('.')?.ToLowerInvariant();
                    
                    if (args.CancelToken.IsCancellationRequested)
                        yield break;
                    
                    // Check special pattern for test file
                    if (file.FullName.Contains("cpy-ot.iso")) {
                        _emuLibrary.Logger.Info($"FOUND TEST FILE IN ENUMERATION: {file.FullName}");
                        _emuLibrary.Logger.Info($"File.Extension={file.Extension}, FileExtension={fileExtension}");
                        _emuLibrary.Logger.Info($"Path.GetExtension={Path.GetExtension(file.FullName)}");
                    }
                    
                    // Enhanced logging for extension check
                    _emuLibrary.Logger.Debug($"Checking if file {file.Name} has extension '{fileExtension}', raw extension: '{file.Extension}'");
                    
                    // Check if this file has a supported extension
                    bool extensionMatch = !string.IsNullOrEmpty(fileExtension) && discExtensions.Contains(fileExtension.ToLowerInvariant());
                    
                    // Add extra check for known patterns
                    bool specialCaseMatch = file.Name.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) || 
                                            file.Name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) ||
                                            file.Name.EndsWith(".img", StringComparison.OrdinalIgnoreCase);
                    
                    if (specialCaseMatch && !extensionMatch) {
                        _emuLibrary.Logger.Info($"Special case detection for file that has .iso in name but failed normal check: {file.FullName}");
                    }
                    
                    if (extensionMatch || specialCaseMatch)
                    {
                        _emuLibrary.Logger.Info($"Found ISO file: {file.FullName} (matches extension '{fileExtension}')");
                        try
                        {
                                // For ISO installers, we use the parent folder name as the game name
                                var parentFolderPath = Path.GetDirectoryName(file.FullName);
                                if (parentFolderPath == null)
                                {
                                    _emuLibrary.Logger.Warn($"Could not get parent directory for {file.FullName}, skipping");
                                    continue;
                                }

                                var parentFolder = Path.GetFileName(parentFolderPath);
                                
                                // Use cached normalized name if available
                                // Initialize game name with parent folder default value
                                string gameName = StringExtensions.NormalizeGameName(parentFolder);
                                
                                // First try SteamGridDB matching if enabled
                                bool usedSteamGridDb = false;
                                if (Settings.Settings.Instance.EnableSteamGridDbMatching && 
                                    Util.SteamGridDbService.Instance != null &&
                                    Util.SteamGridDbService.Instance.IsEnabled)
                                {
                                    try
                                    {
                                        // Try to match with SteamGridDB (synchronous version)
                                        string matchedName;
                                        if (Util.SteamGridDbService.Instance.TryMatchGameName(parentFolder, out matchedName))
                                        {
                                            gameName = matchedName;
                                            _emuLibrary.Logger.Info($"Using SteamGridDB matched name for {parentFolder}: {gameName}");
                                            usedSteamGridDb = true;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _emuLibrary.Logger.Error($"Error using SteamGridDB to match game name: {ex.Message}");
                                    }
                                }
                                
                                // If SteamGridDB didn't work, try fuzzy match next
                                if (!usedSteamGridDb)
                                {
                                    if (gameNameGroups.TryGetValue(parentFolder, out string matchedGameName))
                                    {
                                        gameName = matchedGameName;
                                        _emuLibrary.Logger.Debug($"Using fuzzy matched game name for {parentFolder}: {gameName}");
                                    }
                                    else if (!normalizedNameCache.TryGetValue(parentFolder, out string cachedName))
                                    {
                                        // We already initialized gameName above, just add to cache
                                        normalizedNameCache[parentFolder] = gameName;
                                    }
                                    else
                                    {
                                        gameName = cachedName; 
                                    }
                                }
                                
                                if (string.IsNullOrEmpty(gameName))
                                {
                                    _emuLibrary.Logger.Warn($"Game name is empty for {file.FullName}, using file name instead");
                                    gameName = Path.GetFileNameWithoutExtension(file.Name);
                                }
                                
                                // For updates, we need to be careful not to skip processing
                                if (processedGameNames.Contains(gameName))
                                {
                                    _emuLibrary.Logger.Debug($"Potential duplicate: {gameName} from {file.FullName} - will check if it's an update");
                                    // Continue processing since we'll determine if it's an update later
                                }
                                else
                                {
                                    // Add to processed games set
                                    processedGameNames.Add(gameName);
                                }
                                
                                var relativePath = file.FullName.Substring(srcPath.Length).TrimStart(Path.DirectorySeparatorChar);
                                
                                // Try to detect GOG discs
                                bool isGogInstaller = file.Name.Contains("gog") || 
                                                     file.Name.Contains("setup_") || 
                                                     file.FullName.Contains("GOG") ||
                                                     parentFolder.Contains("GOG");
                                
                                string storeGameId = null;
                                string installerType = null;
                                // Define content type variables for detection
                                var contentType = PCInstaller.ContentType.BaseGame;
                                string version = null;
                                string contentDescription = null;
                                string parentGameId = null;
                                
                                if (isGogInstaller)
                                {
                                    installerType = "GOG";
                                    
                                    // GOG discs often contain an ID
                                    var filename = Path.GetFileNameWithoutExtension(file.Name);
                                    if (!string.IsNullOrEmpty(filename) && filename.StartsWith("setup_"))
                                    {
                                        var parts = filename.Split('_');
                                        if (parts.Length >= 3)
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
                                
                                // First, check if this is an update/DLC folder inside a game folder
                                // In this case, parentFolder is the update folder name and grandparentFolder is the game name
                                var folderDirInfo = new DirectoryInfo(parentFolderPath);
                                var fileDir = folderDirInfo.FullName;
                                var fileInRootFolder = false;
                                
                                // Define a list of valid update file extensions 
                                // Include both ISO/image formats and executable formats for updates
                                var updateFileExtensions = new List<string>();
                                updateFileExtensions.AddRange(discExtensions); // All disc image formats
                                updateFileExtensions.AddRange(new[] { "exe", "msi", "bin" }); // Common executable/installer formats
                                
                                // Check if this file is directly in the game's root folder or in a subfolder
                                var rootGameFolder = folderDirInfo.Parent; // This would be the game folder
                                
                                if (rootGameFolder != null && rootGameFolder.FullName.StartsWith(srcPath))
                                {
                                    // Find all valid files in the root game folder to detect base game
                                    var rootGameFolderFiles = rootGameFolder.GetFiles("*.*", SearchOption.TopDirectoryOnly)
                                        .Where(f => {
                                            string ext = Path.GetExtension(f.Name)?.TrimStart('.')?.ToLowerInvariant();
                                            return !string.IsNullOrEmpty(ext) && discExtensions.Contains(ext);
                                        })
                                        .ToList();
                                        
                                    _emuLibrary.Logger.Debug($"Root folder check: Found {rootGameFolderFiles.Count} valid files in root folder {rootGameFolder.Name}");
                                    if (rootGameFolderFiles.Count > 0) {
                                        _emuLibrary.Logger.Debug($"Examples: {string.Join(", ", rootGameFolderFiles.Take(3).Select(f => f.Name))}");
                                    }
                                        
                                    // Find any valid update files in subfolders (including .exe, etc)
                                    var updateFiles = new List<FileInfo>();
                                    try
                                    {
                                        foreach (var subfolder in rootGameFolder.GetDirectories())
                                        {
                                            var files = subfolder.GetFiles("*.*", SearchOption.AllDirectories)
                                                .Where(f => {
                                                    string ext = Path.GetExtension(f.Name)?.TrimStart('.')?.ToLowerInvariant();
                                                    return !string.IsNullOrEmpty(ext) && updateFileExtensions.Contains(ext);
                                                }).ToList();
                                                
                                            _emuLibrary.Logger.Debug($"Update folder check: Found {files.Count} valid files in update folder {subfolder.Name}");
                                            if (files.Count > 0) {
                                                _emuLibrary.Logger.Debug($"Examples: {string.Join(", ", files.Take(3).Select(f => f.Name))}");
                                            }
                                            
                                            updateFiles.AddRange(files);
                                        }
                                    }
                                    catch (Exception ex) 
                                    {
                                        _emuLibrary.Logger.Error($"Error checking for update files: {ex.Message}");
                                    }
                                        
                                    // If the parent folder has ISO files directly, this might be a game folder with updates in subfolders
                                    if (rootGameFolderFiles.Any())
                                    {
                                        fileInRootFolder = true;
                                        _emuLibrary.Logger.Debug($"Found {rootGameFolderFiles.Count} ISO files in root game folder {rootGameFolder.Name}");
                                        _emuLibrary.Logger.Debug($"Found {updateFiles.Count} potential update files in subfolders");
                                        
                                        // This directory has ISO files, so we're looking at the base game
                                        if (folderDirInfo.FullName == rootGameFolder.FullName)
                                        {
                                            _emuLibrary.Logger.Debug($"File {file.Name} is in game root folder {rootGameFolder.Name}");
                                            contentType = PCInstaller.ContentType.BaseGame;
                                        }
                                        // We're in a subfolder, so this is likely an update
                                        else if (folderDirInfo.Parent != null && folderDirInfo.Parent.FullName == rootGameFolder.FullName)
                                        {
                                            // This is an update folder inside a game folder - your structure
                                            // For this file to be an update, it should be either an ISO or an EXE
                                            if (updateFileExtensions.Contains(Path.GetExtension(file.Name).TrimStart('.').ToLowerInvariant()))
                                            {
                                                _emuLibrary.Logger.Info($"Detected update folder: {folderDirInfo.Name} inside game folder {rootGameFolder.Name}");
                                                contentType = PCInstaller.ContentType.Update;
                                                contentDescription = folderDirInfo.Name;
                                                parentGameId = StringExtensions.NormalizeGameName(rootGameFolder.Name);
                                                
                                                // Try to extract version number
                                                var versionMatch = System.Text.RegularExpressions.Regex.Match(
                                                    folderDirInfo.Name, @"v?(\d+(\.\d+)*)");
                                                if (versionMatch.Success)
                                                {
                                                    version = versionMatch.Groups[1].Value;
                                                }
                                                
                                                _emuLibrary.Logger.Info($"Detected update: {folderDirInfo.Name} for game {parentGameId}, version: {version ?? "unknown"}, file: {file.Name}");
                                            }
                                        }
                                    }
                                }
                                
                                var info = new ISOInstallerGameInfo()
                                {
                                    MappingId = mapping.MappingId,
                                    SourcePath = relativePath,
                                    InstallerFullPath = file.FullName,
                                    InstallDirectory = null, // Will be set during installation
                                    StoreGameId = storeGameId,
                                    InstallerType = installerType,
                                    ContentType = contentType,
                                    Version = version,
                                    ContentDescription = contentDescription,
                                    ParentGameId = parentGameId
                                };

                                // Set proper platform based on installer type
                                string platformName = mapping.Platform?.Name;
                                if (string.IsNullOrEmpty(platformName))
                                {
                                    platformName = "PC"; // Default fallback when no platform is selected
                                    _emuLibrary.Logger.Info($"No platform set for ISOInstaller, using default '{platformName}'");
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
                                    IsInstalled = false, // ISO installer games start as uninstalled
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
                                
                                // Add metadata for EmuLibrary tracking (do not change Source property)
                                if (!string.IsNullOrEmpty(info.StoreGameId) && !string.IsNullOrEmpty(info.InstallerType))
                                {
                                    // Add store-specific info to help with metadata matching later
                                    metadata.Tags = metadata.Tags ?? new HashSet<MetadataProperty>();
                                    metadata.Tags.Add(new MetadataNameProperty($"{info.InstallerType}:{info.StoreGameId}"));
                                    
                                    _emuLibrary.Logger.Debug($"Added store metadata for {gameName}: {info.InstallerType} ID {info.StoreGameId}");
                                }
                                
                                gameMetadataBatch.Add(metadata);
                                
                                // Log successful game detection
                                _emuLibrary.Logger.Info($"Found game: {gameName} from ISO file: {file.FullName}");

                                // Batch processing handled outside the try block
                            }
                            catch (Exception ex)
                            {
                                _emuLibrary.Logger.Error($"Error processing ISO file {file.FullName}: {ex.Message}");
                            }
                        }
                    
                }

                // Add remaining games to the collection
                if (gameMetadataBatch.Count > 0)
                {
                    _emuLibrary.Logger.Debug($"Adding final batch of {gameMetadataBatch.Count} ISO installer games");
                    sourcedGames.AddRange(gameMetadataBatch);
                    gameMetadataBatch.Clear();
                }
            }
            catch (Exception ex)
            {
                _emuLibrary.Logger.Error($"Error scanning source directory {srcPath}: {ex.Message}");
            }
            
            // Log summary of found games
            _emuLibrary.Logger.Info($"ISO Scanner found {sourcedGames.Count} games total");
            
            if (sourcedGames.Count > 0)
            {
                _emuLibrary.Logger.Info("Found games:");
                foreach (var game in sourcedGames)
                {
                    _emuLibrary.Logger.Info($"  - {game.Name}");
                    yield return game;
                }
            }
            else
            {
                _emuLibrary.Logger.Warn("No games found in the source directory. Check your folder structure and ensure ISO files are present.");
                _emuLibrary.Logger.Info($"Supported disc image extensions: {string.Join(", ", discExtensions.Distinct())}");
                
                // Additional diagnostic info to help with troubleshooting
                try {
                    // Direct path check for a specific .iso file that should exist
                    string specificPath = Path.Combine(srcPath, "Octopath.Traveler", "cpy-ot.iso");
                    if (File.Exists(specificPath)) {
                        _emuLibrary.Logger.Info($"FOUND SPECIFIC TEST FILE: {specificPath}");
                        // Get file info
                        var fileInfo = new FileInfo(specificPath);
                        _emuLibrary.Logger.Info($"File exists: Size={fileInfo.Length}, LastWrite={fileInfo.LastWriteTime}, Attributes={fileInfo.Attributes}");
                        _emuLibrary.Logger.Info($"File extension: '{Path.GetExtension(specificPath)}' - Length: {Path.GetExtension(specificPath)?.Length ?? 0}");
                        
                        // Test direct extension check
                        string ext = Path.GetExtension(specificPath)?.TrimStart('.')?.ToLowerInvariant();
                        _emuLibrary.Logger.Info($"Extension after processing: '{ext}' - Is in list: {discExtensions.Contains(ext)}");
                        
                        // Test with System.IO.Path methods
                        _emuLibrary.Logger.Info($"Path.GetExtension: '{Path.GetExtension(specificPath)}'");
                        _emuLibrary.Logger.Info($"Path.GetFileName: '{Path.GetFileName(specificPath)}'");
                    } else {
                        _emuLibrary.Logger.Error($"SPECIFIC TEST FILE NOT FOUND: {specificPath}");
                    }
                    
                    // Print all files in the directory (top level only) to see what's actually there
                    var allFiles = Directory.GetFiles(srcPath, "*.*", SearchOption.TopDirectoryOnly);
                    _emuLibrary.Logger.Info($"Found {allFiles.Length} files at top level in {srcPath}");
                    
                    if (allFiles.Length > 0) {
                        _emuLibrary.Logger.Info($"Examples: {string.Join(", ", allFiles.Take(10).Select(Path.GetFileName))}");
                    }
                    
                    // Check subdirectories too
                    var allDirs = Directory.GetDirectories(srcPath, "*", SearchOption.TopDirectoryOnly);
                    _emuLibrary.Logger.Info($"Found {allDirs.Length} subdirectories at top level in {srcPath}");
                    
                    if (allDirs.Length > 0) {
                        _emuLibrary.Logger.Info($"Examples: {string.Join(", ", allDirs.Take(10).Select(Path.GetFileName))}");
                        
                        // Pick first directory and see what's inside
                        if (allDirs.Length > 0) {
                            var firstDir = allDirs[0];
                            var filesInFirstDir = Directory.GetFiles(firstDir, "*.*", SearchOption.TopDirectoryOnly);
                            _emuLibrary.Logger.Info($"First directory '{Path.GetFileName(firstDir)}' contains {filesInFirstDir.Length} files");
                            
                            if (filesInFirstDir.Length > 0) {
                                _emuLibrary.Logger.Info($"Examples: {string.Join(", ", filesInFirstDir.Take(10).Select(Path.GetFileName))}");
                            }
                        }
                    }
                }
                catch (Exception ex) {
                    _emuLibrary.Logger.Error($"Error in diagnostic check: {ex.Message}");
                }
                
                // Return empty collection
                foreach (var game in sourcedGames)
                {
                    yield return game;
                }
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
                                
                                // Skip user or system IDs that got into the database somehow
                                if (g.Name.StartsWith("S-1-5-") || string.IsNullOrWhiteSpace(g.Name))
                                {
                                    _emuLibrary.Logger.Warn($"Invalid game name detected (system ID or empty): {g.Name}, skipping source check");
                                    return false;
                                }
                                
                                // Check if the source path is valid before checking existence
                                if (string.IsNullOrEmpty(isoInfo.SourceFullPath))
                                {
                                    _emuLibrary.Logger.Warn($"Empty source path for game {g.Name}, skipping source check");
                                    return false;
                                }
                                
                                var sourceExists = false;
                                try 
                                {
                                    sourceExists = File.Exists(isoInfo.SourceFullPath);
                                }
                                catch (Exception pathEx)
                                {
                                    _emuLibrary.Logger.Error($"Invalid source path for game {g.Name}: {pathEx.Message}");
                                    return false;
                                }
                                
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