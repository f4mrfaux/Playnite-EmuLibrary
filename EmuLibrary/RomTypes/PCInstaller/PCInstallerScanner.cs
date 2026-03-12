using EmuLibrary.PlayniteCommon;
using EmuLibrary.Settings;
using EmuLibrary.Util;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;

namespace EmuLibrary.RomTypes.PCInstaller
{
    internal class PCInstallerScanner : RomTypeScanner
    {
        private readonly IPlayniteAPI _playniteAPI;

        public override RomType RomType => RomType.PCInstaller;
        public override Guid LegacyPluginId => EmuLibrary.PluginId;

        public PCInstallerScanner(IEmuLibrary emuLibrary) : base(emuLibrary)
        {
            _playniteAPI = emuLibrary.Playnite;
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
            var installerExtensions = new List<string> { "exe", "iso", "zip", "rar", "7z" };
            
            SafeFileEnumerator fileEnumerator;
            var gameMetadataBatch = new List<GameMetadata>();

            #region Import discovered installers
            var sourcedGames = new List<GameMetadata>();
            try
            {
                fileEnumerator = new SafeFileEnumerator(srcPath, "*.*", SearchOption.AllDirectories);
                _emuLibrary.Logger.Info($"Scanning for PC installers in {srcPath}");

                var normalizedNameCache = new Dictionary<string, string>();
                var extractedContentCache = new Dictionary<string, bool>();

                // Group files by folder for multi-disc support (especially for ISO files)
                var filesByFolder = new Dictionary<string, List<FileSystemInfoBase>>();
                var isoFilesByFolder = new Dictionary<string, List<FileSystemInfoBase>>();

                // Single NAS traversal: collect all files and group by folder
                foreach (var file in fileEnumerator)
                {
                    if (args.CancelToken.IsCancellationRequested)
                    {
                        _emuLibrary.Logger.Info("Scanning cancelled during file enumeration");
                        yield break;
                    }

                    // Lazy check: only inspect folder contents when we encounter a matching file
                    var parentFolderPath = Path.GetDirectoryName(file.FullName);
                    if (parentFolderPath != null && IsExtractedContentFolder(parentFolderPath, extractedContentCache))
                    {
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
                        var patterns = _emuLibrary.Settings.EnableGameNameNormalization
                            ? _emuLibrary.Settings.GameNameNormalizationPatterns?.ToArray()
                            : null;
                        gameName = StringExtensions.NormalizeGameName(folderName, patterns);
                        normalizedNameCache[folderName] = gameName;
                    }

                    if (string.IsNullOrEmpty(gameName))
                    {
                        gameName = Path.GetFileNameWithoutExtension(folderIsoFiles[0].Name);
                        var patterns = _emuLibrary.Settings.EnableGameNameNormalization
                            ? _emuLibrary.Settings.GameNameNormalizationPatterns?.ToArray()
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
                        _emuLibrary.Logger.Warn($"Installer path '{primaryIsoFile.FullName}' doesn't start with expected source path '{srcPath}'. Skipping installer.");
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
                                var patterns = _emuLibrary.Settings.EnableGameNameNormalization
                                    ? _emuLibrary.Settings.GameNameNormalizationPatterns?.ToArray()
                                    : null;
                                gameName = StringExtensions.NormalizeGameName(folderName, patterns);
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
        }

        public override bool TryGetGameInfoBaseFromLegacyGameId(Game game, EmulatorMapping mapping, out ELGameInfo gameInfo)
        {
            // PC installers are a new type, so there are no legacy game IDs to convert
            gameInfo = null;
            return false;
        }

        public override IEnumerable<Game> GetUninstalledGamesMissingSourceFiles(CancellationToken ct)
        {
            _emuLibrary.Logger.Info("Checking for PC installer games with missing source files");

            try
            {
                return _playniteAPI.Database.Games
                    .Where(g => g.PluginId == EmuLibrary.PluginId && !g.IsInstalled)
                    .Where(g =>
                    {
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
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _emuLibrary.Logger.Error($"Error in GetUninstalledGamesMissingSourceFiles: {ex.Message}");
                return Enumerable.Empty<Game>();
            }
        }
    }
}