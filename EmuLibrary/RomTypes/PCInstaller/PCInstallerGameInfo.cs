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

        public string SourceFullPath
        {
            get
            {
                return Path.Combine(Mapping?.SourcePath ?? "", SourcePath);
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