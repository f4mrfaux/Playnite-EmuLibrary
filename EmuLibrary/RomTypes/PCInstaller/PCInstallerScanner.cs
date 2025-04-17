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

namespace EmuLibrary.RomTypes.PCInstaller
{
    internal class PCInstallerScanner : RomTypeScanner
    {
        private readonly IPlayniteAPI _playniteAPI;
        private readonly IEmuLibrary _emuLibrary;
        private const int BATCH_SIZE = 100; // Process games in batches for better performance

        public override RomType RomType => RomType.PCInstaller;
        public override Guid LegacyPluginId => EmuLibrary.PluginId;

        public PCInstallerScanner(IEmuLibrary emuLibrary) : base(emuLibrary)
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

            // For PC installer games, emulator profile is optional since they're native executables
            // No validation needed for emulator profile as these games run natively

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

            // Only support EXE files for PC game installers
            var installerExtensions = new List<string> { "exe" };
            
            SafeFileEnumerator fileEnumerator;
            var gameMetadataBatch = new List<GameMetadata>();

            #region Import discovered installers
            var sourcedGames = new List<GameMetadata>();
            try
            {
                fileEnumerator = new SafeFileEnumerator(srcPath, "*.*", SearchOption.AllDirectories);
                _emuLibrary.Logger.Info($"Scanning for PC installers in {srcPath}");

                // Create a dictionary to cache normalized folder names for performance
                var normalizedNameCache = new Dictionary<string, string>();

                foreach (var file in fileEnumerator)
                {
                    if (args.CancelToken.IsCancellationRequested)
                    {
                        _emuLibrary.Logger.Info("Scanning cancelled during file enumeration");
                        yield break;
                    }

                    foreach (var extension in installerExtensions)
                    {
                        if (args.CancelToken.IsCancellationRequested)
                            yield break;

                        if (HasMatchingExtension(file, extension))
                        {
                            try
                            {
                                // For PC installers, we use the parent folder name as the game name
                                var parentFolderPath = Path.GetDirectoryName(file.FullName);
                                if (parentFolderPath == null)
                                {
                                    _emuLibrary.Logger.Warn($"Could not get parent directory for {file.FullName}, skipping");
                                    continue;
                                }

                                var parentFolder = Path.GetFileName(parentFolderPath);
                                
                                // Use cached normalized name if available
                                string gameName;
                                if (!normalizedNameCache.TryGetValue(parentFolder, out gameName))
                                {
                                    gameName = StringExtensions.NormalizeGameName(parentFolder);
                                    normalizedNameCache[parentFolder] = gameName;
                                }
                                
                                if (string.IsNullOrEmpty(gameName))
                                {
                                    _emuLibrary.Logger.Warn($"Game name is empty for {file.FullName}, using file name instead");
                                    gameName = Path.GetFileNameWithoutExtension(file.Name);
                                }
                                
                                var relativePath = file.FullName.Substring(srcPath.Length).TrimStart(Path.DirectorySeparatorChar);
                                
                                // Try to detect GOG installer by checking filename
                                bool isGogInstaller = file.Name.Contains("gog") || 
                                                     file.Name.Contains("setup_") || 
                                                     file.FullName.Contains("GOG") ||
                                                     parentFolder.Contains("GOG");
                                
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
                                
                                // Add metadata for EmuLibrary tracking (do not change Source property)
                                if (!string.IsNullOrEmpty(info.StoreGameId) && !string.IsNullOrEmpty(info.InstallerType))
                                {
                                    // Add store-specific info to help with metadata matching later
                                    // Store the information in the game tags instead of Properties
                                    metadata.Tags = metadata.Tags ?? new HashSet<MetadataProperty>();
                                    metadata.Tags.Add(new MetadataNameProperty($"{info.InstallerType}:{info.StoreGameId}"));
                                    
                                    _emuLibrary.Logger.Debug($"Added store metadata for {gameName}: {info.InstallerType} ID {info.StoreGameId}");
                                }
                                
                                gameMetadataBatch.Add(metadata);

                                // Batch processing handled outside the try block
                            }
                            catch (Exception ex)
                            {
                                _emuLibrary.Logger.Error($"Error processing installer file {file.FullName}: {ex.Message}");
                            }
                        }
                    }
                }

                // Add remaining games to the collection
                if (gameMetadataBatch.Count > 0)
                {
                    _emuLibrary.Logger.Debug($"Adding final batch of {gameMetadataBatch.Count} PC installer games");
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
                _emuLibrary.Logger.Info("Updating installed PC installer games");
                
                // Use BufferedUpdate for better performance
                using (_playniteAPI.Database.BufferedUpdate())
                {
                    // More efficient filtering upfront to reduce iteration
                    var installedGames = _playniteAPI.Database.Games
                        .Where(g => g.PluginId == EmuLibrary.PluginId && g.IsInstalled)
                        .Select(g => {
                            try {
                                var info = g.GetELGameInfo();
                                if (info.RomType == RomType.PCInstaller) {
                                    return (g, info as PCInstallerGameInfo);
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
                                        Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(mapping.Platform.Name) },
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
            // PC installers are a new type, so there are no legacy game IDs to convert
            gameInfo = null;
            return false;
        }

        public override IEnumerable<Game> GetUninstalledGamesMissingSourceFiles(CancellationToken ct)
        {
            _emuLibrary.Logger.Info("Checking for PC installer games with missing source files");
            
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