using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmuLibrary.RomTypes
{
    internal static class GogToPC_MigrationHelper
    {
        /// <summary>
        /// Migrates GOG installer games to use PC installer format
        /// </summary>
        public static void MigrateGogToPcInstaller(IPlayniteAPI playniteApi, ILogger logger)
        {
            try
            {
                logger.Info("Starting migration of GOG installer games to PC installer format");
                
                // Get all games that use the GOG installer plugin
                var gogGames = playniteApi.Database.Games
                    .Where(g => g.PluginId == EmuLibrary.PluginId && g.GameId.Contains(RomType.GogInstaller.ToString()))
                    .ToList();
                
                if (gogGames.Count == 0)
                {
                    logger.Info("No GOG installer games found, migration not needed");
                    return;
                }
                
                logger.Info($"Found {gogGames.Count} GOG installer games to migrate");
                
                using (playniteApi.Database.BufferedUpdate())
                {
                    foreach (var game in gogGames)
                    {
                        try
                        {
                            // Extract the original GOG game info
                            var gogGameInfo = game.GetELGameInfo();
                            if (gogGameInfo == null)
                            {
                                logger.Warn($"Could not get game info for {game.Name} [{game.GameId}], skipping migration");
                                continue;
                            }
                            
                            // Create a new PC installer game info with the same data
                            var pcGameInfo = new PcInstaller.PcInstallerGameInfo();
                            pcGameInfo.MappingId = gogGameInfo.MappingId;
                            
                            // Update the game ID to use the PC installer format
                            game.GameId = pcGameInfo.AsGameId();
                            
                            // Update the game in the database
                            playniteApi.Database.Games.Update(game);
                            logger.Info($"Successfully migrated GOG game: {game.Name}");
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, $"Error migrating GOG game {game.Name} [{game.GameId}]: {ex.Message}");
                        }
                    }
                }
                
                logger.Info("GOG to PC installer migration completed");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error during GOG to PC installer migration: " + ex.Message);
            }
        }
    }
}