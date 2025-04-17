using EmuLibrary.PlayniteCommon;
using EmuLibrary.Settings;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using EmuLibrary.RomTypes.PCInstaller;

namespace EmuLibrary.RomTypes.ArchiveInstaller
{
    internal class ArchiveInstallerScanner : RomTypeScanner
    {
        private readonly ILogger _logger;
        private readonly IPlayniteAPI _playnite;
        private readonly IEmuLibrary _emuLibrary;
        private readonly HashSet<string> _supportedArchiveExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".rar", ".r00", ".r01", ".r02", ".zip", ".7z"
        };

        private readonly HashSet<string> _supportedIsoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".iso", ".bin", ".cue", ".mdf", ".mds", ".img"
        };

        private readonly Regex _multiPartRarRegex = new Regex(@"\.part0*(\d+)\.rar$", RegexOptions.IgnoreCase);
        private readonly Regex _volumeRarRegex = new Regex(@"\.r(\d+)$", RegexOptions.IgnoreCase);
        
        // Regex patterns for detecting content types
        private static readonly Regex _updatePattern = new Regex(@"(?i)(update|patch|hotfix|fix|v\d+(\.\d+)+)", RegexOptions.Compiled);
        private static readonly Regex _dlcPattern = new Regex(@"(?i)(dlc|addon|add-?on|content|season pass)", RegexOptions.Compiled);
        private static readonly Regex _expansionPattern = new Regex(@"(?i)(expansion|expans\.?|xpac|xpack|xp\d+)", RegexOptions.Compiled);
        private static readonly Regex _versionPattern = new Regex(@"(?i)(?:v|ver|version)\.?\s*(\d+(?:\.\d+){1,3})", RegexOptions.Compiled);
        
        // Common naming patterns for DLC and expansions
        private static readonly string[] _commonDlcPhrases = new[] { "dlc", "addon", "add-on", "content pack", "season pass" };
        private static readonly string[] _commonExpansionPhrases = new[] { "expansion", "expansion pack", "xpac", "xpack" };

        internal ArchiveInstallerScanner(IEmuLibrary emuLibrary) : base(emuLibrary)
        {
            _logger = emuLibrary.Logger;
            _playnite = emuLibrary.Playnite;
            _emuLibrary = emuLibrary;
        }

        public override RomType RomType => RomType.ArchiveInstaller;

        public override Guid LegacyPluginId => 
            Guid.Parse("00000000-0000-0000-0000-000000000000"); // Not applicable, new type

        public override IEnumerable<GameMetadata> GetGames(EmulatorMapping mapping, LibraryGetGamesArgs args)
        {
            _logger.Debug($"Scanning for Archive Installer games in {mapping.SourcePath}");
            
            if (!Directory.Exists(mapping.SourcePath))
            {
                _logger.Error($"Source path does not exist: {mapping.SourcePath}");
                yield break;
            }

            // Create a SafeFileEnumerator to handle network issues gracefully
            // Use SearchOption instead of EnumerationOptions for .NET Framework compatibility
            var searchOption = SearchOption.AllDirectories;

            try
            {
                using (var enumerator = new PlayniteCommon.SafeFileEnumerator(mapping.SourcePath, "*.*", searchOption))
                {
                    var multipartArchives = new Dictionary<string, List<string>>();
                    var allArchives = new List<string>();
                    
                    // First scan: identify all archive files and group multi-part archives
                    foreach (var file in enumerator)
                    {
                        try
                        {
                            if (args.CancelToken.IsCancellationRequested)
                                yield break;

                            var extension = Path.GetExtension(file.FullName);
                            if (!_supportedArchiveExtensions.Contains(extension))
                                continue;

                            var fileName = Path.GetFileName(file.FullName);
                            var filePath = file.FullName;
                            
                            // Check if this is a multi-part RAR archive
                            var multiPartMatch = _multiPartRarRegex.Match(fileName);
                            if (multiPartMatch.Success)
                            {
                                // For part001.rar style archives, we want to group by base name
                                var baseName = fileName.Substring(0, fileName.LastIndexOf(".part"));
                                var baseKey = Path.Combine(Path.GetDirectoryName(filePath), baseName);
                                
                                if (!multipartArchives.ContainsKey(baseKey))
                                    multipartArchives[baseKey] = new List<string>();
                                
                                multipartArchives[baseKey].Add(filePath);
                                continue;
                            }
                            
                            // Check if this is a r00, r01, etc. style RAR volume
                            var volumeMatch = _volumeRarRegex.Match(fileName);
                            if (volumeMatch.Success || extension.Equals(".rar", StringComparison.OrdinalIgnoreCase))
                            {
                                // For .rar, .r00, .r01 style archives
                                var baseName = fileName;
                                if (extension.Equals(".rar", StringComparison.OrdinalIgnoreCase))
                                {
                                    baseName = Path.GetFileNameWithoutExtension(fileName);
                                }
                                else
                                {
                                    // For r## files, get the name without the volume extension
                                    baseName = fileName.Substring(0, fileName.LastIndexOf('.'));
                                }
                                
                                var baseKey = Path.Combine(Path.GetDirectoryName(filePath), baseName);
                                
                                if (!multipartArchives.ContainsKey(baseKey))
                                    multipartArchives[baseKey] = new List<string>();
                                
                                multipartArchives[baseKey].Add(filePath);
                                continue;
                            }
                            
                            // Single archive files
                            allArchives.Add(filePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Error processing file {file.FullName}: {ex.Message}");
                        }
                    }
                    
                    // Process multi-part archives
                    foreach (var archive in multipartArchives)
                    {
                        try
                        {
                            if (args.CancelToken.IsCancellationRequested)
                                yield break;
                                
                            var parts = archive.Value.OrderBy(f => f).ToList();
                            if (parts.Count == 0)
                                continue;
                                
                            // Find the main archive file (.rar or .part01.rar)
                            string mainArchive = null;
                            foreach (var part in parts)
                            {
                                var ext = Path.GetExtension(part);
                                if (ext.Equals(".rar", StringComparison.OrdinalIgnoreCase))
                                {
                                    // If we have a .rar file, it's probably the main one
                                    if (mainArchive == null || !Path.GetFileName(mainArchive).Contains(".part"))
                                    {
                                        mainArchive = part;
                                    }
                                }
                            }
                            
                            // If we couldn't find a clear main archive, use the first part
                            if (mainArchive == null && parts.Count > 0)
                            {
                                mainArchive = parts[0];
                            }
                            
                            if (mainArchive == null)
                                continue;
                                
                            var gameId = Guid.NewGuid();
                            var gameName = Path.GetFileNameWithoutExtension(mainArchive)
                                .Replace('.', ' ')
                                .Replace('_', ' ');
                                
                            var relativePath = mainArchive.Substring(mapping.SourcePath.Length).TrimStart(Path.DirectorySeparatorChar);
                            
                            var gameInfo = new ArchiveInstallerGameInfo
                            {
                                MappingId = mapping.MappingId,
                                SourcePath = relativePath,
                                MainArchivePath = relativePath,
                                ArchiveParts = parts
                                    .Select(p => p.Substring(mapping.SourcePath.Length).TrimStart(Path.DirectorySeparatorChar))
                                    .ToList()
                            };
                            
                            yield return CreateGameMetadata(gameId, gameName, gameInfo, mapping);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Error processing multi-part archive {archive.Key}: {ex.Message}");
                        }
                    }
                    
                    // Process single archive files
                    foreach (var archivePath in allArchives)
                    {
                        try
                        {
                            if (args.CancelToken.IsCancellationRequested)
                                yield break;
                                
                            var gameId = Guid.NewGuid();
                            var gameName = Path.GetFileNameWithoutExtension(archivePath)
                                .Replace('.', ' ')
                                .Replace('_', ' ');
                                
                            var relativePath = archivePath.Substring(mapping.SourcePath.Length).TrimStart(Path.DirectorySeparatorChar);
                            
                            var gameInfo = new ArchiveInstallerGameInfo
                            {
                                MappingId = mapping.MappingId,
                                SourcePath = relativePath,
                                MainArchivePath = relativePath,
                                ArchiveParts = new List<string> { relativePath }
                            };
                            
                            yield return CreateGameMetadata(gameId, gameName, gameInfo, mapping);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Error processing archive {archivePath}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error scanning source path: {mapping.SourcePath}");
            }
        }

        public override bool TryGetGameInfoBaseFromLegacyGameId(Game game, EmulatorMapping mapping, out ELGameInfo gameInfo)
        {
            // Not applicable for this new type
            gameInfo = null;
            return false;
        }
        
        public override IEnumerable<Game> GetUninstalledGamesMissingSourceFiles(CancellationToken ct)
        {
            _logger.Info("Checking for archive installer games with missing source files");
            
            try
            {
                // Use BufferedUpdate for better performance
                using (_playnite.Database.BufferedUpdate())
                {
                    // First filter by plugin ID and installation status to reduce the collection size
                    var filteredGames = _playnite.Database.Games
                        .Where(g => g.PluginId == EmuLibrary.PluginId && !g.IsInstalled)
                        .ToList();
                    
                    return filteredGames
                        .TakeWhile(g => !ct.IsCancellationRequested)
                        .Where(g =>
                        {
                            try
                            {
                                var info = g.GetELGameInfo();
                                if (info.RomType != RomType.ArchiveInstaller)
                                    return false;

                                var archiveInfo = info as ArchiveInstallerGameInfo;
                                
                                // For multi-part archives, check if any part is missing
                                if (archiveInfo.ArchiveParts != null && archiveInfo.ArchiveParts.Count > 0)
                                {
                                    var mapping = archiveInfo.Mapping;
                                    if (mapping == null)
                                    {
                                        _logger.Warn($"No mapping found for game {g.Name}");
                                        return true; // Consider missing if we can't find the mapping
                                    }
                                    
                                    // Check if any part is missing
                                    foreach (var part in archiveInfo.ArchiveParts)
                                    {
                                        var partPath = Path.Combine(mapping.SourcePath, part);
                                        if (!File.Exists(partPath))
                                        {
                                            _logger.Info($"Source file missing for game {g.Name}: {partPath}");
                                            return true; // Consider missing if any part is missing
                                        }
                                    }
                                    
                                    return false; // All parts exist
                                }
                                else
                                {
                                    // Fallback to checking the main source path
                                    var sourceExists = File.Exists(archiveInfo.SourceFullPath);
                                    
                                    if (!sourceExists)
                                    {
                                        _logger.Info($"Source file missing for game {g.Name}: {archiveInfo.SourceFullPath}");
                                    }
                                    
                                    return !sourceExists;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"Error checking source file for game {g.Name}: {ex.Message}");
                                return false;
                            }
                        });
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in GetUninstalledGamesMissingSourceFiles: {ex.Message}");
                return Enumerable.Empty<Game>();
            }
        }

        /// <summary>
        /// Detects content type, version and other metadata from file and directory names
        /// </summary>
        /// <param name="fileName">Archive file name</param>
        /// <param name="dirName">Directory name containing the archive</param>
        /// <returns>Tuple with ContentType, Version, and ContentDescription</returns>
        private (PCInstaller.ContentType contentType, string version, string contentDescription) 
            DetectContentTypeAndVersion(string fileName, string dirName)
        {
            // Default values
            var contentType = PCInstaller.ContentType.BaseGame;
            string version = null;
            string contentDescription = null;
            
            // Combine all relevant names for searching
            var combinedText = $"{fileName} {dirName}".ToLowerInvariant();
            
            // Check for version information
            var versionMatch = _versionPattern.Match(combinedText);
            if (versionMatch.Success && versionMatch.Groups.Count > 1)
            {
                version = versionMatch.Groups[1].Value;
            }
            
            // Determine content type based on keywords in file and directory names
            if (_updatePattern.IsMatch(combinedText))
            {
                contentType = PCInstaller.ContentType.Update;
                contentDescription = "Game Update";
                
                // Try to extract specific update information
                if (string.IsNullOrEmpty(version))
                {
                    // Try to extract version from patterns like "Update 1.2" or "Patch 2.0"
                    var updateVersionMatch = Regex.Match(combinedText, @"(?i)(?:update|patch)\s*(\d+(?:\.\d+){0,3})");
                    if (updateVersionMatch.Success && updateVersionMatch.Groups.Count > 1)
                    {
                        version = updateVersionMatch.Groups[1].Value;
                        contentDescription = $"Update v{version}";
                    }
                }
                else
                {
                    contentDescription = $"Update v{version}";
                }
            }
            else if (_expansionPattern.IsMatch(combinedText))
            {
                contentType = PCInstaller.ContentType.Expansion;
                contentDescription = "Expansion Pack";
                
                // Try to extract specific expansion name
                foreach (var phrase in _commonExpansionPhrases)
                {
                    if (combinedText.Contains(phrase))
                    {
                        var index = combinedText.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
                        if (index > 0)
                        {
                            // Extract content before the phrase
                            var beforePhrase = combinedText.Substring(0, index).Trim();
                            if (!string.IsNullOrEmpty(beforePhrase))
                            {
                                contentDescription = beforePhrase + " Expansion";
                            }
                        }
                        else if (index == 0 && combinedText.Length > phrase.Length)
                        {
                            // Extract content after the phrase
                            var afterPhrase = combinedText.Substring(phrase.Length).Trim();
                            if (!string.IsNullOrEmpty(afterPhrase))
                            {
                                contentDescription = "Expansion: " + afterPhrase;
                            }
                        }
                        break;
                    }
                }
            }
            else if (_dlcPattern.IsMatch(combinedText))
            {
                contentType = PCInstaller.ContentType.DLC;
                contentDescription = "Downloadable Content";
                
                // Try to extract specific DLC name
                foreach (var phrase in _commonDlcPhrases)
                {
                    if (combinedText.Contains(phrase))
                    {
                        var index = combinedText.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
                        if (index > 0)
                        {
                            // Extract content before the phrase
                            var beforePhrase = combinedText.Substring(0, index).Trim();
                            if (!string.IsNullOrEmpty(beforePhrase))
                            {
                                contentDescription = beforePhrase + " DLC";
                            }
                        }
                        else if (index == 0 && combinedText.Length > phrase.Length)
                        {
                            // Extract content after the phrase
                            var afterPhrase = combinedText.Substring(phrase.Length).Trim();
                            if (!string.IsNullOrEmpty(afterPhrase))
                            {
                                contentDescription = "DLC: " + afterPhrase;
                            }
                        }
                        break;
                    }
                }
            }
            
            return (contentType, version, contentDescription);
        }
            
        /// <summary>
        /// Extracts base game name from a content name by removing content type indicators
        /// </summary>
        /// <param name="gameName">The full game name including content indicators</param>
        /// <returns>Extracted base game name or null if can't be determined</returns>
        private string ExtractBaseGameName(string gameName)
        {
            if (string.IsNullOrEmpty(gameName))
                return null;
                
            // Common separators that might appear between base game name and content type
            var separators = new[] { " - ", ": ", " – ", "_", ":" };
            
            // Common suffix patterns for various content types
            var contentPatterns = new Regex[]
            {
                new Regex(@"(?i)\s*(DLC|Downloadable Content|Season Pass|Add-?on|Content Pack).*$"),
                new Regex(@"(?i)\s*(Update|Patch|Hotfix)\s*(\d+(\.\d+)*)?\s*$"),
                new Regex(@"(?i)\s*(Expansion|Expansion Pack|XPAC)\s*.*$"),
                new Regex(@"(?i)\s*v\d+(\.\d+)*\s*$")  // Version number
            };
            
            // First try to split by common separators
            foreach (var separator in separators)
            {
                if (gameName.Contains(separator))
                {
                    var parts = gameName.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        // Assume the first part is the base game name
                        var baseName = parts[0].Trim();
                        if (!string.IsNullOrEmpty(baseName))
                            return baseName;
                    }
                }
            }
            
            // If no separators found, try to remove content suffixes
            var result = gameName;
            foreach (var pattern in contentPatterns)
            {
                result = pattern.Replace(result, "").Trim();
            }
            
            // If we've removed something and have a valid name left
            if (result != gameName && !string.IsNullOrEmpty(result))
                return result;
                
            // If all else fails, return null to indicate we couldn't extract the base name
            return null;
        }
        
        private GameMetadata CreateGameMetadata(Guid gameId, string name, ArchiveInstallerGameInfo gameInfo, EmulatorMapping mapping)
        {
            // Detect content type, version, and content description
            var fileName = Path.GetFileName(gameInfo.MainArchivePath);
            var dirName = Path.GetDirectoryName(gameInfo.MainArchivePath);
            var (contentType, version, contentDescription) = DetectContentTypeAndVersion(fileName, dirName);
            
            // Set content type information in the game info
            gameInfo.ContentType = contentType;
            gameInfo.Version = version;
            gameInfo.ContentDescription = contentDescription;
            
            // For content other than base games, try to extract the base game name
            string baseGameName = null;
            string parentGameId = null;
            
            if (contentType != PCInstaller.ContentType.BaseGame)
            {
                // Try to extract base game name by removing content indicators
                baseGameName = ExtractBaseGameName(name);
                
                if (!string.IsNullOrEmpty(baseGameName))
                {
                    _logger.Debug($"Extracted base game name '{baseGameName}' from '{name}'");
                    
                    // Try to find a matching base game in the database
                    var potentialBaseGames = _playnite.Database.Games
                        .Where(g => g.PluginId == EmuLibrary.PluginId && 
                                (g.Name.Equals(baseGameName, StringComparison.OrdinalIgnoreCase) ||
                                 g.Name.StartsWith(baseGameName, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                        
                    if (potentialBaseGames.Count > 0)
                    {
                        // Find exact match first, or first game that starts with the base name
                        var baseGame = potentialBaseGames.FirstOrDefault(g => 
                            g.Name.Equals(baseGameName, StringComparison.OrdinalIgnoreCase)) ??
                            potentialBaseGames.First();
                            
                        // Make sure it's an ArchiveInstaller game
                        try
                        {
                            var baseGameInfo = baseGame.GetELGameInfo();
                            if (baseGameInfo != null && 
                                (baseGameInfo.RomType == RomType.ArchiveInstaller || 
                                baseGameInfo.RomType == RomType.ISOInstaller || 
                                baseGameInfo.RomType == RomType.PCInstaller))
                            {
                                var pcBaseGameInfo = baseGameInfo as ArchiveInstallerGameInfo;
                                if (pcBaseGameInfo != null && 
                                    pcBaseGameInfo.ContentType == PCInstaller.ContentType.BaseGame)
                                {
                                    parentGameId = baseGame.GameId;
                                    gameInfo.ParentGameId = parentGameId;
                                    _logger.Info($"Found parent game '{baseGame.Name}' for '{name}'");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Error checking potential base game {baseGame.Name}: {ex.Message}");
                        }
                    }
                }
            }
            
            // Adjust game name for DLC, Updates, etc.
            string displayName = name;
            if (contentType != PCInstaller.ContentType.BaseGame && 
                !string.IsNullOrEmpty(contentDescription))
            {
                // For content that's not a base game, include content description in name
                // If we found a parent game, use its name as a prefix
                if (!string.IsNullOrEmpty(parentGameId))
                {
                    var parentGame = _playnite.Database.Games.FirstOrDefault(g => g.GameId == parentGameId);
                    if (parentGame != null)
                    {
                        displayName = $"{parentGame.Name} - {contentDescription}";
                    }
                    else
                    {
                        displayName = $"{name} - {contentDescription}";
                    }
                }
                else
                {
                    displayName = $"{name} - {contentDescription}";
                }
            }
            
            // Add version if available
            if (!string.IsNullOrEmpty(version) && 
                !displayName.Contains(version))
            {
                displayName = $"{displayName} (v{version})";
            }

            var result = new GameMetadata
            {
                GameId = gameInfo.AsGameId(),
                Name = displayName,
                IsInstalled = false,
                GameActions = new List<GameAction>(),
                Source = EmuLibrary.SourceName,
                PluginId = EmuLibrary.PluginId
            };

            if (mapping.Platform != null)
            {
                result.Platforms = new HashSet<MetadataProperty> { new MetadataProperty(mapping.Platform.Name, mapping.Platform.Id) };
            }
            
            // Add content type as tag
            result.Tags = new HashSet<MetadataProperty>();
            switch (contentType)
            {
                case PCInstaller.ContentType.Update:
                    result.Tags.Add(new MetadataNameProperty("Update"));
                    break;
                case PCInstaller.ContentType.DLC:
                    result.Tags.Add(new MetadataNameProperty("DLC"));
                    break;
                case PCInstaller.ContentType.Expansion:
                    result.Tags.Add(new MetadataNameProperty("Expansion"));
                    break;
            }
            
            // Add version info to description
            if (!string.IsNullOrEmpty(version))
            {
                result.Description = $"Version: {version}\n";
                if (!string.IsNullOrEmpty(contentDescription))
                {
                    result.Description += $"Content: {contentDescription}";
                }
            }
            else if (!string.IsNullOrEmpty(contentDescription))
            {
                result.Description = $"Content: {contentDescription}";
            }
            
            // Add parent-child relationship info
            if (!string.IsNullOrEmpty(parentGameId))
            {
                var parentGame = _playnite.Database.Games.FirstOrDefault(g => g.GameId == parentGameId);
                if (parentGame != null)
                {
                    // Store the parent-child relationship in custom properties
                    // Fixed: GameDependencies property doesn't exist in this API version
                    // We'll use GameMetadata's properties instead
                    result.Properties.Add("DependentGameId", parentGame.Id.ToString());
                    
                    if (result.Description == null)
                    {
                        result.Description = $"Related to: {parentGame.Name}";
                    }
                    else
                    {
                        result.Description += $"\nRelated to: {parentGame.Name}";
                    }
                }
            }
            
            return result;
        }
    }
}