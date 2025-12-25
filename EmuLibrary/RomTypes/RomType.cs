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
        
        [RomTypeInfo(typeof(PCInstaller.PCInstallerGameInfo), typeof(PCInstaller.PCInstallerScanner))]
        PCInstaller = 5,
        
        /// <summary>
        /// DEPRECATED: ISOInstaller is kept for backward compatibility but is hidden from the UI.
        /// All ISO functionality is now handled by PCInstaller, which is a unified handler for
        /// EXE files, ISO disc images, and archives (ZIP/RAR/7Z).
        /// </summary>
        [RomTypeInfo(typeof(ISOInstaller.ISOInstallerGameInfo), typeof(ISOInstaller.ISOInstallerScanner))]
        ISOInstaller = 6,
    }
}
