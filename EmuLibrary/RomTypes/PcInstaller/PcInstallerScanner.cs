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
            
            try
            {
                // Use SafeFileEnumerator to handle network paths better
                var fileEnumerator = new SafeFileEnumerator(srcPath, "*.*", SearchOption.AllDirectories);
                int count = 0;
                
                foreach (var file in fileEnumerator)
                {
                    if (args.CancelToken.IsCancellationRequested)
                        yield break;
                        
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
                        
                        yield return new GameMetadata()
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
                        };
                    }
                }
                
                _logger.Info($"Found {count} PC game installers in {srcPath}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error scanning for PC game installers in {srcPath}");
            }
        }
        
        public override bool TryGetGameInfoBaseFromLegacyGameId(Game game, EmulatorMapping mapping, out ELGameInfo gameInfo)
        {
            // No legacy format for PC installers
            gameInfo = null;
            return false;
        }
        
        public override IEnumerable<Game> GetUninstalledGamesMissingSourceFiles(CancellationToken ct)
        {
            return _playniteAPI.Database.Games.TakeWhile(g => !ct.IsCancellationRequested)
                .Where(g =>
                {
                    if (g.PluginId != EmuLibrary.PluginId || g.IsInstalled)
                        return false;
                        
                    var info = g.GetELGameInfo();
                    if (info == null || info.RomType != RomType.PcInstaller)
                        return false;
                        
                    return !File.Exists((info as PcInstallerGameInfo).SourceFullPath);
                });
        }
        
        private bool IsPotentialInstaller(string path)
        {
            try
            {
                // Use cache if result exists
                if (_installerDetectionCache.TryGetValue(path, out bool result))
                    return result;
                
                // Limit cache size to prevent memory issues
                if (_installerDetectionCache.Count > CACHE_SIZE_LIMIT)
                {
                    // Clear half the cache when limit is reached
                    var keysToRemove = _installerDetectionCache.Keys.Take(CACHE_SIZE_LIMIT / 2).ToList();
                    foreach (var key in keysToRemove)
                        _installerDetectionCache.Remove(key);
                }
                
                string extension = Path.GetExtension(path).ToLower();
                string filename = Path.GetFileName(path).ToLower();
                
                // Check if it's an archive type we can handle (ISO, RAR, etc.)
                if (_archiveHandlerFactory.CanHandleFile(path))
                {
                    _installerDetectionCache[path] = true;
                    return true;
                }
                
                // Check file extension
                if (!_installerExtensions.Contains(extension))
                {
                    _installerDetectionCache[path] = false;
                    return false;
                }
                    
                // Exclude obvious non-installers
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
                        // Ignore file property reading errors, especially on network paths
                    }
                }
                
                _installerDetectionCache[path] = false;
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error checking if {path} is an installer");
                _installerDetectionCache[path] = false;
                return false;
            }
        }
        
        private string ExtractGameName(string filePath)
        {
            // Use cache if result exists
            if (_gameNameCache.TryGetValue(filePath, out string cachedName))
                return cachedName;
            
            // Limit cache size to prevent memory issues
            if (_gameNameCache.Count > CACHE_SIZE_LIMIT)
            {
                // Clear half the cache when limit is reached
                var keysToRemove = _gameNameCache.Keys.Take(CACHE_SIZE_LIMIT / 2).ToList();
                foreach (var key in keysToRemove)
                    _gameNameCache.Remove(key);
            }
            
            // Check if it's an archive we can handle
            var handler = _archiveHandlerFactory.GetHandler(filePath);
            if (handler != null)
            {
                var displayName = handler.GetArchiveDisplayName(filePath);
                if (!string.IsNullOrEmpty(displayName))
                {
                    _gameNameCache[filePath] = displayName;
                    return displayName;
                }
            }
            
            // If not a special archive or the handler failed, process the filename
            string fileName = Path.GetFileName(filePath);
            
            // Remove extension
            string name = Path.GetFileNameWithoutExtension(fileName);
            
            // Remove common prefixes/suffixes
            string[] prefixes = { "setup_", "setup-", "install_", "installer_" };
            foreach (var prefix in prefixes)
            {
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(prefix.Length);
                }
            }
            
            string[] suffixes = { "_setup", "-setup", "_install", "-install", "_installer", "-installer" };
            foreach (var suffix in suffixes)
            {
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - suffix.Length);
                }
            }
            
            // Remove version numbers
            name = Regex.Replace(name, @"[_\-\s]v?\d+(\.\d+)*[_\-\s]?$", "");
            
            // Replace underscores and dashes with spaces
            name = name.Replace('_', ' ').Replace('-', ' ');
            
            // Clean up multiple spaces
            name = Regex.Replace(name, @"\s+", " ").Trim();
            
            // Title case
            var textInfo = new System.Globalization.CultureInfo("en-US", false).TextInfo;
            name = textInfo.ToTitleCase(name.ToLower());
            
            // Store result in cache
            _gameNameCache[filePath] = name;
            return name;
        }
        
        /// <summary>
        /// Extracts game name from folder path instead of just the filename
        /// This provides better metadata matching since folder names often contain
        /// cleaner game titles than installer filenames
        /// </summary>
        private string ExtractGameNameFromPath(string filePath, string basePath)
        {
            try
            {
                // Get path relative to scan directory
                var relativePath = filePath.Replace(basePath, "").TrimStart('\\', '/');
                var pathParts = relativePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                
                // No folders in path, fall back to filename
                if (pathParts.Length <= 1)
                {
                    return ExtractGameName(Path.GetFileName(filePath));
                }
                
                // Try to use parent directory name first as it's often the game name
                string parentDir = pathParts[0];
                
                // Clean up folder name
                string name = parentDir;
                
                // Remove bracketed content like [GOG], [Steam], etc.
                name = Regex.Replace(name, @"\s*\[.*?\]\s*", " ");
                
                // Remove parenthesized content like (v1.2), (Complete Edition), etc.
                name = Regex.Replace(name, @"\s*\(.*?\)\s*", " ");
                
                // Replace underscores and dashes with spaces
                name = name.Replace('_', ' ').Replace('-', ' ');
                
                // Clean up multiple spaces
                name = Regex.Replace(name, @"\s+", " ").Trim();
                
                // Title case
                var textInfo = new System.Globalization.CultureInfo("en-US", false).TextInfo;
                name = textInfo.ToTitleCase(name.ToLower());
                
                // If after cleanup we have an empty or very short name, fall back to file-based name
                if (string.IsNullOrWhiteSpace(name) || name.Length < 3)
                {
                    return ExtractGameName(Path.GetFileName(filePath));
                }
                
                return name;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error extracting game name from path: {filePath}");
                return ExtractGameName(Path.GetFileName(filePath));
            }
        }
    }
}