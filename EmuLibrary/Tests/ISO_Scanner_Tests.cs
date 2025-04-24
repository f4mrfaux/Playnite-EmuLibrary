using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using EmuLibrary.RomTypes;
using EmuLibrary.RomTypes.ISOInstaller;
using EmuLibrary.Settings;

namespace EmuLibrary
{
    public class ISOScannerTest
    {
        private readonly IPlayniteAPI _playniteAPI;
        private readonly ILogger _logger;
        
        public ISOScannerTest(IPlayniteAPI playniteAPI, ILogger logger)
        {
            _playniteAPI = playniteAPI;
            _logger = logger;
        }
        
        /// <summary>
        /// Tests direct ISO file scanning in a directory
        /// </summary>
        public void TestDirectFileSearch(string directoryPath)
        {
            _logger.Info($"[TEST] Starting direct file test in: {directoryPath}");
            
            if (!Directory.Exists(directoryPath))
            {
                _logger.Error($"[TEST] Directory does not exist: {directoryPath}");
                return;
            }
            
            try
            {
                // List of common disc image extensions to test
                var discExtensions = new List<string> { 
                    "iso", "bin", "img", "cue", "nrg", "mds", "mdf",
                    "ISO", "BIN", "IMG", "CUE", "NRG", "MDS", "MDF" 
                };
                
                _logger.Info($"[TEST] Checking for files with extensions: {string.Join(", ", discExtensions)}");
                
                // Test 1: Direct file search with GetFiles
                _logger.Info("[TEST] Test 1: Direct GetFiles search");
                var allFiles = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories);
                _logger.Info($"[TEST] Found {allFiles.Length} total files");
                
                var discFiles = allFiles
                    .Where(f => discExtensions.Contains(Path.GetExtension(f).TrimStart('.')))
                    .ToList();
                    
                _logger.Info($"[TEST] Found {discFiles.Count} disc image files using direct extension check");
                
                if (discFiles.Count > 0)
                {
                    _logger.Info($"[TEST] Examples: {string.Join(", ", discFiles.Take(5).Select(Path.GetFileName))}");
                }
                
                // Test 2: Pattern-based search
                _logger.Info("[TEST] Test 2: Pattern-based search");
                var patternFiles = new List<string>();
                
                foreach (var ext in discExtensions)
                {
                    var pattern = $"*.{ext}";
                    var files = Directory.GetFiles(directoryPath, pattern, SearchOption.AllDirectories);
                    _logger.Info($"[TEST] Pattern {pattern}: {files.Length} files");
                    patternFiles.AddRange(files);
                }
                
                _logger.Info($"[TEST] Found {patternFiles.Count} total disc image files with pattern search");
                
                if (patternFiles.Count > 0)
                {
                    _logger.Info($"[TEST] Examples: {string.Join(", ", patternFiles.Take(5).Select(Path.GetFileName))}");
                    
                    // Check for differences
                    var uniqueFiles = patternFiles.Except(discFiles).ToList();
                    if (uniqueFiles.Count > 0)
                    {
                        _logger.Info($"[TEST] Files found in pattern search but not in direct search: {uniqueFiles.Count}");
                        _logger.Info($"[TEST] Examples: {string.Join(", ", uniqueFiles.Take(5).Select(Path.GetFileName))}");
                    }
                }
                
                // Test 3: Check subdirectories
                _logger.Info("[TEST] Test 3: Subdirectory check");
                var subdirs = Directory.GetDirectories(directoryPath, "*", SearchOption.TopDirectoryOnly);
                _logger.Info($"[TEST] Found {subdirs.Length} subdirectories at top level");
                
                foreach (var subdir in subdirs.Take(3))
                {
                    var subdirName = Path.GetFileName(subdir);
                    var subdirFiles = Directory.GetFiles(subdir, "*.*", SearchOption.AllDirectories)
                        .Where(f => discExtensions.Contains(Path.GetExtension(f).TrimStart('.')))
                        .ToList();
                        
                    _logger.Info($"[TEST] Subdir '{subdirName}': {subdirFiles.Count} disc image files");
                    
                    if (subdirFiles.Count > 0)
                    {
                        _logger.Info($"[TEST] Examples: {string.Join(", ", subdirFiles.Take(3).Select(Path.GetFileName))}");
                    }
                }
                
                // Test 4: Check game names from files
                _logger.Info("[TEST] Test 4: Game name extraction");
                if (discFiles.Count > 0)
                {
                    foreach (var file in discFiles.Take(5))
                    {
                        var parentFolder = Path.GetFileName(Path.GetDirectoryName(file));
                        var fileName = Path.GetFileNameWithoutExtension(file);
                        
                        _logger.Info($"[TEST] File: {Path.GetFileName(file)}");
                        _logger.Info($"[TEST]   Parent folder: {parentFolder}");
                        _logger.Info($"[TEST]   File name without extension: {fileName}");
                    }
                }
                
                _logger.Info("[TEST] Direct file test completed");
            }
            catch (Exception ex)
            {
                _logger.Error($"[TEST] Error in direct file test: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.Error($"[TEST] Inner exception: {ex.InnerException.Message}");
                }
            }
        }
        
        /// <summary>
        /// Tests scanning ISO files and creating GameMetadata objects
        /// </summary>
        public void TestScannerGameCreation(string directoryPath)
        {
            _logger.Info($"[TEST] Starting scanner game creation test for: {directoryPath}");
            
            if (!Directory.Exists(directoryPath))
            {
                _logger.Error($"[TEST] Directory does not exist: {directoryPath}");
                return;
            }
            
            try
            {
                // Create temporary mapping
                var mapping = new EmulatorMapping()
                {
                    MappingId = Guid.NewGuid(),
                    RomType = RomType.ISOInstaller,
                    SourcePath = directoryPath,
                    Enabled = true
                };
                
                // Find PC platform if possible
                var pcPlatform = _playniteAPI.Database.Platforms
                    .FirstOrDefault(p => p.Name == "PC" || p.Name == "Windows");
                    
                if (pcPlatform != null)
                {
                    mapping.PlatformId = pcPlatform.SpecificationId ?? pcPlatform.Id.ToString();
                    _logger.Info($"[TEST] Using platform: {pcPlatform.Name}");
                }
                else
                {
                    _logger.Warn("[TEST] PC platform not found in database");
                }
                
                // Create EmuLibrary instance for the scanner
                var emuLibrary = new EmuLibraryTester(_playniteAPI, _logger);
                
                // Create and run scanner
                var scanner = new ISOInstallerScanner(emuLibrary);
                var games = scanner.GetGames(mapping, new LibraryGetGamesArgs()).ToList();
                
                _logger.Info($"[TEST] Scanner found {games.Count} games");
                
                if (games.Count == 0)
                {
                    _logger.Error("[TEST] No games found by scanner");
                    return;
                }
                
                // Log details of found games
                int gameNumber = 0;
                foreach (var game in games)
                {
                    gameNumber++;
                    _logger.Info($"[TEST] Game {gameNumber}: {game.Name}");
                    _logger.Info($"[TEST]   GameId: {game.GameId}");
                    _logger.Info($"[TEST]   Source: {game.Source}");
                    _logger.Info($"[TEST]   IsInstalled: {game.IsInstalled}");
                    
                    if (game.Platforms != null)
                    {
                        var platformNames = string.Join(", ", game.Platforms.OfType<MetadataNameProperty>().Select(p => p.Name));
                        _logger.Info($"[TEST]   Platforms: {platformNames}");
                    }
                    else
                    {
                        _logger.Info("[TEST]   Platforms: <null>");
                    }
                    
                    if (game.Tags != null)
                    {
                        var tagNames = string.Join(", ", game.Tags.OfType<MetadataNameProperty>().Select(t => t.Name));
                        _logger.Info($"[TEST]   Tags: {tagNames}");
                    }
                    
                    if (game.GameActions != null)
                    {
                        _logger.Info($"[TEST]   Actions: {game.GameActions.Count}");
                        foreach (var action in game.GameActions)
                        {
                            _logger.Info($"[TEST]     {action.Name}: {action.Type}, IsPlayAction={action.IsPlayAction}");
                        }
                    }
                    
                    // Check if we can decode the game info
                    try
                    {
                        var gameInfo = ELGameInfo.FromGameMetadata<ELGameInfo>(game);
                        _logger.Info($"[TEST]   GameInfo type: {gameInfo.GetType().Name}");
                        _logger.Info($"[TEST]   RomType: {gameInfo.RomType}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[TEST]   Error decoding game info: {ex.Message}");
                    }
                    
                    _logger.Info("[TEST]   --------------------");
                }
                
                _logger.Info("[TEST] Scanner game creation test completed");
            }
            catch (Exception ex)
            {
                _logger.Error($"[TEST] Error in scanner game creation test: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.Error($"[TEST] Inner exception: {ex.InnerException.Message}");
                }
            }
        }
    }
    
    // Test implementation of IEmuLibrary
    internal class EmuLibraryTester : IEmuLibrary
    {
        public ILogger Logger { get; }
        public IPlayniteAPI Playnite { get; }
        public Settings.Settings Settings { get; }
        
        public EmuLibraryTester(IPlayniteAPI playniteAPI, ILogger logger)
        {
            Playnite = playniteAPI;
            Logger = logger;
            Settings = new Settings.Settings();
        }
        
        public RomTypeScanner GetScanner(RomType romType)
        {
            if (romType == RomType.ISOInstaller)
                return new ISOInstallerScanner(this);
                
            return null;
        }
        
        public string GetPluginUserDataPath()
        {
            return Playnite.Paths.ExtensionsDataPath;
        }
    }
}