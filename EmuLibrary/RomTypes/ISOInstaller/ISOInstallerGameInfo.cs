using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace EmuLibrary.RomTypes.ISOInstaller
{
    internal class ISOInstallerGameInfo : ELGameInfo
    {
        // Enum to identify content types (needs to be identical to PCInstallerGameInfo.ContentType)
        public enum ContentType
        {
            BaseGame,    // The main game
            Update,      // An update/patch for the game
            DLC,         // Downloadable content
            Expansion    // Expansion pack (larger DLC)
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
        public ContentType ContentType { get; set; } = ContentType.BaseGame;
        
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
            return new ISOInstallerInstallController(game, emuLibrary);
        }

        internal override UninstallController GetUninstallController(Game game, IEmuLibrary emuLibrary)
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