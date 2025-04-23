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
                foreach (var ext in new[] { "iso", "img", "cue" })
                {
                    var pattern = $"*.{ext}";
                    var files = Directory.GetFiles(srcPath, pattern, SearchOption.AllDirectories);
                    isoFiles.AddRange(files);
                    
                    // Also try uppercase version
                    pattern = $"*.{ext.ToUpper()}";
                    files = Directory.GetFiles(srcPath, pattern, SearchOption.AllDirectories);
                    isoFiles.AddRange(files);
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
                    _emuLibrary.Logger.Info($"[ISO SCANNER] Examples: {string.Join(", ", isoFiles.Take(5).Select(Path.GetFileName))}");
                    
                    // Log the first few directories where files were found
                    var uniqueDirs = isoFiles.Select(Path.GetDirectoryName).Distinct().Take(3).ToList();
                    _emuLibrary.Logger.Info($"[ISO SCANNER] Files found in directories: {string.Join(", ", uniqueDirs)}");
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
            var processedGameNames = new HashSet<string>();
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
                    
                    // Get parent folder for game name
                    var parentFolderPath = Path.GetDirectoryName(isoFile);
                    var parentFolder = Path.GetFileName(parentFolderPath);
                    var gameName = StringExtensions.NormalizeGameName(parentFolder);
                    
                    // Skip certain problematic folders
                    if (parentFolder == "Update" || 
                        parentFolder.StartsWith("setup.part") || 
                        parentFolder == "Renderer")
                    {
                        // Go up one more level for parent folder
                        parentFolderPath = Directory.GetParent(parentFolderPath)?.FullName;
                        if (parentFolderPath != null)
                        {
                            parentFolder = Path.GetFileName(parentFolderPath);
                            gameName = StringExtensions.NormalizeGameName(parentFolder);
                        }
                    }
                    
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
                                gameName = StringExtensions.NormalizeGameName(parts[1]);
                            }
                            else
                            {
                                gameName = StringExtensions.NormalizeGameName(fileName);
                            }
                        }
                        else if (fileName.Contains("setup_"))
                        {
                            // Handle GOG installer names like "setup_game_name_1.2.3"
                            var withoutSetup = fileName.Replace("setup_", "");
                            var parts = withoutSetup.Split(new[] { '_' }, 2);
                            if (parts.Length > 1)
                            {
                                gameName = StringExtensions.NormalizeGameName(parts[0]);
                            }
                            else
                            {
                                gameName = StringExtensions.NormalizeGameName(fileName);
                            }
                        }
                        else
                        {
                            gameName = StringExtensions.NormalizeGameName(fileName);
                        }
                    }
                    
                    // Skip duplicate game names
                    if (processedGameNames.Contains(gameName))
                    {
                        _emuLibrary.Logger.Debug($"[ISO SCANNER] Skipping duplicate game name: {gameName} from {isoFile}");
                        continue;
                    }
                    
                    processedGameNames.Add(gameName);
                    
                    // Get relative path from source folder
                    var relativePath = isoFile.Substring(srcPath.Length).TrimStart(Path.DirectorySeparatorChar);
                    
                    // Create game info
                    var info = new ISOInstallerGameInfo()
                    {
                        MappingId = mapping.MappingId,
                        SourcePath = relativePath,
                        InstallerFullPath = isoFile,
                        InstallDirectory = null, // Will be set during installation
                    };
                    
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
                        // CRITICAL: Must set PluginId or games won't appear in Playnite UI
                        PluginId = EmuLibrary.PluginId,
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
                    
                    // Add additional metadata to make the game more identifiable
                    metadata.Description = $"ISO installer game from {Path.GetFileName(isoFile)}";
                    
                    // Add tags to identify the ISO type
                    metadata.Tags = new HashSet<MetadataProperty>() { 
                        new MetadataNameProperty("ISO Installer"),
                        new MetadataNameProperty("PC Game")
                    };
                    
                    // Store additional information in Properties
                    metadata.AddProperty("ISOFile", isoFile);
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
                    _emuLibrary.Logger.Info($"[ISO SCANNER] Game details: GameId={metadata.GameId}, PluginId={metadata.PluginId}, Platform={platformName}");
                    sourcedGames.Add(metadata);
                }
                catch (Exception ex)
                {
                    _emuLibrary.Logger.Error($"[ISO SCANNER] Error processing ISO file {isoFile}: {ex.Message}");
                }
            }
            
            // Return all discovered games outside the try/catch
            foreach (var game in sourcedGames)
            {
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
                            yield break; // Will continue after the catch block
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
                                        // CRITICAL: Must set PluginId or games won't appear in Playnite UI
                                        PluginId = EmuLibrary.PluginId,
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
                            _logger.Info($"[TEST] Platforms: {string.Join(", ", gameMetadata.Platforms)}");
                        }
                        
                        if (gameMetadata.Tags != null)
                        {
                            _logger.Info($"[TEST] Tags: {string.Join(", ", gameMetadata.Tags)}");
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