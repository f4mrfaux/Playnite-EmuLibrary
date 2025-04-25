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
                                logger.Info($"Found in mapping {mapping.MappingId}: {combinedPath}");
                                return combinedPath;
                            }
                        }
                    }

                    // Last resort: If we have the game name, try to find an ISO file with that name in any mapping
                    string gameName = null;
                    
                    // Try to get game name from SourcePath first
                    if (!string.IsNullOrEmpty(SourcePath))
                    {
                        gameName = Path.GetFileNameWithoutExtension(SourcePath);
                    }
                    // Also check by filename of InstallerFullPath
                    else if (!string.IsNullOrEmpty(InstallerFullPath))
                    {
                        gameName = Path.GetFileNameWithoutExtension(InstallerFullPath);
                    }
                    
                    if (!string.IsNullOrEmpty(gameName))
                    {
                        logger.Info($"Looking for ISOs with name similar to: {gameName}");
                        
                        // Normalize the game name for better matching
                        gameName = gameName.Replace(":", "_").Replace("\\", "_").Replace("/", "_");
                        
                        foreach (var mapping in settings.Mappings.Where(m => m.RomType == RomType.ISOInstaller))
                        {
                            if (string.IsNullOrEmpty(mapping.SourcePath) || !Directory.Exists(mapping.SourcePath))
                                continue;
                                
                            logger.Info($"Searching in mapping {mapping.MappingId}: {mapping.SourcePath}");
                            
                            // Search for common ISO formats
                            string[] extensions = new[] { ".iso", ".bin", ".img", ".cue", ".nrg", ".mds", ".mdf" };
                            
                            foreach (var ext in extensions)
                            {
                                try
                                {
                                    // Try exact match with each extension
                                    var exactPath = Path.Combine(mapping.SourcePath, gameName + ext);
                                    if (File.Exists(exactPath))
                                    {
                                        this.MappingId = mapping.MappingId;
                                        this.SourcePath = gameName + ext;
                                        this.InstallerFullPath = exactPath;
                                        logger.Info($"Found exact ISO match: {exactPath}");
                                        return exactPath;
                                    }
                                    
                                    // Try fuzzy match (case insensitive, partial name)
                                    var files = Directory.GetFiles(mapping.SourcePath, "*" + ext, SearchOption.AllDirectories);
                                    
                                    foreach (var file in files)
                                    {
                                        var fileName = Path.GetFileNameWithoutExtension(file);
                                        
                                        if (fileName.IndexOf(gameName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            gameName.IndexOf(fileName, StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            // Update our path information
                                            this.MappingId = mapping.MappingId;
                                            this.SourcePath = Path.GetFileName(file);
                                            this.InstallerFullPath = file;
                                            logger.Info($"Found similar ISO match: {file}");
                                            return file;
                                        }
                                    }
                                }
                                catch (Exception searchEx)
                                {
                                    logger.Error($"Error searching for {gameName}{ext} in {mapping.SourcePath}: {searchEx.Message}");
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