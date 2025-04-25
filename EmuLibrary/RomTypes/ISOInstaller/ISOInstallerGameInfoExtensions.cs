using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.IO;

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
        
        /// <summary>
        /// Ensures ISO paths are preserved after any metadata update operations
        /// </summary>
        public static void EnsureISOPathsPreserved(this Game game, bool updateGame = true)
        {
            try
            {
                if (game == null || string.IsNullOrEmpty(game.GameId) || !game.GameId.StartsWith("!"))
                    return;
                    
                var logger = LogManager.GetLogger();
                
                // Get the game info
                ISOInstallerGameInfo isoInfo;
                try
                {
                    isoInfo = game.GetISOInstallerGameInfo();
                }
                catch
                {
                    // Not an ISO installer game
                    return;
                }
                
                logger.Info($"Checking ISO paths for {game.Name}: SourcePath={isoInfo.SourcePath}, InstallerFullPath={isoInfo.InstallerFullPath}");
                
                // Check if we need to fix paths
                bool pathsNeedUpdate = false;
                
                // If InstallerFullPath is empty or invalid but SourcePath has content, try to resolve
                if ((string.IsNullOrEmpty(isoInfo.InstallerFullPath) || !File.Exists(isoInfo.InstallerFullPath)) 
                    && !string.IsNullOrEmpty(isoInfo.SourcePath))
                {
                    // Force path resolution
                    var resolvedPath = isoInfo.SourceFullPath;
                    if (!string.IsNullOrEmpty(resolvedPath) && File.Exists(resolvedPath))
                    {
                        logger.Info($"Updating ISO path for {game.Name} to: {resolvedPath}");
                        isoInfo.InstallerFullPath = resolvedPath;
                        pathsNeedUpdate = true;
                    }
                }
                
                // If we made changes, update the game
                if (pathsNeedUpdate && updateGame)
                {
                    // Update the game ID to persist changes
                    game.GameId = isoInfo.AsGameId();
                    
                    // Get API reference from any existing game
                    var settings = Settings.Settings.Instance;
                    if (settings?.EmuLibrary?.Playnite != null)
                    {
                        settings.EmuLibrary.Playnite.Database.Games.Update(game);
                        logger.Info($"Updated game {game.Name} with preserved ISO paths");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.GetLogger().Error($"Error in EnsureISOPathsPreserved: {ex.Message}");
            }
        }
    }
}