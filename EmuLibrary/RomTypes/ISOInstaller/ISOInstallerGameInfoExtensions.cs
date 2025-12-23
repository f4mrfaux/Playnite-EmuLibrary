using System;
using System.IO;
using Playnite.SDK.Models;

namespace EmuLibrary.RomTypes.ISOInstaller
{
    public static class ISOInstallerGameInfoExtensions
    {
        public static ISOInstallerGameInfo GetISOInstallerGameInfo(this Game game)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            if (game.GameId == null)
            {
                throw new ArgumentException("Game ID is null, not a valid EmuLibrary game", nameof(game));
            }

            try
            {
                // Look for the new format with the ProtoBuf serialization
                if (game.GameId.StartsWith("!"))
                {
                    return ELGameInfo.FromGame<ISOInstallerGameInfo>(game);
                }
                
                // Legacy format support for backward compatibility
                var parts = game.GameId.Split('|');
                if (parts.Length < 2 || parts[0] != "ISOInstaller")
                {
                    throw new ArgumentException("Game ID does not match ISO Installer format", nameof(game));
                }

                var gameInfo = new ISOInstallerGameInfo();
                if (Guid.TryParse(parts[1], out Guid mappingId))
                {
                    gameInfo.MappingId = mappingId;
                }
                
                if (parts.Length > 2)
                {
                    gameInfo.SourcePath = parts[2] == string.Empty ? null : parts[2];
                }
                
                if (parts.Length > 3)
                {
                    gameInfo.InstallDirectory = parts[3] == string.Empty ? null : parts[3];
                }

                return gameInfo;
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to parse game ID: {ex.Message}", nameof(game), ex);
            }
        }

        public static bool IsISOInstallerGame(this Game game)
        {
            if (game?.GameId == null)
            {
                return false;
            }

            // Check for new format (ProtoBuf serialized)
            if (game.GameId.StartsWith("!"))
            {
                try
                {
                    var info = ELGameInfo.FromGame<ELGameInfo>(game);
                    return info.RomType == RomType.ISOInstaller;
                }
                catch
                {
                    return false;
                }
            }

            // Legacy format check
            var parts = game.GameId.Split('|');
            return parts.Length >= 2 && parts[0] == "ISOInstaller";
        }
    }
}