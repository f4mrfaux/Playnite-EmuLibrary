﻿using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using ProtoBuf;
using System.Collections.Generic;
using System.IO;

namespace EmuLibrary.RomTypes.SingleFile
{
    [ProtoContract]
    internal sealed class SingleFileGameInfo : ELGameInfo
    {
        public override RomType RomType => RomType.SingleFile;

        // Relative to Mapping's SourcePath
        [ProtoMember(1)]
        public string SourcePath { get; set; }  

        public string SourceFullPath
        {
            get
            {
                return Path.Combine(Mapping?.SourcePath ?? "", SourcePath);
            }
        }

        internal override InstallController GetInstallController(Game game, IEmuLibrary emuLibrary) =>
            new SingleFileInstallController(game, emuLibrary);

        internal override ELUninstallController GetUninstallController(Game game, IEmuLibrary emuLibrary) =>
            new SingleFileUninstallController(game, emuLibrary);

        protected override IEnumerable<string> GetDescriptionLines()
        {
            yield return $"{nameof(SourcePath)} : {SourcePath}";
            yield return $"{nameof(SourceFullPath)}* : {SourceFullPath}";
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
