using Playnite.SDK.Models;
using System;
using System.Linq;

namespace EmuLibrary.RomTypes.PCInstaller
{
    internal static class PCInstallerGameInfoExtensions
    {
        internal static PCInstallerGameInfo GetPCInstallerGameInfo(this Game game)
        {
            try
            {
                var gameInfo = ELGameInfo.FromGame<PCInstallerGameInfo>(game);
                
                // Restore installation state from Game object (since it's excluded from stable GameId)
                // This ensures we have the full game info even though GameId doesn't include installation fields
                if (string.IsNullOrEmpty(gameInfo.InstallDirectory) && !string.IsNullOrEmpty(game.InstallDirectory))
                {
                    gameInfo.InstallDirectory = game.InstallDirectory;
                }
                
                // Try to restore PrimaryExecutable from GameActions if available
                if (string.IsNullOrEmpty(gameInfo.PrimaryExecutable) && game.IsInstalled)
                {
                    var playAction = game.GameActions?.FirstOrDefault(a => a.IsPlayAction);
                    if (playAction != null && !string.IsNullOrEmpty(playAction.Path))
                    {
                        gameInfo.PrimaryExecutable = playAction.Path;
                    }
                }
                
                return gameInfo;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to deserialize game info for {game.Name}: {ex.Message}", ex);
            }
        }
    }
}