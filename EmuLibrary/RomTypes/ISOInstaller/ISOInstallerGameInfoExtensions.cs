using Playnite.SDK.Models;
using System;

namespace EmuLibrary.RomTypes.ISOInstaller
{
    internal static class ISOInstallerGameInfoExtensions
    {
        public static ISOInstallerGameInfo GetISOInstallerGameInfo(this Game game)
        {
            var elInfo = game.GetELGameInfo();
            if (elInfo.RomType != RomType.ISOInstaller)
            {
                throw new InvalidOperationException($"Game {game.Name} is not an ISO installer game");
            }

            return elInfo as ISOInstallerGameInfo;
        }
    }
}