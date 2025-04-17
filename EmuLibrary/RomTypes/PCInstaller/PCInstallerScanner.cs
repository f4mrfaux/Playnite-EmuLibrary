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
using System.Text.RegularExpressions;
using System.Threading;

namespace EmuLibrary.RomTypes.PCInstaller
{
    internal class PCInstallerScanner : RomTypeScanner
    {
        private readonly IPlayniteAPI _playniteAPI;
        private readonly IEmuLibrary _emuLibrary;
        private const int BATCH_SIZE = 100; // Process games in batches for better performance
        
        // Regex patterns for detecting content types
        private static readonly Regex _updatePattern = new Regex(@"(?i)(update|patch|hotfix|fix|v\d+(\.\d+)+)", RegexOptions.Compiled);
        private static readonly Regex _dlcPattern = new Regex(@"(?i)(dlc|addon|add-?on|content|season pass)", RegexOptions.Compiled);
        private static readonly Regex _expansionPattern = new Regex(@"(?i)(expansion|expans\.?|xpac|xpack|xp\d+)", RegexOptions.Compiled);
        private static readonly Regex _versionPattern = new Regex(@"(?i)(?:v|ver|version)\.?\s*(\d+(?:\.\d+){1,3})", RegexOptions.Compiled);
        
        // Common naming patterns for DLC and expansions
        private static readonly string[] _commonDlcPhrases = new[] { "dlc", "addon", "add-on", "content pack", "season pass" };
        private static readonly string[] _commonExpansionPhrases = new[] { "expansion", "expansion pack", "xpac", "xpack" };

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
                                                !Regex.IsMatch(storeGameId, @"^\d+$"))
                                            {
                                                storeGameId = null;
                                            }
                                        }
                                    }
                                }
                                
                                // Detect content type, version, and content description
                                var (contentType, version, contentDescription) = 
                                    DetectContentTypeAndVersion(
                                        file.FullName, 
                                        file.Name, 
                                        parentFolder
                                    );
                                
                                // For content other than base games, try to extract the base game name
                                string baseGameName = null;
                                string parentGameId = null;
                                
                                if (contentType != PCInstallerGameInfo.ContentType.BaseGame)
                                {
                                    // Try to extract base game name by removing content indicators
                                    baseGameName = ExtractBaseGameName(gameName);
                                    
                                    if (!string.IsNullOrEmpty(baseGameName))
                                    {
                                        _emuLibrary.Logger.Debug($"Extracted base game name '{baseGameName}' from '{gameName}'");
                                        
                                        // Try to find a matching base game in the database
                                        var potentialBaseGames = _playniteAPI.Database.Games
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
                                                
                                            // Make sure it's a PCInstaller game
                                            try
                                            {
                                                var baseGameInfo = baseGame.GetELGameInfo();
                                                if (baseGameInfo != null && baseGameInfo.RomType == RomType.PCInstaller)
                                                {
                                                    var pcBaseGameInfo = baseGameInfo as PCInstallerGameInfo;
                                                    if (pcBaseGameInfo != null && 
                                                        pcBaseGameInfo.ContentType == PCInstallerGameInfo.ContentType.BaseGame)
                                                    {
                                                        parentGameId = baseGame.GameId;
                                                        _emuLibrary.Logger.Info($"Found parent game '{baseGame.Name}' for '{gameName}'");
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                _emuLibrary.Logger.Error($"Error checking potential base game {baseGame.Name}: {ex.Message}");
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
                                    InstallerType = installerType,
                                    // Add content type information
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
                                    _emuLibrary.Logger.Info($"No platform set for PCInstaller, using default '{platformName}'");
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
                                
                                // Adjust game name for DLC, Updates, etc.
                                string displayName = gameName;
                                if (info.ContentType != PCInstallerGameInfo.ContentType.BaseGame && 
                                    !string.IsNullOrEmpty(info.ContentDescription))
                                {
                                    // For content that's not a base game, include content description in name
                                    // If we found a parent game, use its name as a prefix
                                    if (!string.IsNullOrEmpty(info.ParentGameId))
                                    {
                                        var parentGame = _playniteAPI.Database.Games.FirstOrDefault(g => g.GameId == info.ParentGameId);
                                        if (parentGame != null)
                                        {
                                            displayName = $"{parentGame.Name} - {info.ContentDescription}";
                                        }
                                        else
                                        {
                                            displayName = $"{gameName} - {info.ContentDescription}";
                                        }
                                    }
                                    else
                                    {
                                        displayName = $"{gameName} - {info.ContentDescription}";
                                    }
                                }
                                
                                // Add version if available
                                if (!string.IsNullOrEmpty(info.Version) && 
                                    !displayName.Contains(info.Version))
                                {
                                    displayName = $"{displayName} (v{info.Version})";
                                }
                                
                                var metadata = new GameMetadata()
                                {
                                    Source = EmuLibrary.SourceName,
                                    Name = displayName,
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
                                metadata.Tags = new HashSet<MetadataProperty>();
                                
                                if (!string.IsNullOrEmpty(info.InstallerType))
                                {
                                    metadata.Tags.Add(new MetadataNameProperty(info.InstallerType));
                                }
                                
                                // Add content type as tag
                                switch (info.ContentType)
                                {
                                    case PCInstallerGameInfo.ContentType.Update:
                                        metadata.Tags.Add(new MetadataNameProperty("Update"));
                                        break;
                                    case PCInstallerGameInfo.ContentType.DLC:
                                        metadata.Tags.Add(new MetadataNameProperty("DLC"));
                                        break;
                                    case PCInstallerGameInfo.ContentType.Expansion:
                                        metadata.Tags.Add(new MetadataNameProperty("Expansion"));
                                        break;
                                }
                                
                                // Add version info to description
                                if (!string.IsNullOrEmpty(info.Version))
                                {
                                    metadata.Description = $"Version: {info.Version}\n";
                                    if (!string.IsNullOrEmpty(info.ContentDescription))
                                    {
                                        metadata.Description += $"Content: {info.ContentDescription}";
                                    }
                                }
                                else if (!string.IsNullOrEmpty(info.ContentDescription))
                                {
                                    metadata.Description = $"Content: {info.ContentDescription}";
                                }
                                
                                // Add parent-child relationship info
                                if (!string.IsNullOrEmpty(info.ParentGameId))
                                {
                                    var parentGame = _playniteAPI.Database.Games.FirstOrDefault(g => g.GameId == info.ParentGameId);
                                    if (parentGame != null)
                                    {
                                        // Store the parent-child relationship in custom properties
                                        metadata.GameDependencies = new HashSet<Guid> { parentGame.Id };
                                        
                                        if (metadata.Description == null)
                                        {
                                            metadata.Description = $"Related to: {parentGame.Name}";
                                        }
                                        else
                                        {
                                            metadata.Description += $"\nRelated to: {parentGame.Name}";
                                        }
                                    }
                                }
                                
                                // Add metadata for EmuLibrary tracking (do not change Source property)
                                if (!string.IsNullOrEmpty(info.StoreGameId) && !string.IsNullOrEmpty(info.InstallerType))
                                {
                                    // Add store-specific info to help with metadata matching later
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
                                    
                                    // Handle content type-specific metadata
                                    if (gameInfo.ContentType != PCInstallerGameInfo.ContentType.BaseGame)
                                    {
                                        // Add content type as tag
                                        gameMetadata.Tags = new HashSet<MetadataProperty>();
                                        switch (gameInfo.ContentType)
                                        {
                                            case PCInstallerGameInfo.ContentType.Update:
                                                gameMetadata.Tags.Add(new MetadataNameProperty("Update"));
                                                break;
                                            case PCInstallerGameInfo.ContentType.DLC:
                                                gameMetadata.Tags.Add(new MetadataNameProperty("DLC"));
                                                break;
                                            case PCInstallerGameInfo.ContentType.Expansion:
                                                gameMetadata.Tags.Add(new MetadataNameProperty("Expansion"));
                                                break;
                                        }
                                        
                                        // Add version info to description
                                        if (!string.IsNullOrEmpty(gameInfo.Version))
                                        {
                                            gameMetadata.Description = $"Version: {gameInfo.Version}\n";
                                            if (!string.IsNullOrEmpty(gameInfo.ContentDescription))
                                            {
                                                gameMetadata.Description += $"Content: {gameInfo.ContentDescription}";
                                            }
                                        }
                                        else if (!string.IsNullOrEmpty(gameInfo.ContentDescription))
                                        {
                                            gameMetadata.Description = $"Content: {gameInfo.ContentDescription}";
                                        }
                                        
                                        // Add parent-child relationship info
                                        if (!string.IsNullOrEmpty(gameInfo.ParentGameId))
                                        {
                                            var parentGame = _playniteAPI.Database.Games.FirstOrDefault(
                                                g => g.GameId == gameInfo.ParentGameId);
                                                
                                            if (parentGame != null)
                                            {
                                                // Store the parent-child relationship
                                                gameMetadata.GameDependencies = new HashSet<Guid> { parentGame.Id };
                                                
                                                if (gameMetadata.Description == null)
                                                {
                                                    gameMetadata.Description = $"Related to: {parentGame.Name}";
                                                }
                                                else
                                                {
                                                    gameMetadata.Description += $"\nRelated to: {parentGame.Name}";
                                                }
                                                
                                                // Update parent game's installed addons list
                                                try
                                                {
                                                    var parentInfo = parentGame.GetELGameInfo() as PCInstallerGameInfo;
                                                    if (parentInfo != null)
                                                    {
                                                        // Ensure the list exists
                                                        parentInfo.InstalledAddons = parentInfo.InstalledAddons ?? new List<string>();
                                                        
                                                        // Add this addon if not already in the list
                                                        if (!parentInfo.InstalledAddons.Contains(game.GameId))
                                                        {
                                                            parentInfo.InstalledAddons.Add(game.GameId);
                                                            
                                                            // Update the parent game in the database
                                                            parentGame.SetELGameInfo(parentInfo);
                                                            _playniteAPI.Database.Games.Update(parentGame);
                                                            _emuLibrary.Logger.Info($"Updated parent game {parentGame.Name} with installed addon {game.Name}");
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    _emuLibrary.Logger.Error($"Error updating parent game {parentGame.Name} with addon: {ex.Message}");
                                                }
                                            }
                                        }
                                    }
                                    else if (gameInfo.InstalledAddons != null && gameInfo.InstalledAddons.Count > 0)
                                    {
                                        // For base games, include information about installed addons
                                        var installedAddonGames = _playniteAPI.Database.Games
                                            .Where(g => gameInfo.InstalledAddons.Contains(g.GameId))
                                            .ToList();
                                            
                                        if (installedAddonGames.Count > 0)
                                        {
                                            var addonNames = installedAddonGames.Select(g => g.Name).ToList();
                                            
                                            gameMetadata.Description = gameMetadata.Description ?? "";
                                            gameMetadata.Description += "\nInstalled add-ons:\n- " + 
                                                string.Join("\n- ", addonNames);
                                                
                                            // Set dependencies (this will show up in the UI)
                                            gameMetadata.GameDependencies = new HashSet<Guid>(
                                                installedAddonGames.Select(g => g.Id));
                                        }
                                    }
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
        
        /// <summary>
        /// Detects content type, version and other metadata from file and directory names
        /// </summary>
        /// <param name="filePath">Full file path to the installer</param>
        /// <param name="fileName">File name</param>
        /// <param name="dirName">Directory name</param>
        /// <returns>Tuple with ContentType, Version, and ContentDescription</returns>
        private (PCInstallerGameInfo.ContentType contentType, string version, string contentDescription) 
            DetectContentTypeAndVersion(string filePath, string fileName, string dirName)
        {
            // Default values
            var contentType = PCInstallerGameInfo.ContentType.BaseGame;
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
                contentType = PCInstallerGameInfo.ContentType.Update;
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
                contentType = PCInstallerGameInfo.ContentType.Expansion;
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
                contentType = PCInstallerGameInfo.ContentType.DLC;
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
        
        /// <summary>
        /// Detects content type, version and other metadata from file and directory names
        /// </summary>
        /// <param name="filePath">Full file path to the installer</param>
        /// <param name="fileName">File name</param>
        /// <param name="dirName">Directory name</param>
        /// <returns>Tuple with ContentType, Version, and ContentDescription</returns>
        private (PCInstallerGameInfo.ContentType contentType, string version, string contentDescription) 
            DetectContentTypeAndVersion(string filePath, string fileName, string dirName)
        {
            // Default values
            var contentType = PCInstallerGameInfo.ContentType.BaseGame;
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
                contentType = PCInstallerGameInfo.ContentType.Update;
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
                contentType = PCInstallerGameInfo.ContentType.Expansion;
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
                contentType = PCInstallerGameInfo.ContentType.DLC;
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