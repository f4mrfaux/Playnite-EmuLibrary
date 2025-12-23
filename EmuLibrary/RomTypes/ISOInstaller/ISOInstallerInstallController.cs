using EmuLibrary.Util;
using EmuLibrary.Util.FileCopier;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace EmuLibrary.RomTypes.ISOInstaller
{
    class ISOInstallerInstallController : BaseInstallController
    {
        private readonly ISOInstallerGameInfo _gameInfo;
        internal ISOInstallerInstallController(Game game, IEmuLibrary emuLibrary) : base(game, emuLibrary)
        {
            _gameInfo = game.GetISOInstallerGameInfo();
            _watcherToken = new CancellationTokenSource();
        }

        public override void Install(InstallActionArgs args)
        {
            if (_gameInfo.SourceBasePath == null)
            {
                _emuLibrary.Logger.Error($"Source base path is null for game {Game.Name}");
                _emuLibrary.Playnite.Dialogs.ShowErrorMessage($"Could not locate source path for game {Game.Name}.", "Installation Error");
                return;
            }

            if (string.IsNullOrEmpty(_gameInfo.SourcePath))
            {
                _emuLibrary.Logger.Error($"Source path is null or empty for game {Game.Name}");
                _emuLibrary.Playnite.Dialogs.ShowErrorMessage($"Source path is missing for game {Game.Name}.", "Installation Error");
                return;
            }

            if (string.IsNullOrEmpty(_gameInfo.InstallDirectory))
            {
                _emuLibrary.Logger.Error($"Install directory is null or empty for game {Game.Name}");
                _emuLibrary.Playnite.Dialogs.ShowErrorMessage($"Install directory is missing for game {Game.Name}.", "Installation Error");
                return;
            }

            // No built-in progress reporting in InstallActionArgs
            _emuLibrary.Logger.Info($"Installing {Game.Name}");

            Task.Run(async () =>
            {
                var cancellationToken = CancellationToken.None; // Initialize to default
                try
                {
                    var sourceISOPath = _gameInfo.SourceFullPath;
                    if (string.IsNullOrEmpty(sourceISOPath) || !File.Exists(sourceISOPath))
                    {
                        _emuLibrary.Logger.Error($"Source ISO file does not exist: {sourceISOPath ?? "null"}");
                        _emuLibrary.Playnite.Dialogs.ShowErrorMessage($"Source ISO file not found for {Game.Name}.", "Installation Error");
                        return;
                    }
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _emuLibrary.Logger.Info($"Installation cancelled for game {Game.Name}");
                        return;
                    }

                    _emuLibrary.Logger.Info($"Preparing to install {Game.Name}");
                    
                    // Ensure the install directory exists
                    if (!Directory.Exists(_gameInfo.InstallDirectory))
                    {
                        try
                        {
                            Directory.CreateDirectory(_gameInfo.InstallDirectory);
                        }
                        catch (Exception ex)
                        {
                            _emuLibrary.Logger.Error($"Failed to create install directory {_gameInfo.InstallDirectory}: {ex.Message}");
                            _emuLibrary.Playnite.Dialogs.ShowErrorMessage($"Could not create installation directory for {Game.Name}.", "Installation Error");
                            return;
                        }
                    }

                    // Mount the ISO image
                    _emuLibrary.Logger.Info($"Mounting disc image for {Game.Name}");
                    string mountedDriveLetter = await MountISOImage(sourceISOPath, args);
                    if (string.IsNullOrEmpty(mountedDriveLetter))
                    {
                        _emuLibrary.Logger.Error($"Failed to mount ISO image for game {Game.Name}");
                        _emuLibrary.Playnite.Dialogs.ShowErrorMessage($"Failed to mount ISO image for {Game.Name}.", "Mount Error");
                        return;
                    }

                    // Try to find and run installer from mounted image
                    try
                    {
                        _emuLibrary.Logger.Info($"Looking for installer in mounted disc for {Game.Name}");
                        
                        // Common installer file names to look for
                        var installerCandidates = new List<string>
                        {
                            "setup.exe", "install.exe", "autorun.exe", "start.exe", 
                            "SETUP.EXE", "INSTALL.EXE", "AUTORUN.EXE", "START.EXE"
                        };

                        // Look for setup EXE in root of the mounted drive
                        string installerPath = null;
                        foreach (var candidate in installerCandidates)
                        {
                            string fullPath = Path.Combine(mountedDriveLetter, candidate);
                            if (File.Exists(fullPath))
                            {
                                installerPath = fullPath;
                                break;
                            }
                        }

                        // If no installer found in root, try to find any EXE in the root
                        if (installerPath == null)
                        {
                            var exeFiles = Directory.GetFiles(mountedDriveLetter, "*.exe");
                            if (exeFiles.Length > 0)
                            {
                                installerPath = exeFiles[0];
                            }
                        }

                        if (installerPath == null)
                        {
                            _emuLibrary.Logger.Error($"No installer found in mounted ISO for game {Game.Name}");
                            _emuLibrary.Playnite.Dialogs.ShowErrorMessage($"Could not find an installer in the disc image for {Game.Name}.", "Installation Error");
                        }
                        else
                        {
                            // Run the installer
                            _emuLibrary.Logger.Info($"Running installer for {Game.Name}");
                            _emuLibrary.Logger.Info($"Running installer: {installerPath}");
                            
                            ProcessStartInfo startInfo = new ProcessStartInfo
                            {
                                FileName = installerPath,
                                WorkingDirectory = Path.GetDirectoryName(installerPath)
                            };
                            
                            using (Process process = Process.Start(startInfo))
                            {
                                if (process != null)
                                {
                                        _emuLibrary.Logger.Info($"Running installer for {Game.Name}. Please complete the installation process.");
                                    
                                    await Task.Run(() => process.WaitForExit());
                                    
                                    _emuLibrary.Logger.Info($"Installation process completed for {Game.Name}");
                                    
                                    // Update game info
                                    UpdateGameInfo();
                                }
                                else
                                {
                                    _emuLibrary.Logger.Error($"Failed to start installer process for game {Game.Name}");
                                    _emuLibrary.Playnite.Dialogs.ShowErrorMessage($"Failed to start the installer for {Game.Name}.", "Installation Error");
                                }
                            }
                        }
                    }
                    finally
                    {
                        // Always try to unmount the ISO
                        try
                        {
                            _emuLibrary.Logger.Info($"Unmounting disc image for {Game.Name}");
                            await UnmountISOImage(mountedDriveLetter);
                        }
                        catch (Exception ex)
                        {
                            _emuLibrary.Logger.Error($"Error unmounting ISO image: {ex.Message}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _emuLibrary.Logger.Info($"Installation cancelled for game {Game.Name}");
                }
                catch (Exception ex)
                {
                    _emuLibrary.Logger.Error($"Error during installation of game {Game.Name}: {ex.Message}");
                    _emuLibrary.Playnite.Dialogs.ShowErrorMessage($"An error occurred during installation: {ex.Message}", "Installation Error");
                }
                finally
                {
                    // No progress view to close
                }
            });
        }

        private void UpdateGameInfo()
        {
            // No need to assign InstallDirectory to itself
            
            // Try to find the main executable in the install directory
            try
            {
                var exeFiles = Directory.GetFiles(_gameInfo.InstallDirectory, "*.exe", SearchOption.AllDirectories);
                if (exeFiles.Length > 0)
                {
                    // Try to find common main executable names
                    var commonNames = new List<string> 
                    { 
                        "game.exe", "launcher.exe", "start.exe", "play.exe", "bin\\game.exe", "bin\\main.exe" 
                    };
                    
                    string mainExe = null;
                    foreach (var name in commonNames)
                    {
                        var potentialPath = Path.Combine(_gameInfo.InstallDirectory, name);
                        if (File.Exists(potentialPath))
                        {
                            mainExe = potentialPath;
                            break;
                        }
                    }
                    
                    // If no common name found, use the first exe in the root directory
                    if (mainExe == null)
                    {
                        var rootExes = Directory.GetFiles(_gameInfo.InstallDirectory, "*.exe", SearchOption.TopDirectoryOnly);
                        if (rootExes.Length > 0)
                        {
                            mainExe = rootExes[0];
                        }
                        else
                        {
                            // If no exe in root, use the first exe found anywhere
                            mainExe = exeFiles[0];
                        }
                    }
                    
                    if (mainExe != null)
                    {
                        _gameInfo.PrimaryExecutable = mainExe;
                    }
                }
            }
            catch (Exception ex)
            {
                _emuLibrary.Logger.Error($"Error finding primary executable: {ex.Message}");
            }
            
            Game.IsInstalled = true;
            Game.IsInstalling = false;
            Game.InstallDirectory = _gameInfo.InstallDirectory;
            
            // Update play action if we found a primary executable
            if (!string.IsNullOrEmpty(_gameInfo.PrimaryExecutable))
            {
                var playAction = Game.GameActions?.FirstOrDefault(a => a.IsPlayAction);
                if (playAction != null)
                {
                    playAction.Path = _gameInfo.PrimaryExecutable;
                    playAction.WorkingDir = _gameInfo.InstallDirectory;
                    playAction.Type = GameActionType.File;
                }
                else
                {
                    // Create new play action
                    Game.GameActions = Game.GameActions ?? new ObservableCollection<GameAction>();
                    Game.GameActions.Add(new GameAction
                    {
                        Name = "Play",
                        Path = _gameInfo.PrimaryExecutable,
                        WorkingDir = _gameInfo.InstallDirectory,
                        Type = GameActionType.File,
                        IsPlayAction = true
                    });
                }
            }
            
            // Update the game in the database
            try
            {
                _emuLibrary.Playnite.Database.Games.Update(Game);
                _emuLibrary.Logger.Info($"Game {Game.Name} installed successfully to {_gameInfo.InstallDirectory}");
                InvokeOnInstalled(new GameInstalledEventArgs());
            }
            catch (Exception ex)
            {
                _emuLibrary.Logger.Error($"Error updating game in database: {ex.Message}");
            }
        }

        private async Task<string> MountISOImage(string isoPath, InstallActionArgs args)
        {
            try
            {
                _emuLibrary.Logger.Info($"Mounting ISO image: {Path.GetFileName(isoPath)}");
                
                // Use PowerShell to mount the ISO since it's built into Windows
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"$mountResult = Mount-DiskImage -ImagePath '{isoPath}' -PassThru; $volume = Get-DiskImage -ImagePath '{isoPath}' | Get-Volume; Write-Output $volume.DriveLetter\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await Task.Run(() => process.WaitForExit());

                    if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                    {
                        _emuLibrary.Logger.Error($"Failed to mount ISO image: {error}");
                        return null;
                    }

                    string driveLetter = output.Trim();
                    if (string.IsNullOrWhiteSpace(driveLetter))
                    {
                        _emuLibrary.Logger.Error("Mount-DiskImage succeeded but no drive letter was returned");
                        return null;
                    }

                    // Ensure drive letter ends with a colon and backslash
                    if (!driveLetter.EndsWith(":\\"))
                    {
                        driveLetter = driveLetter.TrimEnd(':') + ":\\";
                    }

                    _emuLibrary.Logger.Info($"ISO image mounted at {driveLetter}");
                    return driveLetter;
                }
            }
            catch (Exception ex)
            {
                _emuLibrary.Logger.Error($"Error mounting ISO image: {ex.Message}");
                return null;
            }
        }

        private async Task UnmountISOImage(string driveLetter)
        {
            if (string.IsNullOrEmpty(driveLetter))
                return;

            try
            {
                // Use PowerShell to unmount by drive letter
                var driveLetter2 = driveLetter.TrimEnd(':', '\\');
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"$volume = Get-Volume -DriveLetter {driveLetter2}; $diskImage = Get-DiskImage -Volume $volume; Dismount-DiskImage -ImagePath $diskImage.ImagePath\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    string error = await process.StandardError.ReadToEndAsync();
                    await Task.Run(() => process.WaitForExit());

                    if (process.ExitCode != 0)
                    {
                        _emuLibrary.Logger.Error($"Failed to unmount ISO image: {error}");
                        return;
                    }

                    _emuLibrary.Logger.Info($"ISO image unmounted from {driveLetter}");
                }
            }
            catch (Exception ex)
            {
                _emuLibrary.Logger.Error($"Error unmounting ISO image: {ex.Message}");
            }
        }

    }
}