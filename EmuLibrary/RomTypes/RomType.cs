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
        
        // PC installers don't require an emulator profile since they're native executables
        [RomTypeInfo(typeof(PCInstaller.PCInstallerGameInfo), typeof(PCInstaller.PCInstallerScanner))]
        PCInstaller = 5,
        
        // ISO installers don't require an emulator profile since they're mounted and installed natively
        [RomTypeInfo(typeof(ISOInstaller.ISOInstallerGameInfo), typeof(ISOInstaller.ISOInstallerScanner))]
        ISOInstaller = 6,
        
        // Archive installer functionality has been removed
        // Keep this enum value for backward compatibility with saved settings
        ArchiveInstaller = 7,
    }
}
