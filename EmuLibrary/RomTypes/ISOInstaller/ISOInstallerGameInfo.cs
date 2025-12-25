using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using ProtoBuf;

namespace EmuLibrary.RomTypes.ISOInstaller
{
    [ProtoContract]
    public sealed class ISOInstallerGameInfo : ELGameInfo
    {
        [ProtoIgnore]
        public override RomType RomType => RomType.ISOInstaller;

        /// <summary>
        /// The source directory path (relative to mapping's SourcePath)
        /// </summary>
        [ProtoMember(1)]
        public string SourcePath { get; set; }

        /// <summary>
        /// The source base path for ISO files
        /// </summary>
        [ProtoMember(2)]
        public string SourceBasePath { get; set; }

        /// <summary>
        /// The working directory for installed executables (usually the install directory)
        /// </summary>
        [ProtoMember(3)]
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// The installation directory path
        /// NOTE: Excluded from GameId to ensure stable IDs - installation state shouldn't change GameId
        /// </summary>
        [ProtoMember(4)]
        public string InstallDirectory { get; set; }

        /// <summary>
        /// The primary executable path (relative to install directory)
        /// NOTE: Excluded from GameId to ensure stable IDs - installation state shouldn't change GameId
        /// </summary>
        [ProtoMember(5)]
        public string PrimaryExecutable { get; set; }

        /// <summary>
        /// The full path to the ISO installer
        /// </summary>
        [DontSerialize]
        public string InstallerFullPath => Path.Combine(SourceBasePath, SourcePath);

        /// <summary>
        /// List of all ISO files for this game
        /// </summary>
        [ProtoMember(6)]
        public List<string> ISOFiles { get; set; } = new List<string>();
        
        /// <summary>
        /// Generates a stable GameId that excludes installation-state fields.
        /// This ensures the same game always gets the same ID regardless of installation status.
        /// </summary>
        public override string AsGameId()
        {
            // Create a copy with only identifying fields (exclude installation state)
            var stableInfo = new ISOInstallerGameInfo
            {
                MappingId = MappingId,
                SourcePath = SourcePath,
                SourceBasePath = SourceBasePath,
                ISOFiles = ISOFiles,
                // Explicitly exclude installation-state fields
                InstallDirectory = null,
                PrimaryExecutable = null,
                WorkingDirectory = null
            };
            
            using (var ms = new System.IO.MemoryStream())
            {
                ProtoBuf.Serializer.Serialize(ms, stableInfo);
                return string.Format("!0{0}", Convert.ToBase64String(ms.ToArray()));
            }
        }

        /// <summary>
        /// Gets the full path to the source ISO file
        /// </summary>
        [DontSerialize]
        public string SourceFullPath 
        { 
            get 
            {
                if (string.IsNullOrEmpty(SourcePath) || string.IsNullOrEmpty(SourceBasePath))
                    return null;
                    
                try
                {
                    // Primary source path
                    var fullPath = Path.Combine(SourceBasePath, SourcePath);
                    if (File.Exists(fullPath))
                        return fullPath;
                        
                    // If the primary source doesn't exist, check all ISO files
                    if (ISOFiles != null && ISOFiles.Count > 0)
                    {
                        foreach (var isoPath in ISOFiles)
                        {
                            var isoFullPath = Path.Combine(SourceBasePath, isoPath);
                            if (File.Exists(isoFullPath))
                                return isoFullPath;
                        }
                    }
                    
                    // If none of the explicit paths exist, try to find any ISO in the source directory
                    var directory = Path.GetDirectoryName(fullPath);
                    if (Directory.Exists(directory))
                    {
                        var extensions = new[] { ".iso", ".bin", ".img", ".cue", ".nrg", ".mds", ".mdf" };
                        var files = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                            .ToList();
                            
                        if (files.Count > 0)
                            return files.First();
                    }
                }
                catch (Exception ex)
                {
                    // Just return null on any error - typically means the path wasn't found
                    LogManager.GetLogger().Error(ex, "Error getting source full path");
                }
                
                return null;
            }
        }

        public override InstallController GetInstallController(Game game, IEmuLibrary emuLibrary) =>
            new ISOInstallerInstallController(game, emuLibrary);

        public override UninstallController GetUninstallController(Game game, IEmuLibrary emuLibrary) =>
            new ISOInstallerUninstallController(game, emuLibrary);

        protected override IEnumerable<string> GetDescriptionLines()
        {
            yield return $"{nameof(SourcePath)}: {SourcePath}";
            yield return $"{nameof(SourceBasePath)}: {SourceBasePath}";
            yield return $"{nameof(SourceFullPath)}*: {SourceFullPath}";
            yield return $"{nameof(InstallDirectory)}: {InstallDirectory}";
            yield return $"{nameof(PrimaryExecutable)}: {PrimaryExecutable}";
            yield return $"{nameof(WorkingDirectory)}: {WorkingDirectory}";
            yield return $"ISO Files Count: {(ISOFiles?.Count ?? 0)}";
        }

        public override void BrowseToSource()
        {
            try
            {
                var sourcePath = SourceFullPath;
                if (string.IsNullOrEmpty(sourcePath))
                {
                    return;
                }

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
                    // File doesn't exist, try to open the source directory
                    var directory = string.IsNullOrEmpty(SourceBasePath) ? null : SourceBasePath;
                    if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
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
            catch (Exception ex)
            {
                LogManager.GetLogger().Error(ex, "Error browsing to source");
            }
        }
    }
}