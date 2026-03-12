using System;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using ProtoBuf;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EmuLibrary.RomTypes.PCInstaller
{
    [ProtoContract]
    internal sealed class PCInstallerGameInfo : ELGameInfo
    {
        public override RomType RomType => RomType.PCInstaller;

        // Relative to Mapping's SourcePath
        [ProtoMember(1)]
        public string SourcePath { get; set; }
        
        // Original installer path for reference
        [ProtoMember(2)]
        public string InstallerFullPath { get; set; }
        
        // Installation directory if installed
        // NOTE: Excluded from GameId to ensure stable IDs - installation state shouldn't change GameId
        [ProtoMember(3)]
        public string InstallDirectory { get; set; }
        
        // Path to the primary executable after installation
        // NOTE: Excluded from GameId to ensure stable IDs - installation state shouldn't change GameId
        [ProtoMember(4)]
        public string PrimaryExecutable { get; set; }
        
        // Store GOG/Steam/etc. specific game identifier
        [ProtoMember(5)]
        public string StoreGameId { get; set; }
        
        // Store installer type (GOG, Steam, Epic, etc.)
        [ProtoMember(6)]
        public string InstallerType { get; set; }
        
        /// <summary>
        /// Generates a stable GameId that excludes installation-state fields.
        /// This ensures the same game always gets the same ID regardless of installation status.
        /// </summary>
        public override string AsGameId()
        {
            // Create a copy with only identifying fields (exclude installation state)
            var stableInfo = new PCInstallerGameInfo
            {
                MappingId = MappingId,
                SourcePath = SourcePath,
                // InstallerFullPath excluded - absolute NAS path that changes with remapping
                StoreGameId = StoreGameId,
                InstallerType = InstallerType,
                InstallDirectory = null,
                PrimaryExecutable = null
            };
            
            using (var ms = new MemoryStream())
            {
                Serializer.Serialize(ms, stableInfo);
                return string.Format("!0{0}", Convert.ToBase64String(ms.ToArray()));
            }
        }

        public string SourceFullPath
        {
            get
            {
                try
                {
                    // Primary path: use SourcePath if available
                    if (!string.IsNullOrEmpty(SourcePath))
                    {
                        var mapping = Mapping;
                        if (mapping != null && !string.IsNullOrEmpty(mapping.SourcePath))
                        {
                            var fullPath = Path.Combine(mapping.SourcePath, SourcePath);
                            if (File.Exists(fullPath))
                                return fullPath;
                        }
                    }
                    
                    // Fallback 1: Use InstallerFullPath if it exists
                    if (!string.IsNullOrEmpty(InstallerFullPath) && File.Exists(InstallerFullPath))
                        return InstallerFullPath;
                    
                    // Fallback 2: If primary path doesn't exist, try to find file in source directory
                    var mapping2 = Mapping;
                    if (mapping2 != null && !string.IsNullOrEmpty(mapping2.SourcePath))
                    {
                        var sourceDir = mapping2.SourcePath;
                        if (Directory.Exists(sourceDir))
                        {
                            // Get the directory where the file should be
                            string searchDir = sourceDir;
                            if (!string.IsNullOrEmpty(SourcePath))
                            {
                                var expectedDir = Path.GetDirectoryName(Path.Combine(sourceDir, SourcePath));
                                if (Directory.Exists(expectedDir))
                                    searchDir = expectedDir;
                            }
                            
                            // Search for installer files (EXE, ISO, archives)
                            var extensions = new[] { ".exe", ".iso", ".zip", ".rar", ".7z" };
                            var files = Directory.GetFiles(searchDir, "*.*", SearchOption.TopDirectoryOnly)
                                .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                                .ToList();
                            
                            if (files.Count > 0 && !string.IsNullOrEmpty(SourcePath))
                            {
                                // Only return a file if it matches the expected name to avoid
                                // returning a completely wrong game's installer
                                var expectedName = Path.GetFileName(SourcePath);
                                var matchingFile = files.FirstOrDefault(f =>
                                    Path.GetFileName(f).Equals(expectedName, StringComparison.OrdinalIgnoreCase));
                                if (matchingFile != null)
                                    return matchingFile;
                            }
                        }
                    }
                    
                    // Final fallback: return InstallerFullPath even if it doesn't exist
                    return InstallerFullPath;
                }
                catch (System.Exception ex)
                {
                    LogManager.GetLogger().Error(ex, "Error getting source full path");
                    return InstallerFullPath;
                }
            }
        }

        public override InstallController GetInstallController(Game game, IEmuLibrary emuLibrary) =>
            new PCInstallerInstallController(game, emuLibrary);

        public override UninstallController GetUninstallController(Game game, IEmuLibrary emuLibrary) =>
            new PCInstallerUninstallController(game, emuLibrary);

        protected override IEnumerable<string> GetDescriptionLines()
        {
            yield return $"{nameof(SourcePath)} : {SourcePath}";
            yield return $"{nameof(SourceFullPath)}* : {SourceFullPath}";
            yield return $"{nameof(InstallerFullPath)} : {InstallerFullPath}";
            yield return $"{nameof(InstallDirectory)} : {InstallDirectory}";
            yield return $"{nameof(PrimaryExecutable)} : {PrimaryExecutable}";
            yield return $"{nameof(StoreGameId)} : {StoreGameId}";
            yield return $"{nameof(InstallerType)} : {InstallerType}";
        }

        public override void BrowseToSource()
        {
            try
            {
                var sourcePath = SourceFullPath;
                
                if (File.Exists(sourcePath))
                {
                    if (System.Environment.OSVersion.Platform == System.PlatformID.Win32NT)
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo()
                        {
                            FileName = "explorer.exe",
                            Arguments = $"/select,\"{Path.GetFullPath(sourcePath)}\""
                        };
                        System.Diagnostics.Process.Start(psi);
                    }
                    else
                    {
                        // For non-Windows platforms, open the directory
                        var directory = Path.GetDirectoryName(sourcePath);
                        System.Diagnostics.Process.Start(directory);
                    }
                }
                else
                {
                    // File doesn't exist, show directory
                    var directory = Path.GetDirectoryName(sourcePath);
                    if (Directory.Exists(directory))
                    {
                        if (System.Environment.OSVersion.Platform == System.PlatformID.Win32NT)
                        {
                            System.Diagnostics.Process.Start("explorer.exe", directory);
                        }
                        else
                        {
                            System.Diagnostics.Process.Start(directory);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                LogManager.GetLogger().Error(ex, "Error browsing to source");
            }
        }
    }
}