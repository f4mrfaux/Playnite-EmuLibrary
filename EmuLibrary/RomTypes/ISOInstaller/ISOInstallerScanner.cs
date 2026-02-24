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

        public override RomType RomType => RomType.ISOInstaller;
        public override Guid LegacyPluginId => EmuLibrary.PluginId;

        public ISOInstallerScanner(IEmuLibrary emuLibrary) : base(emuLibrary)
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

            var discExtensions = new List<string> {
                "iso", "zip", "rar", "7z", "7zip"
            };
            
            SafeFileEnumerator fileEnumerator;
            var gameMetadataBatch = new List<GameMetadata>();

            #region Import discovered disc images
            var sourcedGames = new List<GameMetadata>();
            try
            {
                fileEnumerator = new SafeFileEnumerator(srcPath, "*.*", SearchOption.AllDirectories);
                _emuLibrary.Logger.Info($"Scanning for disc images in {srcPath}");

                var normalizedNameCache = new Dictionary<string, string>();
                var extractedContentCache = new Dictionary<string, bool>();

                // Collect all matching files in a single NAS traversal
                var isoFiles = new List<string>();

                foreach (var file in fileEnumerator)
                {
                    if (args.CancelToken.IsCancellationRequested)
                    {
                        _emuLibrary.Logger.Info("Scanning cancelled during file enumeration");
                        yield break;
                    }

                    string fileExtension = file.Extension?.TrimStart('.')?.ToLowerInvariant();

                    if (!string.IsNullOrEmpty(fileExtension) && discExtensions.Contains(fileExtension))
                    {
                        try
                        {
                            // Lazy check: only inspect folder contents when we encounter a matching file
                            var parentDir = Path.GetDirectoryName(file.FullName);
                            if (IsExtractedContentFolder(parentDir, extractedContentCache))
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
                            // First normalize the name to remove release groups, versions, etc.
                            var patterns = _emuLibrary.Settings.EnableGameNameNormalization
                                ? _emuLibrary.Settings.GameNameNormalizationPatterns?.ToArray()
                                : null;
                            gameName = StringExtensions.NormalizeGameName(folderName, patterns);
                            
                            // Optionally try to get a cleaner name from Playnite's metadata providers
                            if (!string.IsNullOrEmpty(gameName) && gameName.Length > 3)
                            {
                                try
                                {
                                    var metadataName = TryGetMetadataName(gameName, mapping.Platform);
                                    if (!string.IsNullOrEmpty(metadataName) && 
                                        !string.Equals(metadataName, gameName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        _emuLibrary.Logger.Debug($"Metadata provider suggested name '{metadataName}' for '{gameName}'");
                                        // Use metadata name if it's significantly different (likely cleaner)
                                        if (metadataName.Length <= gameName.Length * 1.2) // Don't use if metadata name is much longer
                                        {
                                            gameName = metadataName;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Don't fail on metadata lookup errors, just log and continue
                                    _emuLibrary.Logger.Debug($"Could not get metadata name for '{gameName}': {ex.Message}");
                                }
                            }
                            
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
                        if (!primaryIsoFile.StartsWith(srcPath, StringComparison.OrdinalIgnoreCase))
                        {
                            _emuLibrary.Logger.Warn($"ISO path '{primaryIsoFile}' doesn't start with expected source path '{srcPath}'. Skipping ISO.");
                            continue;
                        }
                        var relativePath = primaryIsoFile.Substring(srcPath.Length).TrimStart(Path.DirectorySeparatorChar);

                        // Validate all ISO files start with source path
                        var validIsoFiles = folderIsoFiles.Where(f => f.StartsWith(srcPath, StringComparison.OrdinalIgnoreCase)).ToList();
                        if (validIsoFiles.Count != folderIsoFiles.Count)
                        {
                            _emuLibrary.Logger.Warn($"Some ISO files in folder don't start with expected source path '{srcPath}'. Using only valid files.");
                        }

                        // Create game info
                        var info = new ISOInstallerGameInfo()
                        {
                            MappingId = mapping.MappingId,
                            SourcePath = relativePath,
                            SourceBasePath = srcPath,
                            InstallDirectory = null, // Will be set during installation
                            ISOFiles = validIsoFiles.Select(f => f.Substring(srcPath.Length).TrimStart(Path.DirectorySeparatorChar)).ToList()
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

        /// <summary>
        /// Attempts to get a cleaner game name from Playnite's metadata providers.
        /// This helps match release names to proper game titles.
        /// </summary>
        private string TryGetMetadataName(string normalizedName, EmulatedPlatform platform)
        {
            if (string.IsNullOrEmpty(normalizedName) || _playniteAPI == null)
            {
                return null;
            }

            try
            {
                // Create a temporary game object for metadata lookup
                var tempGame = new Game
                {
                    Name = normalizedName,
                    PlatformIds = new List<Guid>()
                };

                // Try to get metadata from available providers
                var metadataProviders = _playniteAPI.Addons.Plugins
                    .Where(p => p is Playnite.SDK.Plugins.MetadataPlugin)
                    .Cast<Playnite.SDK.Plugins.MetadataPlugin>()
                    .ToList();

                if (metadataProviders.Count == 0)
                {
                    return null;
                }

                // Try the first available metadata provider (usually IGDB)
                foreach (var provider in metadataProviders)
                {
                    try
                    {
                        var metadataProvider = provider.GetMetadataProvider(
                            new Playnite.SDK.Plugins.MetadataRequestOptions(tempGame, true));

                        if (metadataProvider != null)
                        {
                            // Try to get just the name field (lightweight operation)
                            var nameField = metadataProvider.GetName(
                                new Playnite.SDK.Plugins.GetMetadataFieldArgs());

                            if (!string.IsNullOrEmpty(nameField) && 
                                nameField.Length > 2 &&
                                !string.Equals(nameField, normalizedName, StringComparison.OrdinalIgnoreCase))
                            {
                                // Found a better name from metadata
                                return nameField;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Continue to next provider if this one fails
                        _emuLibrary.Logger.Debug($"Metadata provider {provider.Name} failed: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                // Don't throw, just return null if metadata lookup fails
                _emuLibrary.Logger.Debug($"Error in TryGetMetadataName: {ex.Message}");
            }

            return null;
        }

        public override IEnumerable<Game> GetUninstalledGamesMissingSourceFiles(CancellationToken ct)
        {
            _emuLibrary.Logger.Info("Checking for ISO installer games with missing source files");

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