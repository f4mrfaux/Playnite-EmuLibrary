using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using EmuLibrary.Util;
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
            
            // Use the local cancellation token
            var cancellationToken = _watcherToken.Token;

            Task.Run(async () =>
            {
                // Track installation state for logging
                
                try
                {
                    // Create a temporary directory for the installer
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
                    
                    // Check if source file is a direct ISO file
                    if (Path.GetExtension(info.SourceFullPath).Equals(".iso", StringComparison.OrdinalIgnoreCase))
                    {
                        // Handle direct ISO file
                        UpdateProgress("Preparing ISO installation...", 10);
                        _emuLibrary.Logger.Info($"Detected direct ISO file: {Path.GetFileName(info.SourceFullPath)}");
                        
                        // Ask user for installation directory
                        UpdateProgress("Selecting installation directory...", 20);
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
                        
                        // Ensure install directory exists
                        if (!Directory.Exists(installDir))
                        {
                            try
                            {
                                Directory.CreateDirectory(installDir);
                            }
                            catch (Exception ex)
                            {
                                _emuLibrary.Logger.Error($"Failed to create install directory {installDir}: {ex.Message}");
                                throw new Exception($"Could not create installation directory: {ex.Message}");
                            }
                        }
                        
                        // Mount and install from ISO
                        await HandleISOInstallation(info.SourceFullPath, installDir, cancellationToken);
                        
                        // Update game info
                        info.InstallDirectory = installDir;
                        
                        // Finalize installation
                        UpdateProgress("Installation complete", 100);
                        var installationData = new GameInstallationData
                        {
                            InstallDirectory = installDir
                        };
                        InvokeOnInstalled(new GameInstalledEventArgs(installationData));
                        return;
                    }
                    
                    string tempInstallerPath = null;
                    string extractedContentDir = null;
                    
                    // Check if source file is an archive
                    if (ArchiveExtractor.IsArchiveFile(info.SourceFullPath))
                    {
                        // Extract archive to temp directory
                        UpdateProgress("Extracting archive...", 10);
                        _emuLibrary.Logger.Info($"Detected archive file: {Path.GetFileName(info.SourceFullPath)}");
                        
                        extractedContentDir = Path.Combine(tempDir, "extracted");
                        Directory.CreateDirectory(extractedContentDir);
                        
                        var extractor = new ArchiveExtractor(_emuLibrary.Logger);
                        var extractSuccess = await extractor.ExtractArchiveAsync(
                            info.SourceFullPath,
                            extractedContentDir,
                            cancellationToken
                        );
                        
                        if (!extractSuccess)
                        {
                            throw new Exception($"Failed to extract archive: {info.SourceFullPath}");
                        }
                        
                        if (cancellationToken.IsCancellationRequested)
                        {
                            _emuLibrary.Logger.Info($"Installation of {Game.Name} was cancelled after extraction");
                            Game.IsInstalling = false;
                            return;
                        }
                        
                        // Detect content type after extraction
                        var contentInfo = ArchiveExtractor.DetectContentType(extractedContentDir);
                        
                        if (contentInfo.HasIsoFiles && !contentInfo.HasExeFiles)
                        {
                            // Archive contains ISO files - handle as ISO
                            _emuLibrary.Logger.Info($"Archive contains ISO files. Handling as ISO for {Game.Name}");
                            
                            // Use the first ISO file found
                            var isoPath = contentInfo.IsoFiles[0];
                            if (contentInfo.IsoFiles.Count > 1)
                            {
                                _emuLibrary.Logger.Info($"Archive contains {contentInfo.IsoFiles.Count} ISO files. Using first one: {Path.GetFileName(isoPath)}");
                            }
                            
                            // Ask user for installation directory
                            UpdateProgress("Selecting installation directory...", 30);
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
                            
                            // Ensure install directory exists
                            if (!Directory.Exists(installDir))
                            {
                                try
                                {
                                    Directory.CreateDirectory(installDir);
                                }
                                catch (Exception ex)
                                {
                                    _emuLibrary.Logger.Error($"Failed to create install directory {installDir}: {ex.Message}");
                                    throw new Exception($"Could not create installation directory: {ex.Message}");
                                }
                            }
                            
                            // Mount and install from ISO
                            await HandleISOInstallation(isoPath, installDir, cancellationToken);
                            
                            // Update game info
                            info.InstallDirectory = installDir;
                            
                            // Clean up temp directory
                            try
                            {
                                if (Directory.Exists(tempDir))
                                {
                                    Directory.Delete(tempDir, true);
                                }
                            }
                            catch (Exception ex)
                            {
                                _emuLibrary.Logger.Warn($"Failed to clean up temp directory: {ex.Message}");
                            }
                            
                            // Finalize installation
                            UpdateProgress("Installation complete", 100);
                            var installationData = new GameInstallationData
                            {
                                InstallDirectory = installDir
                            };
                            InvokeOnInstalled(new GameInstalledEventArgs(installationData));
                            return;
                        }
                        else if (contentInfo.HasExeFiles)
                        {
                            // Find the primary installer EXE
                            var installerCandidates = new[] { "setup.exe", "install.exe", "autorun.exe", "game.exe" };
                            tempInstallerPath = contentInfo.ExeFiles.FirstOrDefault(exe =>
                                installerCandidates.Any(candidate =>
                                    Path.GetFileName(exe).Equals(candidate, StringComparison.OrdinalIgnoreCase)
                                )
                            );
                            
                            // If no common installer name found, use the first EXE
                            if (string.IsNullOrEmpty(tempInstallerPath))
                            {
                                tempInstallerPath = contentInfo.ExeFiles[0];
                            }
                            
                            _emuLibrary.Logger.Info($"Found installer in extracted archive: {tempInstallerPath}");
                        }
                        else
                        {
                            throw new Exception($"Extracted archive does not contain any executable files or ISO files.");
                        }
                    }
                    else
                    {
                        // Not an archive - copy the installer to the temp directory
                        UpdateProgress("Copying installer file...", 10);
                        var installerFileName = Path.GetFileName(info.SourceFullPath);
                        tempInstallerPath = Path.Combine(tempDir, installerFileName);
                        File.Copy(info.SourceFullPath, tempInstallerPath);
                    }
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _emuLibrary.Logger.Info($"Installation of {Game.Name} was cancelled after file preparation");
                        Game.IsInstalling = false;
                        return;
                    }
                    
                    // Show a notification to the user
                    UpdateProgress("Running installer...", 20);
                    
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Running installer for {Game.Name}. Follow the installation prompts.",
                        NotificationType.Info
                    );
                    
                    // Execute the installer
                    using (var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = tempInstallerPath,
                            WorkingDirectory = string.IsNullOrEmpty(extractedContentDir) ? tempDir : Path.GetDirectoryName(tempInstallerPath),
                            UseShellExecute = true
                        }
                    })
                    {
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
                                        _emuLibrary.Logger.Error($"Failed to kill installation process: {ex.Message}");
                                    }
                                }
                                Thread.Sleep(500);
                            }
                            process.WaitForExit();
                        }, cancellationToken);
                    }
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _emuLibrary.Logger.Info($"Installation of {Game.Name} was cancelled after installer execution");
                        Game.IsInstalling = false;
                        return;
                    }
                    
                    // Ask user to provide the installation directory
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
                                    // For now, just use the first executable since we can't show a proper selection dialog
                                    _emuLibrary.Logger.Info($"Multiple executables found. Automatically selecting the first one.");
                                    var selectedExe = exeOptions.FirstOrDefault();
                                    
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
                        _emuLibrary.Logger.Error($"Error finding executable files: {ex.Message}");
                    }
                    
                    // Clean up temp directory - ensure this always happens
                    UpdateProgress("Cleaning up...", 90);
                    
                    try
                    {
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, true);
                            _emuLibrary.Logger.Debug($"Cleaned up temp directory: {tempDir}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the full exception details
                        _emuLibrary.Logger.Warn($"Failed to clean up temp directory {tempDir}: {ex.Message}");
                    }
                    
                    // Create GameInstallationData
                    UpdateProgress("Finalizing installation...", 95);
                    
                    // Preserve store info in installation data
                    var installationData = new GameInstallationData
                    {
                        InstallDirectory = installDir
                    };
                    
                    // Preserve store-specific information
                    if (!string.IsNullOrEmpty(info.StoreGameId) && !string.IsNullOrEmpty(info.InstallerType))
                    {
                        _emuLibrary.Logger.Info($"Preserving store information for {Game.Name}: {info.InstallerType} ID {info.StoreGameId}");
                    }
                    
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
                    // Installation failed
                    _emuLibrary.Logger.Error($"Failed to install {Game.Name}: {ex.Message}");
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Failed to install {Game.Name}.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                        NotificationType.Error
                    );
                    Game.IsInstalling = false;
                    throw; // Rethrow without wrapping to preserve stack trace
                }
                finally
                {
                    // Ensure temp directory is cleaned up even on exception
                    try
                    {
                        var tempDir = Path.Combine(Path.GetTempPath(), "Playnite_PCInstaller");
                        if (Directory.Exists(tempDir))
                        {
                            // Try to clean up any orphaned temp directories older than 1 hour
                            var dirs = Directory.GetDirectories(tempDir);
                            foreach (var dir in dirs)
                            {
                                try
                                {
                                    var dirInfo = new DirectoryInfo(dir);
                                    if (DateTime.Now - dirInfo.LastWriteTime > TimeSpan.FromHours(1))
                                    {
                                        Directory.Delete(dir, true);
                                        _emuLibrary.Logger.Debug($"Cleaned up orphaned temp directory: {dir}");
                                    }
                                }
                                catch
                                {
                                    // Ignore individual cleanup failures
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore cleanup failures in finally block
                    }
                }
            });
        }
        
        private void UpdateProgress(string status, int progressPercentage)
        {
            _emuLibrary.Logger.Debug($"Install progress for {Game.Name}: {status} ({progressPercentage}%)");
        }
        
        /// <summary>
        /// Handles ISO installation: mounts ISO, finds and runs installer, unmounts, finds executable
        /// </summary>
        private async Task HandleISOInstallation(string isoPath, string installDir, CancellationToken cancellationToken)
        {
            var info = Game.GetPCInstallerGameInfo();
            string mountedDriveLetter = null;
            try
            {
                // Mount the ISO image
                UpdateProgress("Mounting disc image...", 40);
                _emuLibrary.Logger.Info($"Mounting ISO image: {Path.GetFileName(isoPath)}");
                mountedDriveLetter = await MountISOImage(isoPath);
                
                if (string.IsNullOrEmpty(mountedDriveLetter))
                {
                    throw new Exception("Failed to mount ISO image");
                }
                
                if (cancellationToken.IsCancellationRequested)
                {
                    await UnmountISOImage(mountedDriveLetter);
                    throw new OperationCanceledException();
                }
                
                // Find installer in mounted disc
                UpdateProgress("Finding installer...", 50);
                var installerCandidates = new List<string>
                {
                    "setup.exe", "install.exe", "autorun.exe", "start.exe",
                    "SETUP.EXE", "INSTALL.EXE", "AUTORUN.EXE", "START.EXE"
                };
                
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
                    throw new Exception("Could not find an installer in the disc image");
                }
                
                // Run the installer
                UpdateProgress("Running installer...", 60);
                _emuLibrary.Logger.Info($"Running installer: {installerPath}");
                _emuLibrary.Playnite.Notifications.Add(
                    Game.GameId,
                    $"Running installer for {Game.Name}. Please complete the installation process.",
                    NotificationType.Info
                );
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    WorkingDirectory = Path.GetDirectoryName(installerPath)
                };
                
                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        while (!process.HasExited)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                try
                                {
                                    process.Kill();
                                    _emuLibrary.Logger.Info($"Installation process for {Game.Name} was cancelled and terminated");
                                }
                                catch (Exception ex)
                                {
                                    _emuLibrary.Logger.Error($"Failed to kill installation process: {ex.Message}");
                                }
                                throw new OperationCanceledException();
                            }
                            await Task.Delay(500, cancellationToken);
                        }
                        process.WaitForExit();
                    }
                    else
                    {
                        throw new Exception("Failed to start installer process");
                    }
                }
                
                // Unmount ISO
                UpdateProgress("Unmounting disc image...", 90);
                await UnmountISOImage(mountedDriveLetter);
                mountedDriveLetter = null;
                
                // Find primary executable in install directory
                UpdateProgress("Finding game executable...", 95);
                try
                {
                    var exeFiles = Directory.GetFiles(installDir, "*.exe", SearchOption.AllDirectories);
                    if (exeFiles.Length > 0)
                    {
                        var commonNames = new List<string> 
                        { 
                            "game.exe", "launcher.exe", "start.exe", "play.exe", 
                            "bin\\game.exe", "bin\\main.exe" 
                        };
                        
                        string mainExe = null;
                        foreach (var name in commonNames)
                        {
                            var potentialPath = Path.Combine(installDir, name);
                            if (File.Exists(potentialPath))
                            {
                                mainExe = potentialPath;
                                break;
                            }
                        }
                        
                        if (mainExe == null)
                        {
                            var rootExes = Directory.GetFiles(installDir, "*.exe", SearchOption.TopDirectoryOnly);
                            mainExe = rootExes.Length > 0 ? rootExes[0] : exeFiles[0];
                        }
                        
                        if (mainExe != null)
                        {
                            info.PrimaryExecutable = mainExe;
                            _emuLibrary.Logger.Info($"Found primary executable: {mainExe}");
                            
                            // Update game actions
                            using (_emuLibrary.Playnite.Database.BufferedUpdate())
                            {
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
                                    Path = mainExe,
                                    IsPlayAction = true
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _emuLibrary.Logger.Warn($"Error finding primary executable: {ex.Message}");
                }
            }
            finally
            {
                // Ensure ISO is unmounted
                if (!string.IsNullOrEmpty(mountedDriveLetter))
                {
                    try
                    {
                        await UnmountISOImage(mountedDriveLetter);
                    }
                    catch (Exception ex)
                    {
                        _emuLibrary.Logger.Error($"Error unmounting ISO in finally block: {ex.Message}");
                    }
                }
            }
        }
        
        private async Task<string> MountISOImage(string isoPath)
        {
            try
            {
                _emuLibrary.Logger.Info($"Mounting ISO image: {Path.GetFileName(isoPath)}");

                // Escape single quotes for PowerShell (double them)
                var escapedIsoPath = isoPath.Replace("'", "''");

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"$mountResult = Mount-DiskImage -ImagePath '{escapedIsoPath}' -PassThru; $volume = Get-DiskImage -ImagePath '{escapedIsoPath}' | Get-Volume; Write-Output $volume.DriveLetter\"",
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