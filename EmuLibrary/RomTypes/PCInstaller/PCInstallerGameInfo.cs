using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using ProtoBuf;
using System.Collections.Generic;
using System.IO;

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
        [ProtoMember(3)]
        public string InstallDirectory { get; set; }
        
        // Path to the primary executable after installation
        [ProtoMember(4)]
        public string PrimaryExecutable { get; set; }
        
        // Store GOG/Steam/etc. specific game identifier
        [ProtoMember(5)]
        public string StoreGameId { get; set; }
        
        // Store installer type (GOG, Steam, Epic, etc.)
        [ProtoMember(6)]
        public string InstallerType { get; set; }

        public string SourceFullPath
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(SourcePath))
                    {
                        return InstallerFullPath;
                    }

                    var mapping = Mapping;
                    if (mapping == null || string.IsNullOrEmpty(mapping.SourcePath))
                    {
                        // Fallback to direct path if mapping is missing
                        return InstallerFullPath;
                    }

                    return Path.Combine(mapping.SourcePath, SourcePath);
                }
                catch (System.Exception ex)
                {
                    LogManager.GetLogger().Error(ex, "Error getting source full path");
                    return InstallerFullPath;
                }
            }
        }

        internal override InstallController GetInstallController(Game game, IEmuLibrary emuLibrary) =>
            new PCInstallerInstallController(game, emuLibrary);

        internal override UninstallController GetUninstallController(Game game, IEmuLibrary emuLibrary) =>
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