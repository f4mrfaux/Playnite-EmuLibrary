using EmuLibrary.PlayniteCommon;
using EmuLibrary.Settings;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace EmuLibrary.RomTypes.PcInstaller
{
    internal class PcInstallerScanner : RomTypeScanner
    {
        private readonly IPlayniteAPI _playniteAPI;
        private readonly ILogger _logger;
        private readonly IEmuLibrary _emuLibrary;
        private readonly Handlers.ArchiveHandlerFactory _archiveHandlerFactory;
        
        public override RomType RomType => RomType.PcInstaller;
        public override Guid LegacyPluginId => EmuLibrary.PluginId;
        
        // File extensions for PC game installers
        private readonly string[] _installerExtensions = new[] { ".exe", ".msi", ".iso", ".rar" };
        
        // Common installer patterns to detect
        private readonly string[] _installerPatterns = new[]
        {
            @"setup[_\-\s]?.*\.exe",
            @"install[_\-\s]?.*\.exe",
            @".*[_\-\s]?setup\.exe",
            @".*[_\-\s]?install\.exe",
            @".*[_\-\s]?installer\.exe"
        };
        
        // Non-installer patterns to exclude
        private readonly string[] _excludePatterns = new[]
        {
            @"unins.*\.exe",
            @"uninst.*\.exe",
            @"patch.*\.exe",
            @"update.*\.exe"
        };
        
        // Cache for installer detection to improve performance
        private readonly Dictionary<string, bool> _installerDetectionCache = new Dictionary<string, bool>();
        
        // Cache for game name extraction to improve performance
        private readonly Dictionary<string, string> _gameNameCache = new Dictionary<string, string>();
        
        // Limit on cache size to prevent memory issues
        private const int CACHE_SIZE_LIMIT = 10000;
        
        public PcInstallerScanner(IEmuLibrary emuLibrary) : base(emuLibrary)
        {
            _playniteAPI = emuLibrary.Playnite;
            _logger = emuLibrary.Logger;
            _archiveHandlerFactory = new Handlers.ArchiveHandlerFactory(_logger);
        }
        
        public override IEnumerable<GameMetadata> GetGames(EmulatorMapping mapping, LibraryGetGamesArgs args)
        {
            if (args.CancelToken.IsCancellationRequested)
                yield break;
                
            var srcPath = mapping.SourcePath;
            
            if (!Directory.Exists(srcPath))
            {
                _logger.Warn($"Source directory does not exist: {srcPath}");
                yield break;
            }
            
            _logger.Info($"Scanning for PC game installers in {srcPath}");
            
            // Create a list to store the results first, then we'll yield them after the try-catch
            var results = new List<GameMetadata>();
            int count = 0;
            
            try
            {
                // Use SafeFileEnumerator to handle network paths better
                var fileEnumerator = new SafeFileEnumerator(srcPath, "*.*", SearchOption.AllDirectories);
                
                foreach (var file in fileEnumerator)
                {
                    if (args.CancelToken.IsCancellationRequested)
                        break;
                        
                    // Check if the file is a potential installer
                    if (IsPotentialInstaller(file.FullName))
                    {
                        count++;
                        string relativePath = file.FullName.Replace(srcPath, "").TrimStart('\\', '/');
                        
                        // If we should use folder names for better metadata matching
                        string gameName;
                        if (EmuLibrary.Settings.UseSourceFolderNamesForMetadata)
                        {
                            // Try to use parent folder name for better metadata matching
                            gameName = ExtractGameNameFromPath(file.FullName, srcPath);
                        }
                        else
                        {
                            // Use traditional filename-based extraction
                            gameName = ExtractGameName(file.Name);
                        }
                        
                        var info = new PcInstallerGameInfo()
                        {
                            MappingId = mapping.MappingId,
                            SourcePath = relativePath
                        };
                        
                        results.Add(new GameMetadata()
                        {
                            Source = EmuLibrary.SourceName,
                            Name = gameName,
                            IsInstalled = false,
                            GameId = info.AsGameId(),
                            Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(mapping.Platform.Name) },
                            InstallSize = (ulong)new FileInfo(file.FullName).Length,
                            GameActions = new List<GameAction>() { new GameAction()
                            {
                                Name = "Install Game",
                                Type = GameActionType.Emulator,
                                EmulatorId = mapping.EmulatorId,
                                EmulatorProfileId = mapping.EmulatorProfileId,
                                IsPlayAction = true
                            }}
                        });
                    }
                }
                
                _logger.Info($"Found {count} PC game installer(s)");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error scanning for PC game installers: {ex.Message}");
            }
            
            // Now yield the results outside the try-catch block
            foreach (var result in results)
            {
                yield return result;
            }
        }
        
        public override bool TryGetGameInfoBaseFromLegacyGameId(Game game, EmulatorMapping mapping, out ELGameInfo gameInfo)
        {
            gameInfo = null;
            return false; // No legacy support for PC installers
        }
        
        public override IEnumerable<Game> GetUninstalledGamesMissingSourceFiles(CancellationToken ct)
        {
            var relevantGames = _playniteAPI.Database.Games
                .Where(g => !g.IsInstalled && g.PluginId == EmuLibrary.PluginId && g.GameId.Contains(RomType.ToString()))
                .ToList();
            
            // Create a list to collect games that need to be returned
            var gamesToCleanup = new List<Game>();

            foreach (var game in relevantGames)
            {
                if (ct.IsCancellationRequested)
                    break;

                try
                {
                    var info = game.GetELGameInfo() as PcInstallerGameInfo;
                    if (info != null)
                    {
                        var mapping = info.Mapping;
                        if (mapping == null)
                        {
                            _logger.Warn($"Mapping {info.MappingId} was not found. Will clean up the game {game.Name} [{game.GameId}]");
                            gamesToCleanup.Add(game);
                            continue;
                        }

                        string sourceFullPath = info.SourceFullPath;
                        if (!File.Exists(sourceFullPath))
                        {
                            _logger.Warn($"Source file {sourceFullPath} no longer exists. Will clean up the game {game.Name} [{game.GameId}]");
                            gamesToCleanup.Add(game);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e, $"Error checking PC installer game {game.Name} [{game.GameId}]");
                }
            }
            
            // Return the collected games outside of the try/catch block
            return gamesToCleanup;
        }
        
        private bool IsPotentialInstaller(string path)
        {
            // Check cache first to improve performance
            if (_installerDetectionCache.TryGetValue(path, out bool result))
            {
                return result;
            }
            
            try
            {
                // Clean cache if it's getting too large
                if (_installerDetectionCache.Count > CACHE_SIZE_LIMIT)
                {
                    _installerDetectionCache.Clear();
                }
                
                var filename = Path.GetFileName(path).ToLower();
                var extension = Path.GetExtension(path).ToLower();
                
                // Quickly reject extensions we don't support
                if (!_installerExtensions.Contains(extension))
                {
                    _installerDetectionCache[path] = false;
                    return false;
                }
                
                // Check against exclude patterns first
                foreach (var pattern in _excludePatterns)
                {
                    if (Regex.IsMatch(filename, pattern, RegexOptions.IgnoreCase))
                    {
                        _installerDetectionCache[path] = false;
                        return false;
                    }
                }
                
                // Check if filename matches installer patterns
                foreach (var pattern in _installerPatterns)
                {
                    if (Regex.IsMatch(filename, pattern, RegexOptions.IgnoreCase))
                    {
                        _installerDetectionCache[path] = true;
                        return true;
                    }
                }
                
                // Try to check file properties for clues
                if (extension == ".exe" && EmuLibrary.Settings.AutoDetectPcInstallers)
                {
                    try
                    {
                        var versionInfo = FileVersionInfo.GetVersionInfo(path);
                        
                        // Check if it mentions "setup" or "install" in properties
                        if (versionInfo.FileDescription?.IndexOf("setup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            versionInfo.FileDescription?.IndexOf("install", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            versionInfo.ProductName?.IndexOf("setup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            versionInfo.ProductName?.IndexOf("install", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _installerDetectionCache[path] = true;
                            return true;
                        }
                    }
                    catch
                    {
                        // Ignore errors reading file version info
                    }
                }
                
                // For ISO files, we support these as potential game installers
                if (extension == ".iso")
                {
                    _installerDetectionCache[path] = true;
                    return true;
                }
                
                // For RAR files, we need to check if they might contain an ISO or installer
                if (extension == ".rar")
                {
                    // Check if it's part of a multi-part RAR archive
                    if (Regex.IsMatch(filename, @"\.part\d+\.rar$", RegexOptions.IgnoreCase) ||
                        (filename.Contains(".r") && Regex.IsMatch(filename, @"\.r\d+$", RegexOptions.IgnoreCase)))
                    {
                        _installerDetectionCache[path] = true;
                        return true;
                    }
                    
                    // For regular RAR files, check if they're supported by our handler
                    var handler = _archiveHandlerFactory.GetHandler(path);
                    if (handler != null)
                    {
                        _installerDetectionCache[path] = true;
                        return true;
                    }
                }
                
                // If we get here, it's not a supported installer
                _installerDetectionCache[path] = false;
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error checking if file is a potential installer: {path}");
                return false;
            }
        }
        
        private string ExtractGameName(string fileName)
        {
            // Check cache first
            if (_gameNameCache.TryGetValue(fileName, out string cachedName))
            {
                return cachedName;
            }
            
            try
            {
                // Clean cache if it's getting too large
                if (_gameNameCache.Count > CACHE_SIZE_LIMIT)
                {
                    _gameNameCache.Clear();
                }
                
                // Remove file extension
                string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                
                // Remove common installer prefixes/suffixes
                nameWithoutExt = Regex.Replace(nameWithoutExt, @"(?i)setup[_\-\s]", "");
                nameWithoutExt = Regex.Replace(nameWithoutExt, @"(?i)install[_\-\s]", "");
                nameWithoutExt = Regex.Replace(nameWithoutExt, @"(?i)[_\-\s]setup$", "");
                nameWithoutExt = Regex.Replace(nameWithoutExt, @"(?i)[_\-\s]install(er)?$", "");
                
                // Replace underscores and periods with spaces
                nameWithoutExt = nameWithoutExt.Replace('_', ' ').Replace('.', ' ');
                
                // Remove version numbers
                nameWithoutExt = Regex.Replace(nameWithoutExt, @"\b[vV]?\d+(\.\d+)*[a-z]?\b", "");
                
                // Clean up extra spaces
                nameWithoutExt = Regex.Replace(nameWithoutExt, @"\s+", " ").Trim();
                
                // Apply title casing
                nameWithoutExt = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(nameWithoutExt.ToLower());
                
                // Cache the result
                _gameNameCache[fileName] = nameWithoutExt;
                
                return nameWithoutExt;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error extracting game name from filename: {fileName}");
                return fileName; // Return original as fallback
            }
        }
        
        private string ExtractGameNameFromPath(string filePath, string basePath)
        {
            try
            {
                // Get the directory path
                string dirPath = Path.GetDirectoryName(filePath);
                
                // If it's in the base directory, use the file name
                if (dirPath.Equals(basePath, StringComparison.OrdinalIgnoreCase))
                {
                    return ExtractGameName(Path.GetFileName(filePath));
                }
                
                // Get the relative path from the base
                string relativePath = dirPath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                
                // Get the parent folder name
                string parentFolder = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                
                // Clean up the parent folder name
                parentFolder = parentFolder.Replace('_', ' ').Replace('.', ' ');
                parentFolder = Regex.Replace(parentFolder, @"\s+", " ").Trim();
                
                // Apply title casing
                parentFolder = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(parentFolder.ToLower());
                
                return parentFolder;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error extracting game name from path: {filePath}");
                return Path.GetFileNameWithoutExtension(filePath); // Fallback to filename
            }
        }
    }
}