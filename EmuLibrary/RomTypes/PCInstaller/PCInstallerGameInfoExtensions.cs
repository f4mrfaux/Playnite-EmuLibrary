using Playnite.SDK.Models;
using System;

namespace EmuLibrary.RomTypes.PCInstaller
{
    internal static class PCInstallerGameInfoExtensions
    {
        internal static PCInstallerGameInfo GetPCInstallerGameInfo(this Game game)
        {
            try
            {
                return ELGameInfo.FromGame<PCInstallerGameInfo>(game);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to deserialize game info for {game.Name}: {ex.Message}", ex);
            }
        }
    }
}