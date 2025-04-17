using Playnite.SDK.Models;
using System;

namespace EmuLibrary.RomTypes.ArchiveInstaller
{
    static class ArchiveInstallerGameInfoExtensions
    {
        public static ArchiveInstallerGameInfo GetArchiveInstallerGameInfo(this Game game)
        {
            try
            {
                return ELGameInfo.FromGame<ArchiveInstallerGameInfo>(game);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get ArchiveInstallerGameInfo from game {game.Name}: {ex.Message}", ex);
            }
        }

        public static ArchiveInstallerGameInfo GetArchiveInstallerGameInfo(this GameMetadata game)
        {
            try
            {
                return ELGameInfo.FromGameMetadata<ArchiveInstallerGameInfo>(game);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get ArchiveInstallerGameInfo from game metadata: {ex.Message}", ex);
            }
        }
    }
}