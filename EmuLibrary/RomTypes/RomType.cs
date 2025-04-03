namespace EmuLibrary.RomTypes
{
    // Don't renumber these, ever. Saved mappings and ELGameInfo field numbers rely on them being static
    public enum RomType
    {
        [RomTypeInfo(typeof(SingleFile.SingleFileGameInfo), typeof(SingleFile.SingleFileScanner))]
        SingleFile = 0,

        [RomTypeInfo(typeof(MultiFile.MultiFileGameInfo), typeof(MultiFile.MultiFileScanner))]
        MultiFile = 1,

        [RomTypeInfo(typeof(Yuzu.YuzuGameInfo), typeof(Yuzu.YuzuScanner))]
        Yuzu = 4,
        // Now GogInstaller uses the PcInstaller implementation
        [RomTypeInfo(typeof(PcInstaller.PcInstallerGameInfo), typeof(PcInstaller.PcInstallerScanner))]
        GogInstaller = 5,
        [RomTypeInfo(typeof(PcInstaller.PcInstallerGameInfo), typeof(PcInstaller.PcInstallerScanner))]
        PcInstaller = 6,
    }
}
