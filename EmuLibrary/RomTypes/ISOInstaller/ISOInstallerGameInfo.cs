using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using EmuLibrary.RomTypes.PCInstaller;

namespace EmuLibrary.RomTypes.ISOInstaller
{
    internal class ISOInstallerGameInfo : ELGameInfo
    {
        // Use shared content type from PCInstallerGameInfo instead of duplicating
        
        public ISOInstallerGameInfo()
        {
            // Provide default placeholder values when creating new instances
            SourcePath = "";
            InstallerFullPath = "";
        }
        
        [SerializationPropertyName("MappingId")]
        public new Guid MappingId { get; set; }

        [SerializationPropertyName("SourcePath")]
        public string SourcePath { get; set; }

        [SerializationPropertyName("InstallerFullPath")]
        public string InstallerFullPath { get; set; }

        [SerializationPropertyName("MountedISOPath")]
        public string MountedISOPath { get; set; }

        [SerializationPropertyName("SelectedInstaller")]
        public string SelectedInstaller { get; set; }

        [SerializationPropertyName("InstallDirectory")]
        public string InstallDirectory { get; set; }
        
        /// <summary>
        /// Checks if this game is properly installed and has all necessary paths
        /// </summary>
        /// <returns>True if the game is installed, false otherwise</returns>
        public bool IsProperlyInstalled()
        {
            // Check if game has an installation directory and it exists
            if (string.IsNullOrEmpty(InstallDirectory) || !Directory.Exists(InstallDirectory))
                return false;
                
            // Check if we have a primary executable and it exists
            if (!string.IsNullOrEmpty(PrimaryExecutable) && !File.Exists(PrimaryExecutable))
                return false;
                
            // All checks passed, game is properly installed
            return true;
        }

        [SerializationPropertyName("PrimaryExecutable")]
        public string PrimaryExecutable { get; set; }

        [SerializationPropertyName("StoreGameId")]
        public string StoreGameId { get; set; }

        [SerializationPropertyName("InstallerType")]
        public string InstallerType { get; set; }

        [SerializationPropertyName("MountPoint")]
        public string MountPoint { get; set; }
        
        // Content type identification fields
        [SerializationPropertyName("ContentType")]
        public PCInstaller.ContentType ContentType { get; set; } = PCInstaller.ContentType.BaseGame;
        
        [SerializationPropertyName("ParentGameId")]
        public string ParentGameId { get; set; }
        
        [SerializationPropertyName("Version")]
        public string Version { get; set; }
        
        [SerializationPropertyName("InstalledAddons")]
        public List<string> InstalledAddons { get; set; } = new List<string>();
        
        [SerializationPropertyName("ContentDescription")]
        public string ContentDescription { get; set; }

        public override RomType RomType => RomType.ISOInstaller;
        
        public string SourceFullPath
        {
            get
            {
                try 
                {
                    var logger = Playnite.SDK.LogManager.GetLogger();
                    var settings = Settings.Settings.Instance;
                    
                    // FIX: Add extra debugging to see what's causing the empty path issue
                    logger.Info($"Getting SourceFullPath - Current values: SourcePath={SourcePath}, InstallerFullPath={InstallerFullPath}, MappingId={MappingId}");
                    
                    // CRITICAL: InstallerFullPath should always have priority since it's explicitly set 
                    // during scanning with the full path to the ISO file
                    if (!string.IsNullOrEmpty(InstallerFullPath))
                    {
                        if (File.Exists(InstallerFullPath))
                        {
                            logger.Info($"Using InstallerFullPath directly (exists): {InstallerFullPath}");
                            return InstallerFullPath;
                        }
                        else
                        {
                            logger.Warn($"InstallerFullPath doesn't exist: {InstallerFullPath}, will try alternatives");
                        }
                    }
                    
                    // If we have a source path and mapping, try to combine them
                    if (!string.IsNullOrEmpty(SourcePath) && MappingId != Guid.Empty)
                    {
                        var mapping = settings.GetMapping(MappingId);
                        
                        if (mapping != null && !string.IsNullOrEmpty(mapping.SourcePath))
                        {
                            // Combine paths and verify the file exists
                            string combinedPath = Path.Combine(mapping.SourcePath, SourcePath);
                            logger.Info($"Trying combined path from mapping: {combinedPath}");
                            
                            if (File.Exists(combinedPath))
                            {
                                logger.Info($"Combined path exists: {combinedPath}");
                                return combinedPath;
                            }
                        }
                    }
                    
                    // At this point, we couldn't find the ISO file using the stored paths
                    // Try all available mappings with our source path
                    if (!string.IsNullOrEmpty(SourcePath))
                    {
                        logger.Info($"Trying all mappings with SourcePath: {SourcePath}");
                        foreach (var mapping in settings.Mappings.Where(m => m.RomType == RomType.ISOInstaller))
                        {
                            if (string.IsNullOrEmpty(mapping.SourcePath))
                                continue;
                                
                            string combinedPath = Path.Combine(mapping.SourcePath, SourcePath);
                            logger.Info($"Checking in mapping {mapping.MappingId}: {combinedPath}");
                            
                            if (File.Exists(combinedPath))
                            {
                                // Update our mapping ID for future use
                                this.MappingId = mapping.MappingId;
                                // Also update InstallerFullPath for consistency
                                this.InstallerFullPath = combinedPath;
                                logger.Info($"Found in mapping {mapping.MappingId}: {combinedPath}");
                                return combinedPath;
                            }
                        }
                    }

                    // Try to extract game name from various sources
                    // This is used for searching for ISO files by name
                    string gameName = null;
                    List<string> gameNameVariations = new List<string>();
                    
                    // Try to get game name from metadata first - this is the most reliable source
                    try
                    {
                        // Try to check if we're in a GameInfo that's part of a Game object
                        var game = settings.EmuLibrary?.Playnite?.Database?.Games?
                            .FirstOrDefault(g => g.GameId == this.AsGameId());
                            
                        if (game != null && !string.IsNullOrEmpty(game.Name))
                        {
                            gameName = game.Name;
                            logger.Info($"Using game name from database: {gameName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Error getting game name from database: {ex.Message}");
                    }
                    
                    // If we couldn't get the name from the database, try other sources
                    if (string.IsNullOrEmpty(gameName))
                    {
                        // Try to get game name from SourcePath first
                        if (!string.IsNullOrEmpty(SourcePath))
                        {
                            gameName = Path.GetFileNameWithoutExtension(SourcePath);
                            logger.Info($"Using game name from SourcePath: {gameName}");
                        }
                        // Then try InstallerFullPath
                        else if (!string.IsNullOrEmpty(InstallerFullPath))
                        {
                            gameName = Path.GetFileNameWithoutExtension(InstallerFullPath);
                            logger.Info($"Using game name from InstallerFullPath: {gameName}");
                        }
                        // Then try StoreGameId
                        else if (!string.IsNullOrEmpty(StoreGameId))
                        {
                            // If StoreGameId has a format like "GameName_file.iso", extract the game name
                            var parts = StoreGameId.Split(new[] { '_' }, 2);
                            if (parts.Length > 0)
                            {
                                gameName = parts[0];
                                logger.Info($"Using game name from StoreGameId: {gameName}");
                            }
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(gameName))
                    {
                        logger.Info($"Looking for ISOs with name similar to: {gameName}");
                        
                        // Normalize the game name and create variations for better matching
                        string normalizedName = gameName.Replace(":", "_").Replace("\\", "_").Replace("/", "_").Trim();
                        
                        // Create different name variations to increase chance of matching
                        gameNameVariations = new List<string> {
                            normalizedName,
                            normalizedName.Replace(" ", ""),           // NoSpaces
                            normalizedName.Replace(" ", "_"),          // Snake_Case
                            normalizedName.Replace(" ", "-"),          // Kebab-Case
                            normalizedName.Replace(" ", "."),          // Dot.Notation
                            System.Text.RegularExpressions.Regex.Replace(normalizedName, @"[^a-zA-Z0-9]", "") // AlphanumericOnly
                        };
                        
                        logger.Info($"Generated name variations: {string.Join(", ", gameNameVariations)}");
                        
                        foreach (var mapping in settings.Mappings.Where(m => m.RomType == RomType.ISOInstaller))
                        {
                            if (string.IsNullOrEmpty(mapping.SourcePath) || !Directory.Exists(mapping.SourcePath))
                                continue;
                                
                            logger.Info($"Searching in mapping {mapping.MappingId}: {mapping.SourcePath}");
                            
                            // Score-based matching for all ISO files in the mapping directory
                            try
                            {
                                // Scan for all ISO files in this mapping
                                var discFiles = new List<string>();
                                
                                // Search for common ISO formats - use both lowercase and uppercase extensions
                                foreach (var ext in new[] { "iso", "bin", "img", "cue", "nrg", "mds", "mdf" })
                                {
                                    try
                                    {
                                        discFiles.AddRange(Directory.GetFiles(mapping.SourcePath, $"*.{ext}", SearchOption.AllDirectories));
                                        discFiles.AddRange(Directory.GetFiles(mapping.SourcePath, $"*.{ext.ToUpper()}", SearchOption.AllDirectories));
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.Error($"Error searching for {ext} files: {ex.Message}");
                                    }
                                }
                                
                                logger.Info($"Found {discFiles.Count} total ISO files to check");
                                
                                if (discFiles.Count > 0)
                                {
                                    // Sort by filename length (shorter filenames first) to prefer simpler names
                                    discFiles = discFiles
                                        .OrderBy(f => Path.GetFileName(f).Length)
                                        .ThenBy(f => Path.GetFileName(f))
                                        .ToList();
                                    
                                    // Score-based best match selection
                                    string bestMatch = null;
                                    int bestScore = 0;
                                    
                                    foreach (var file in discFiles)
                                    {
                                        // Skip invalid files
                                        if (string.IsNullOrEmpty(file) || !File.Exists(file))
                                            continue;
                                            
                                        string fileName = Path.GetFileNameWithoutExtension(file);
                                        
                                        // Calculate match score
                                        int score = 0;
                                        
                                        // Exact match is best
                                        if (fileName.Equals(gameName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            score = 1000;
                                        }
                                        // Try all name variations
                                        else
                                        {
                                            foreach (var variant in gameNameVariations)
                                            {
                                                // Perfect match with a variant
                                                if (fileName.Equals(variant, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    score = 900;
                                                    break;
                                                }
                                                
                                                // File contains full game name
                                                if (fileName.IndexOf(variant, StringComparison.OrdinalIgnoreCase) >= 0)
                                                {
                                                    score = Math.Max(score, 700);
                                                }
                                                
                                                // Game name contains file name
                                                if (variant.IndexOf(fileName, StringComparison.OrdinalIgnoreCase) >= 0)
                                                {
                                                    score = Math.Max(score, 600);
                                                }
                                                
                                                // Check for word-by-word match (partial match)
                                                var fileWords = fileName.Split(new[] { ' ', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
                                                var gameWords = variant.Split(new[] { ' ', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
                                                
                                                int matchingWords = fileWords.Count(fw => 
                                                    gameWords.Any(gw => fw.Equals(gw, StringComparison.OrdinalIgnoreCase)));
                                                    
                                                if (matchingWords > 0)
                                                {
                                                    // Score based on percentage of matching words
                                                    double matchPercent = (double)matchingWords / Math.Max(fileWords.Length, gameWords.Length);
                                                    int partialScore = (int)(500 * matchPercent);
                                                    score = Math.Max(score, partialScore);
                                                }
                                            }
                                        }
                                        
                                        // Log high-scoring matches
                                        if (score > 400)
                                        {
                                            logger.Info($"Potential match: {fileName} (Score: {score})");
                                        }
                                        
                                        // Save best match so far
                                        if (score > bestScore)
                                        {
                                            bestScore = score;
                                            bestMatch = file;
                                            logger.Info($"New best match: {bestMatch} (Score: {bestScore})");
                                        }
                                    }
                                    
                                    // If we have a good match, use it
                                    if (bestScore > 400 && !string.IsNullOrEmpty(bestMatch))
                                    {
                                        // Update all our path information
                                        this.MappingId = mapping.MappingId;
                                        this.SourcePath = Path.GetFileName(bestMatch);
                                        this.InstallerFullPath = bestMatch;
                                        
                                        logger.Info($"Found best matching ISO with score {bestScore}: {bestMatch}");
                                        return bestMatch;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.Error($"Error during advanced ISO search: {ex.Message}");
                            }
                            
                            // Try simple extension-based matching as a fallback
                            // Search for common ISO formats
                            string[] extensions = new[] { ".iso", ".bin", ".img", ".cue", ".nrg", ".mds", ".mdf" };
                            
                            foreach (var ext in extensions)
                            {
                                // Try all name variations with each extension
                                foreach (var variant in gameNameVariations)
                                {
                                    try
                                    {
                                        // Try exact match with each extension
                                        var exactPath = Path.Combine(mapping.SourcePath, variant + ext);
                                        logger.Info($"Checking specific path: {exactPath}");
                                        
                                        if (File.Exists(exactPath))
                                        {
                                            this.MappingId = mapping.MappingId;
                                            this.SourcePath = variant + ext;
                                            this.InstallerFullPath = exactPath;
                                            logger.Info($"Found exact ISO match: {exactPath}");
                                            return exactPath;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.Error($"Error checking exact path for {variant}{ext}: {ex.Message}");
                                    }
                                }
                                
                                try
                                {
                                    // Try fuzzy match with each extension (case insensitive, partial name)
                                    var pattern = $"*{ext}";
                                    logger.Info($"Searching with pattern: {pattern}");
                                    
                                    var files = Directory.GetFiles(mapping.SourcePath, pattern, SearchOption.AllDirectories);
                                    logger.Info($"Found {files.Length} files with pattern {pattern}");
                                    
                                    foreach (var file in files)
                                    {
                                        try
                                        {
                                            var fileName = Path.GetFileNameWithoutExtension(file);
                                            
                                            // Try all name variations for fuzzy matching
                                            foreach (var variant in gameNameVariations)
                                            {
                                                if (fileName.IndexOf(variant, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                    variant.IndexOf(fileName, StringComparison.OrdinalIgnoreCase) >= 0)
                                                {
                                                    // Update our path information
                                                    this.MappingId = mapping.MappingId;
                                                    this.SourcePath = Path.GetFileName(file);
                                                    this.InstallerFullPath = file;
                                                    logger.Info($"Found fuzzy match: {file}");
                                                    return file;
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.Error($"Error processing file {file}: {ex.Message}");
                                        }
                                    }
                                }
                                catch (Exception searchEx)
                                {
                                    logger.Error($"Error searching for pattern in {mapping.SourcePath}: {searchEx.Message}");
                                }
                            }
                        }
                    }
                    
                    // Last resort: Check for ISO files with the exact name in all mappings
                    if (!string.IsNullOrEmpty(gameName))
                    {
                        foreach (var mapping in settings.Mappings.Where(m => m.RomType == RomType.ISOInstaller))
                        {
                            if (string.IsNullOrEmpty(mapping.SourcePath) || !Directory.Exists(mapping.SourcePath))
                                continue;
                            
                            // Log that we're checking this mapping as a last resort
                            logger.Info($"Last resort check in mapping {mapping.MappingId}: {mapping.SourcePath}");
                            
                            // Check for common disc image formats directly
                            foreach (var ext in new[] { ".iso", ".bin", ".img", ".cue", ".nrg", ".mds", ".mdf" })
                            {
                                string possiblePath = Path.Combine(mapping.SourcePath, gameName + ext);
                                logger.Info($"Checking {possiblePath}");
                                
                                if (File.Exists(possiblePath))
                                {
                                    // Update our path information
                                    this.MappingId = mapping.MappingId;
                                    this.SourcePath = gameName + ext;
                                    this.InstallerFullPath = possiblePath;
                                    logger.Info($"Found last resort match: {possiblePath}");
                                    return possiblePath;
                                }
                            }
                        }
                    }
                    
                    // We failed to find the ISO file, return an empty string
                    logger.Error($"Failed to resolve ISO path. SourcePath={SourcePath}, InstallerFullPath={InstallerFullPath}");
                    return "";
                }
                catch (System.Exception ex)
                {
                    Playnite.SDK.LogManager.GetLogger().Error(ex, "Error getting ISO source full path");
                    return InstallerFullPath;
                }
            }
            set
            {
                // Add a setter to allow fixing the path when needed
                InstallerFullPath = value;
            }
        }
        
        /// <summary>
        /// Checks if the provided path is likely to be an update or DLC for this game
        /// </summary>
        /// <param name="folderPath">Path to check</param>
        /// <param name="gameName">Original game name</param>
        /// <returns>True if the folder appears to be an update or DLC</returns>
        public static bool IsLikelyUpdateOrDLC(string folderPath, string gameName)
        {
            if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(gameName))
                return false;
                
            var folderName = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(folderName))
                return false;
                
            // If folder contains update-related keywords
            var updateKeywords = new[] { 
                "update", "patch", "dlc", "expansion", "addon", "add-on", "content", 
                "hotfix", "fix", "crack", "upgrade", "service pack", "sp", "v.", "ver"
            };
            
            // Do a case-insensitive check for update keywords
            string folderLower = folderName.ToLowerInvariant();
            foreach (var keyword in updateKeywords)
            {
                if (folderLower.Contains(keyword.ToLowerInvariant()))
                    return true;
            }
            
            // Check if folder name contains the game name plus additional text
            if (folderLower.Contains(gameName.ToLowerInvariant()) && 
                folderName.Length > gameName.Length)
                return true;
                
            // Check for version number patterns (v1.0, 1.0.2, etc)
            if (System.Text.RegularExpressions.Regex.IsMatch(folderName, @"v\d+(\.\d+)*") || 
                System.Text.RegularExpressions.Regex.IsMatch(folderName, @"\d+\.\d+(\.\d+)*"))
                return true;
                
            // Check for date-based patterns often used in patches (YYYY-MM-DD)
            if (System.Text.RegularExpressions.Regex.IsMatch(folderName, @"\d{4}[-_\.]\d{2}[-_\.]\d{2}"))
                return true;
                
            // Check for file content - if folder contains exe files with update-related names
            try 
            {
                var exeFiles = Directory.GetFiles(folderPath, "*.exe", SearchOption.TopDirectoryOnly);
                foreach (var exeFile in exeFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(exeFile).ToLowerInvariant();
                    foreach (var keyword in updateKeywords)
                    {
                        if (fileName.Contains(keyword.ToLowerInvariant()))
                            return true;
                    }
                    
                    // Also check for specific update installer patterns
                    if (fileName.Contains("setup") || fileName.Contains("install") || 
                        fileName.StartsWith("patch") || fileName.EndsWith("patch") ||
                        fileName.Contains("update"))
                        return true;
                }
            }
            catch
            {
                // Ignore file access errors
            }
                
            return false;
        }
        
        internal override InstallController GetInstallController(Game game, IEmuLibrary emuLibrary)
        {
            return new ISOInstallerInstallController(game, emuLibrary);
        }

        internal override ELUninstallController GetUninstallController(Game game, IEmuLibrary emuLibrary)
        {
            return new ISOInstallerUninstallController(game, emuLibrary);
        }

        protected override IEnumerable<string> GetDescriptionLines()
        {
            yield return $"{nameof(SourcePath)}: {SourcePath}";
            yield return $"{nameof(InstallDirectory)}: {InstallDirectory}";
            
            if (!string.IsNullOrEmpty(SelectedInstaller))
                yield return $"{nameof(SelectedInstaller)}: {SelectedInstaller}";
                
            if (!string.IsNullOrEmpty(PrimaryExecutable))
                yield return $"{nameof(PrimaryExecutable)}: {PrimaryExecutable}";
                
            if (!string.IsNullOrEmpty(InstallerType))
                yield return $"{nameof(InstallerType)}: {InstallerType}";
                
            if (!string.IsNullOrEmpty(StoreGameId))
                yield return $"{nameof(StoreGameId)}: {StoreGameId}";
                
            // Content type information
            if (ContentType != ContentType.BaseGame)
                yield return $"{nameof(ContentType)}: {ContentType}";
                
            if (!string.IsNullOrEmpty(Version))
                yield return $"{nameof(Version)}: {Version}";
                
            if (!string.IsNullOrEmpty(ContentDescription))
                yield return $"{nameof(ContentDescription)}: {ContentDescription}";
                
            if (!string.IsNullOrEmpty(ParentGameId))
                yield return $"{nameof(ParentGameId)}: {ParentGameId}";
                
            if (InstalledAddons != null && InstalledAddons.Count > 0)
                yield return $"{nameof(InstalledAddons)}: {string.Join(", ", InstalledAddons)}";
        }

        public override void BrowseToSource()
        {
            if (string.IsNullOrEmpty(SourcePath))
            {
                return;
            }

            try
            {
                var fullPath = SourceFullPath;
                var parentDir = Path.GetDirectoryName(fullPath);
                if (Directory.Exists(parentDir))
                {
                    Process.Start(parentDir);
                }
            }
            catch (Exception ex)
            {
                Settings.Settings.Instance.EmuLibrary.Logger.Error($"Failed to browse to source: {ex.Message}");
            }
        }
    }
}