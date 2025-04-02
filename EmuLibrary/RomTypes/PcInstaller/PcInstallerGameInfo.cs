using Playnite.SDK.Models;
using ProtoBuf;
using System.Collections.Generic;
using System.IO;

namespace EmuLibrary.RomTypes.PcInstaller
{
    [ProtoContract]
    internal sealed class PcInstallerGameInfo : ELGameInfo
    {
        public override RomType RomType => RomType.PcInstaller;

        // Path to installer relative to mapping source
        [ProtoMember(1)]
        public string SourcePath { get; set; }
        
        // Directory where the game is installed
        [ProtoMember(2)]
        public string InstallDirectory { get; set; }
        
        // Path to the main executable after installation
        [ProtoMember(3)]
        public string ExecutablePath { get; set; }
        
        // Flag indicating whether executable path was manually selected by user
        [ProtoMember(4)]
        public bool IsExecutablePathManuallySet { get; set; }
        
        // Full path to the installer
        public string SourceFullPath => Path.Combine(Mapping?.SourcePath ?? "", SourcePath);

        public override InstallController GetInstallController(Game game, IEmuLibrary emuLibrary) =>
            new PcInstallerInstallController(game, emuLibrary);

        public override UninstallController GetUninstallController(Game game, IEmuLibrary emuLibrary) =>
            new PcInstallerUninstallController(game, emuLibrary);

        protected override IEnumerable<string> GetDescriptionLines()
        {
            yield return $"{nameof(SourcePath)}: {SourcePath}";
            yield return $"{nameof(SourceFullPath)}*: {SourceFullPath}";
            
            if (!string.IsNullOrEmpty(InstallDirectory))
                yield return $"{nameof(InstallDirectory)}: {InstallDirectory}";
                
            if (!string.IsNullOrEmpty(ExecutablePath))
            {
                yield return $"{nameof(ExecutablePath)}: {ExecutablePath}";
                if (IsExecutablePathManuallySet)
                    yield return $"{nameof(IsExecutablePathManuallySet)}: True (User selected)";
            }
        }

        public override void BrowseToSource()
        {
            var psi = new System.Diagnostics.ProcessStartInfo()
            {
                FileName = "explorer.exe",
                Arguments = $"/e, /select, \"{Path.GetFullPath(SourceFullPath)}\""
            };
            System.Diagnostics.Process.Start(psi);
        }
    }
}