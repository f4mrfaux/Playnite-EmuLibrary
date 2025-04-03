using System;
using System.IO;
using System.Collections.Generic;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using EmuLibrary.RomTypes;
using ProtoBuf;

namespace EmuLibrary.RomTypes.GogInstaller
{
    [ProtoContract]
    internal sealed class GogInstallerGameInfo : ELGameInfo
    {
        public override RomType RomType => RomType.GogInstaller;

        [ProtoMember(1)]
        public string Path { get; set; }

        [ProtoMember(2)]
        // We use a backing field for the Name property
        private string _name;
        
        // Override the Name property with a custom getter that returns our field
        public override string Name 
        { 
            get => _name; 
            protected set => _name = value;
        }

        [ProtoMember(3)]
        public string RomExtension { get; set; }

        [ProtoMember(4)]
        public DateTime LastModified { get; set; }

        public GogInstallerGameInfo()
        {
        }

        public GogInstallerGameInfo(string name, string path)
        {
            _name = name; // Set the backing field directly
            Path = path;
            RomExtension = System.IO.Path.GetExtension(path);
            LastModified = File.GetLastWriteTime(path);
        }

        public override InstallController GetInstallController(Game game, IEmuLibrary emuLibrary) =>
            new GogInstallerInstallController(game, emuLibrary);

        public override UninstallController GetUninstallController(Game game, IEmuLibrary emuLibrary) =>
            new GogInstallerUninstallController(game, emuLibrary);

        protected override IEnumerable<string> GetDescriptionLines()
        {
            yield return $"Name: {Name}";
            yield return $"Path: {Path}";
            yield return $"LastModified: {LastModified}";
        }

        // Name property is already defined on line 19
        // public string Name { get; set; }

        public override void BrowseToSource()
        {
            if (File.Exists(Path))
            {
                try
                {
                    string directory = System.IO.Path.GetDirectoryName(Path);
                    System.Diagnostics.Process.Start(directory);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error browsing to source: {ex.Message}");
                }
            }
        }
    }
}
