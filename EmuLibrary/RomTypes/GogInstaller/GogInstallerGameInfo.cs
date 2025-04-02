using System;
using System.IO;
using Playnite.SDK.Models;
using EmuLibrary.RomTypes;

namespace EmuLibrary.RomTypes.GogInstaller
{
    public class GogInstallerGameInfo : ELGameInfo
    {
        public GogInstallerGameInfo() : base(RomType.GogInstaller)
        {
        }

        public GogInstallerGameInfo(string name, string path) : base(RomType.GogInstaller)
        {
            Name = name;
            Path = path;
            RomExtension = Path.GetExtension(path);
            LastModified = File.GetLastWriteTime(path);
        }

        public override string GetDisplayName()
        {
            return Name;
        }

        public override string GetGameImagePath()
        {
            return Path;
        }
    }
}
