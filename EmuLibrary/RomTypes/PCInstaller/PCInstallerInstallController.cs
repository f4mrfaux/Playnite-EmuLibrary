using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace EmuLibrary.RomTypes.PCInstaller
{
    class PCInstallerInstallController : BaseInstallController
    {
        private enum InstallState
        {
            NotStarted,
            PreparingFiles,
            RunningInstaller,
            SelectingInstallDir,
            ConfiguringGame,
            CleaningUp,
            Completed,
            Failed
        }

        internal PCInstallerInstallController(Game game, IEmuLibrary emuLibrary) : base(game, emuLibrary)
        { }

        public override void Install(InstallActionArgs args)
        {
            var info = Game.GetPCInstallerGameInfo();
            _watcherToken = new CancellationTokenSource();
            
            // Link to the global cancellation token if provided
            var cancellationToken = _watcherToken.Token;
            if (args.CancelToken != null)
            {
                args.CancelToken.Register(() => _watcherToken.Cancel());
            }

            Task.Run(async () =>
            {
                var currentState = InstallState.NotStarted;
                
                try
                {
                    // Create a temporary directory for the installer
                    currentState = InstallState.PreparingFiles;
                    UpdateProgress("Creating temporary directory...", 0);
                    
                    var tempDir = Path.Combine(Path.GetTempPath(), "Playnite_PCInstaller", Guid.NewGuid().ToString());
                    Directory.CreateDirectory(tempDir);
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _emuLibrary.Logger.Info($"Installation of {Game.Name} was cancelled during preparation");
                        Game.IsInstalling = false;
                        return;
                    }
                    
                    // Verify source file exists
                    if (!File.Exists(info.SourceFullPath))
                    {
                        throw new FileNotFoundException($"Installer file not found: {info.SourceFullPath}");
                    }
                    
                    // Copy the installer to the temp directory
                    UpdateProgress("Copying installer file...", 10);
                    var installerFileName = Path.GetFileName(info.SourceFullPath);
                    var tempInstallerPath = Path.Combine(tempDir, installerFileName);
                    File.Copy(info.SourceFullPath, tempInstallerPath);
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _emuLibrary.Logger.Info($"Installation of {Game.Name} was cancelled after file copy");
                        Game.IsInstalling = false;
                        return;
                    }
                    
                    // Show a notification to the user
                    currentState = InstallState.RunningInstaller;
                    UpdateProgress("Running installer...", 20);
                    
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Running installer for {Game.Name}. Follow the installation prompts.",
                        NotificationType.Info
                    );
                    
                    // Execute the installer
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = tempInstallerPath,
                            WorkingDirectory = tempDir,
                            UseShellExecute = true
                        }
                    };
                    
                    process.Start();
                    
                    // Don't create nested tasks - use await directly
                    await Task.Run(() => 
                    {
                        while (!process.HasExited)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                try
                                {
                                    process.Kill();
                                    _emuLibrary.Logger.Info($"Installation process for {Game.Name} was cancelled and terminated");
                                    Game.IsInstalling = false;
                                    return;
                                }
                                catch (Exception ex)
                                {
                                    _emuLibrary.Logger.Error($"Failed to kill installation process: {ex.Message}", ex);
                                }
                            }
                            Thread.Sleep(500);
                        }
                        process.WaitForExit();
                    }, cancellationToken);
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _emuLibrary.Logger.Info($"Installation of {Game.Name} was cancelled after installer execution");
                        Game.IsInstalling = false;
                        return;
                    }
                    
                    // Ask user to provide the installation directory
                    currentState = InstallState.SelectingInstallDir;
                    UpdateProgress("Selecting installation directory...", 70);
                    
                    string installDir = null;
                    _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                    {
                        installDir = _emuLibrary.Playnite.Dialogs.SelectFolder();
                    });
                    
                    if (string.IsNullOrEmpty(installDir))
                    {
                        _emuLibrary.Playnite.Notifications.Add(
                            Game.GameId,
                            $"Installation of {Game.Name} was cancelled because no installation directory was selected.",
                            NotificationType.Error
                        );
                        Game.IsInstalling = false;
                        return;
                    }
                    
                    // Verify the selected directory exists
                    if (!Directory.Exists(installDir))
                    {
                        _emuLibrary.Logger.Error($"Selected installation directory does not exist: {installDir}");
                        throw new DirectoryNotFoundException($"Selected installation directory does not exist: {installDir}");
                    }
                    
                    // Update the game info with the installation directory
                    currentState = InstallState.ConfiguringGame;
                    UpdateProgress("Configuring game...", 80);
                    
                    info.InstallDirectory = installDir;
                    
                    // Find executable files in the installation directory for potential play action
                    string primaryExe = null;
                    try
                    {
                        var exeFiles = Directory.GetFiles(installDir, "*.exe", SearchOption.AllDirectories);
                        
                        if (exeFiles.Length > 0)
                        {
                            // Try to find a primary exe (launcher.exe, game.exe, etc.)
                            var commonMainExeNames = new[] { "launcher.exe", "game.exe", Game.Name.ToLower() + ".exe" };
                            
                            foreach (var commonName in commonMainExeNames)
                            {
                                var matchingExe = exeFiles.FirstOrDefault(exe => 
                                    Path.GetFileName(exe).Equals(commonName, StringComparison.OrdinalIgnoreCase));
                                
                                if (matchingExe != null)
                                {
                                    primaryExe = matchingExe;
                                    break;
                                }
                            }
                            
                            // If no primary exe found, ask user to select one
                            if (primaryExe == null && exeFiles.Length > 1)
                            {
                                _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                                {
                                    var exeOptions = exeFiles.Select(Path.GetFileName).ToList();
                                    var selectedExe = _emuLibrary.Playnite.Dialogs.SelectString(
                                        "Select primary executable for launching the game",
                                        exeOptions
                                    );
                                    
                                    if (selectedExe != null)
                                    {
                                        primaryExe = exeFiles.First(exe => 
                                            string.Equals(Path.GetFileName(exe), selectedExe, StringComparison.OrdinalIgnoreCase));
                                    }
                                    else
                                    {
                                        // If no selection, use the first exe
                                        primaryExe = exeFiles[0];
                                    }
                                });
                            }
                            else if (primaryExe == null && exeFiles.Length == 1)
                            {
                                // If only one exe, use it
                                primaryExe = exeFiles[0];
                            }
                            
                            // Store the primary executable path in the game info
                            if (!string.IsNullOrEmpty(primaryExe))
                            {
                                info.PrimaryExecutable = primaryExe;
                                _emuLibrary.Logger.Info($"Selected primary executable for {Game.Name}: {primaryExe}");
                            }
                        }
                        else
                        {
                            _emuLibrary.Logger.Warn($"No executable files found in installation directory: {installDir}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _emuLibrary.Logger.Error($"Error finding executable files: {ex.Message}", ex);
                    }
                    
                    // Clean up temp directory
                    currentState = InstallState.CleaningUp;
                    UpdateProgress("Cleaning up...", 90);
                    
                    try
                    {
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the full exception details
                        _emuLibrary.Logger.Warn($"Failed to clean up temp directory: {ex.Message}", ex);
                    }
                    
                    // Create GameInstallationData
                    currentState = InstallState.Completed;
                    UpdateProgress("Finalizing installation...", 95);
                    
                    var installationData = new GameInstallationData
                    {
                        InstallDirectory = installDir
                    };
                    
                    if (!string.IsNullOrEmpty(primaryExe))
                    {
                        installationData.Roms = new List<GameRom> 
                        { 
                            new GameRom(Game.Name, primaryExe) 
                        };
                        
                        // Use buffered update for game actions to reduce UI events
                        using (_emuLibrary.Playnite.Database.BufferedUpdate())
                        {
                            // Add game actions that will be applied to the game
                            if (Game.GameActions == null)
                            {
                                Game.GameActions = new ObservableCollection<GameAction>();
                            }
                            else
                            {
                                Game.GameActions.Clear();
                            }
                            
                            Game.GameActions.Add(new GameAction()
                            {
                                Name = "Play",
                                Type = GameActionType.File, 
                                Path = primaryExe,
                                IsPlayAction = true
                            });
                        }
                    }
                    
                    // Update progress to 100%
                    UpdateProgress("Installation complete", 100);
                    
                    // Notify completion and update game
                    InvokeOnInstalled(new GameInstalledEventArgs(installationData));
                }
                catch (OperationCanceledException)
                {
                    _emuLibrary.Logger.Info($"Installation of {Game.Name} was cancelled");
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Installation of {Game.Name} was cancelled.",
                        NotificationType.Info
                    );
                    Game.IsInstalling = false;
                }
                catch (Exception ex)
                {
                    currentState = InstallState.Failed;
                    _emuLibrary.Logger.Error($"Failed to install {Game.Name}: {ex.Message}", ex);
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Failed to install {Game.Name}.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                        NotificationType.Error
                    );
                    Game.IsInstalling = false;
                    throw; // Rethrow without wrapping to preserve stack trace
                }
            });
        }
        
        private void UpdateProgress(string status, int progressPercentage)
        {
            _emuLibrary.Logger.Debug($"Install progress for {Game.Name}: {status} ({progressPercentage}%)");
            InvokeOnProgress(new InstallProgressEventArgs()
            {
                Status = status,
                ProgressPercentage = progressPercentage
            });
        }
    }
}