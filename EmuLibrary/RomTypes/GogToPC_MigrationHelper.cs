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
                            
                            // Try to extract additional fields from the game properties if available
                            try {
                                // Check if we can access any additional data from the game object
                                var gameData = game.GameData;
                                if (gameData != null && gameData.Contains("SourcePath"))
                                {
                                    pcGameInfo.SourcePath = gameData["SourcePath"].ToString();
                                }
                                
                                if (gameData != null && gameData.Contains("InstallDirectory"))
                                {
                                    pcGameInfo.InstallDirectory = gameData["InstallDirectory"].ToString();
                                }
                                
                                if (gameData != null && gameData.Contains("ExecutablePath"))
                                {
                                    pcGameInfo.ExecutablePath = gameData["ExecutablePath"].ToString();
                                    pcGameInfo.IsExecutablePathManuallySet = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.Warn($"Error extracting additional data for {game.Name}: {ex.Message}");
                                // Continue with the migration even if we can't get all the data
                            }
                            
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