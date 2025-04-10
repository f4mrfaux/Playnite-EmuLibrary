using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
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
        internal PCInstallerInstallController(Game game, IEmuLibrary emuLibrary) : base(game, emuLibrary)
        { }

        public override void Install(InstallActionArgs args)
        {
            var info = Game.GetPCInstallerGameInfo();
            _watcherToken = new CancellationTokenSource();

            Task.Run(async () =>
            {
                try
                {
                    // Create a temporary directory for the installer
                    var tempDir = Path.Combine(Path.GetTempPath(), "Playnite_PCInstaller", Guid.NewGuid().ToString());
                    Directory.CreateDirectory(tempDir);
                    
                    // Copy the installer to the temp directory
                    var installerFileName = Path.GetFileName(info.SourceFullPath);
                    var tempInstallerPath = Path.Combine(tempDir, installerFileName);
                    File.Copy(info.SourceFullPath, tempInstallerPath);
                    
                    // Show a notification to the user
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
                    await Task.Run(() => process.WaitForExit());
                    
                    // Ask user to provide the installation directory
                    string installDir = null;
                    _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                    {
                        installDir = _emuLibrary.Playnite.Dialogs.SelectFolder($"Select installation directory for {Game.Name}");
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
                    
                    // Update the game info with the installation directory
                    info.InstallDirectory = installDir;
                    
                    // Find executable files in the installation directory for potential play action
                    var exeFiles = Directory.GetFiles(installDir, "*.exe", SearchOption.AllDirectories);
                    string primaryExe = null;
                    
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
                                
                                var selectedExe = _emuLibrary.Playnite.Dialogs.ChooseItemWithSearch(
                                    "Select primary executable for launching the game",
                                    exeOptions,
                                    null
                                );
                                
                                if (selectedExe != null)
                                {
                                    primaryExe = exeFiles.First(exe => 
                                        Path.GetFileName(exe).Equals(selectedExe, StringComparison.OrdinalIgnoreCase));
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
                        }
                    }
                    
                    // Clean up temp directory
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch (Exception ex)
                    {
                        _emuLibrary.Logger.Warn($"Failed to clean up temp directory: {ex.Message}");
                    }
                    
                    // Create GameInstallationData
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
                        
                        // Create a proper play action using the primary executable
                        installationData.GameActions = new List<GameAction>()
                        {
                            new GameAction()
                            {
                                Name = "Play",
                                Type = GameActionType.File, 
                                Path = primaryExe,
                                IsPlayAction = true
                            }
                        };
                    }
                    
                    // Notify completion and update game
                    InvokeOnInstalled(new GameInstalledEventArgs(installationData));
                }
                catch (Exception ex)
                {
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Failed to install {Game.Name}.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                        NotificationType.Error
                    );
                    Game.IsInstalling = false;
                    throw;
                }
            });
        }
    }
}