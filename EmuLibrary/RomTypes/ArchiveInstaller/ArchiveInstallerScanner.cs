using EmuLibrary.Settings;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace EmuLibrary.RomTypes.ArchiveInstaller
{
    internal class ArchiveInstallerScanner : RomTypeScanner
    {
        private readonly ILogger _logger;
        private readonly IPlayniteAPI _playnite;
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

        internal ArchiveInstallerScanner(IEmuLibrary emuLibrary) : base(emuLibrary)
        {
            _logger = emuLibrary.Logger;
            _playnite = emuLibrary.Playnite;
        }

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
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false
            };

            try
            {
                using (var enumerator = new PlayniteCommon.SafeFileEnumerator(mapping.SourcePath, "*.*", options))
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

        private GameMetadata CreateGameMetadata(Guid gameId, string name, ArchiveInstallerGameInfo gameInfo, EmulatorMapping mapping)
        {
            var result = new GameMetadata
            {
                GameId = gameInfo.AsGameId(),
                Name = name,
                IsInstalled = false,
                GameActions = new List<GameAction>(),
                Source = EmuLibrary.SourceName,
                PluginId = EmuLibrary.PluginId
            };

            if (mapping.Platform != null)
            {
                result.Platforms = new HashSet<MetadataProperty> { new MetadataProperty(mapping.Platform.Name, mapping.Platform.Id) };
            }
            
            return result;
        }
    }
}