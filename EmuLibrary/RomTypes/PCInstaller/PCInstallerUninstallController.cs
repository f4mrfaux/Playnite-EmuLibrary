using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EmuLibrary.RomTypes.PCInstaller
{
    class PCInstallerUninstallController : UninstallController
    {
        private readonly PCInstallerGameInfo _gameInfo;
        private readonly IEmuLibrary _emuLibrary;

        internal PCInstallerUninstallController(Game game, IEmuLibrary emuLibrary) : base(game)
        {
            Name = "Uninstall";
            _gameInfo = game.GetPCInstallerGameInfo();
            _emuLibrary = emuLibrary;
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            if (_gameInfo == null)
            {
                _emuLibrary.Logger.Error($"Game info is null for game {Game.Name}");
                _emuLibrary.Playnite.Dialogs.ShowErrorMessage($"Could not retrieve game information for {Game.Name}.", "Uninstallation Error");
                return;
            }

            if (string.IsNullOrEmpty(_gameInfo.InstallDirectory) || !Directory.Exists(_gameInfo.InstallDirectory))
            {
                _emuLibrary.Logger.Warn($"Install directory is missing or doesn't exist for game {Game.Name}: {_gameInfo.InstallDirectory}");
                
                // Still update the game status to uninstalled, even if directory doesn't exist
                InvokeOnUninstalled(new GameUninstalledEventArgs());
                
                return;
            }

            // No built-in progress reporting in UninstallActionArgs
            _emuLibrary.Logger.Info($"Uninstalling {Game.Name}");

            Task.Run(() =>
            {
                try
                {
                    // Check if the game has an uninstaller
                    string uninstallerPath = Path.Combine(_gameInfo.InstallDirectory, "uninstall.exe");
                    if (!File.Exists(uninstallerPath))
                    {
                        // Look for uninstaller using pattern matching (more robust than checking specific names)
                        var uninstallerPatterns = new[] { 
                            "uninstall*.exe", "uninst*.exe", "setup.exe", "uninstall_*.exe", 
                            "UNINSTALL*.EXE", "UNINST*.EXE", "SETUP.EXE", "UNINSTALL_*.EXE" 
                        };
                        
                        foreach (var pattern in uninstallerPatterns)
                        {
                            var matches = Directory.GetFiles(_gameInfo.InstallDirectory, pattern, SearchOption.AllDirectories);
                            if (matches.Length > 0)
                            {
                                uninstallerPath = matches[0];
                                break;
                            }
                        }
                    }

                    // If an uninstaller was found, run it
                    if (File.Exists(uninstallerPath))
                    {
                        _emuLibrary.Logger.Info($"Running uninstaller for {Game.Name}");
                        _emuLibrary.Logger.Info($"Running uninstaller: {uninstallerPath}");
                        
                        var startInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = uninstallerPath,
                            WorkingDirectory = Path.GetDirectoryName(uninstallerPath)
                        };
                        
                        using (var process = System.Diagnostics.Process.Start(startInfo))
                        {
                            _emuLibrary.Logger.Info($"Waiting for uninstaller to complete for {Game.Name}");
                            process.WaitForExit();
                            
                            _emuLibrary.Logger.Info($"Uninstaller completed for {Game.Name} with exit code {process.ExitCode}");
                        }
                    }
                    else
                    {
                        // If no uninstaller found, automatically delete the directory
                        _emuLibrary.Logger.Info($"No uninstaller found, deleting installation directory for {Game.Name}");
                        _emuLibrary.Logger.Info($"No uninstaller found for {Game.Name}, deleting directory: {_gameInfo.InstallDirectory}");
                        
                        try
                        {
                            Directory.Delete(_gameInfo.InstallDirectory, true);
                            _emuLibrary.Logger.Info($"Successfully deleted directory: {_gameInfo.InstallDirectory}");
                        }
                        catch (Exception ex)
                        {
                            _emuLibrary.Logger.Error($"Failed to delete directory {_gameInfo.InstallDirectory}: {ex.Message}");
                            _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                            {
                                _emuLibrary.Playnite.Dialogs.ShowErrorMessage(
                                    $"Failed to delete installation directory. You may need to delete it manually: {_gameInfo.InstallDirectory}",
                                    "Uninstallation Error");
                            });
                        }
                    }

                    // Clear internal fields on background thread
                    _gameInfo.InstallDirectory = null;
                    _gameInfo.PrimaryExecutable = null;

                    // Update game state on UI thread
                    _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                    {
                        using (_emuLibrary.Playnite.Database.BufferedUpdate())
                        {
                            Game.IsInstalled = false;
                            Game.IsInstalling = false;
                            Game.InstallDirectory = null;

                            // Update play action
                            var playAction = Game.GameActions?.FirstOrDefault(a => a.IsPlayAction);
                            if (playAction != null)
                            {
                                playAction.Path = "";
                                playAction.WorkingDir = null;
                                playAction.Name = "Install Game";
                                playAction.Type = GameActionType.URL;
                            }

                            // Update the game in the database
                            _emuLibrary.Playnite.Database.Games.Update(Game);
                            _emuLibrary.Logger.Info($"Game {Game.Name} marked as uninstalled");
                        }
                    });

                    InvokeOnUninstalled(new GameUninstalledEventArgs());
                    
                    _emuLibrary.Logger.Info($"{Game.Name} has been uninstalled");
                }
                catch (Exception ex)
                {
                    _emuLibrary.Logger.Error($"Error during uninstallation of game {Game.Name}: {ex.Message}");
                    _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                    {
                        _emuLibrary.Playnite.Dialogs.ShowErrorMessage(
                            $"An error occurred during uninstallation: {ex.Message}",
                            "Uninstallation Error");
                    });
                }
                finally
                {
                    // No progress view to close
                }
            });
        }
    }
}