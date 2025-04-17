using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace EmuLibrary.RomTypes.ArchiveInstaller
{
    [ProtoContract]
    internal class ArchiveInstallerGameInfo : ELGameInfo
    {
        // Enum to identify content types (needs to be identical to PCInstallerGameInfo.ContentType)
        public enum ContentType
        {
            BaseGame,    // The main game
            Update,      // An update/patch for the game
            DLC,         // Downloadable content
            Expansion    // Expansion pack (larger DLC)
        }
        
        public override RomType RomType => RomType.ArchiveInstaller;

        // Relative to Mapping's SourcePath
        [ProtoMember(1)]
        public string SourcePath { get; set; }

        [ProtoMember(2)]
        public string ArchivePassword { get; set; }
        
        // For multi-part archives, store the main archive path
        [ProtoMember(3)]
        public string MainArchivePath { get; set; }

        // Store paths to all parts of a multi-part archive
        [ProtoMember(4)]
        public List<string> ArchiveParts { get; set; }

        // Information about extracted ISO(s) from the archive
        [ProtoMember(5)]
        public string ExtractedISOPath { get; set; }
        
        // Installation directory if installed
        [ProtoMember(6)]
        public string InstallDirectory { get; set; }
        
        // Path to the primary executable after installation
        [ProtoMember(7)]
        public string PrimaryExecutable { get; set; }
        
        // Store GOG/Steam/etc. specific game identifier
        [ProtoMember(8)]
        public string StoreGameId { get; set; }
        
        // Store installer type (GOG, Steam, Epic, etc.)
        [ProtoMember(9)]
        public string InstallerType { get; set; }

        // Local temp directory where we extracted the archive
        [ProtoMember(10)]
        public string ExtractedArchiveDir { get; set; }

        // Imported archive path (local temp copy)
        [ProtoMember(11)]
        public string ImportedArchivePath { get; set; }

        // ISO mount point if applicable
        [ProtoMember(12)]
        public string MountPoint { get; set; }

        // Selected installer from mounted ISO
        [ProtoMember(13)]
        public string SelectedInstaller { get; set; }
        
        // Content type identification fields
        [ProtoMember(14)]
        public ContentType ContentTypeValue { get; set; } = ContentType.BaseGame;
        
        [ProtoMember(15)]
        public string ParentGameId { get; set; }
        
        [ProtoMember(16)]
        public string Version { get; set; }
        
        [ProtoMember(17)]
        public List<string> InstalledAddons { get; set; } = new List<string>();
        
        [ProtoMember(18)]
        public string ContentDescription { get; set; }

        public string SourceFullPath
        {
            get
            {
                var settings = Settings.Settings.Instance;
                var mapping = settings.GetMapping(MappingId);
                if (mapping == null)
                {
                    return null;
                }

                return Path.Combine(mapping.SourcePath, SourcePath);
            }
        }
        
        internal override InstallController GetInstallController(Game game, IEmuLibrary emuLibrary)
        {
            return new ArchiveInstallerInstallController(game, emuLibrary);
        }

        internal override UninstallController GetUninstallController(Game game, IEmuLibrary emuLibrary)
        {
            return new ArchiveInstallerUninstallController(game, emuLibrary);
        }

        protected override IEnumerable<string> GetDescriptionLines()
        {
            yield return $"{nameof(SourcePath)}: {SourcePath}";
            
            if (!string.IsNullOrEmpty(MainArchivePath))
                yield return $"{nameof(MainArchivePath)}: {MainArchivePath}";
                
            if (ArchiveParts != null && ArchiveParts.Count > 0)
                yield return $"Archive Parts: {ArchiveParts.Count}";
                
            if (!string.IsNullOrEmpty(ExtractedISOPath))
                yield return $"{nameof(ExtractedISOPath)}: {ExtractedISOPath}";
                
            if (!string.IsNullOrEmpty(InstallDirectory))
                yield return $"{nameof(InstallDirectory)}: {InstallDirectory}";
                
            if (!string.IsNullOrEmpty(PrimaryExecutable))
                yield return $"{nameof(PrimaryExecutable)}: {PrimaryExecutable}";
                
            if (!string.IsNullOrEmpty(StoreGameId))
                yield return $"{nameof(StoreGameId)}: {StoreGameId}";
                
            if (!string.IsNullOrEmpty(InstallerType))
                yield return $"{nameof(InstallerType)}: {InstallerType}";
                
            // Content type information
            if (ContentTypeValue != ContentType.BaseGame)
                yield return $"{nameof(ContentTypeValue)}: {ContentTypeValue}";
                
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
            try
            {
                var fullPath = SourceFullPath;
                var parentDir = Path.GetDirectoryName(fullPath);
                if (Directory.Exists(parentDir))
                {
                    if (System.Environment.OSVersion.Platform == System.PlatformID.Win32NT)
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo()
                        {
                            FileName = "explorer.exe",
                            Arguments = $"/select,\"{Path.GetFullPath(fullPath)}\""
                        };
                        System.Diagnostics.Process.Start(psi);
                    }
                    else
                    {
                        // For non-Windows platforms, open the directory
                        Process.Start(parentDir);
                    }
                }
            }
            catch (Exception ex)
            {
                Settings.Settings.Instance.EmuLibrary.Logger.Error($"Failed to browse to source: {ex.Message}");
            }
        }
    }
}