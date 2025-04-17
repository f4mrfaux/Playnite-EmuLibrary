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

namespace EmuLibrary.RomTypes.ArchiveInstaller
{
    class ArchiveInstallerUninstallController : UninstallController
    {
        private Game _game;
        private IEmuLibrary _emuLibrary;
        private CancellationTokenSource _watcherToken;

        internal ArchiveInstallerUninstallController(Game game, IEmuLibrary emuLibrary)
        {
            _game = game;
            _emuLibrary = emuLibrary;
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            var info = _game.GetArchiveInstallerGameInfo();
            _watcherToken = new CancellationTokenSource();
            
            // Timeout after 10 minutes for uninstall
            _watcherToken.CancelAfter(TimeSpan.FromMinutes(10));
            
            Task.Run(async () =>
            {
                try
                {
                    _emuLibrary.Logger.Info($"Starting uninstallation of {_game.Name}");
                    
                    // Mark game as uninstalling
                    _game.IsUninstalling = true;
                    _emuLibrary.Playnite.Database.Games.Update(_game);
                    
                    // Get game install directory
                    var installDirectory = info.InstallDirectory;
                    if (string.IsNullOrEmpty(installDirectory) || !Directory.Exists(installDirectory))
                    {
                        _emuLibrary.Logger.Warn($"Game {_game.Name} doesn't have a valid installation directory. Marking as uninstalled.");
                        CompleteUninstall();
                        return;
                    }
                    
                    // First, try to find an uninstaller in the installation directory
                    var uninstallerExecuted = await TryRunUninstaller(installDirectory, _watcherToken.Token);
                    
                    // If no uninstaller found or executed successfully, ask if user wants to delete the directory
                    if (!uninstallerExecuted || Directory.Exists(installDirectory))
                    {
                        bool deleteDir = false;
                        
                        // Ask the user for confirmation
                        _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                        {
                            if (_watcherToken.IsCancellationRequested)
                                return;
                                
                            deleteDir = _emuLibrary.Playnite.Dialogs.ShowMessage(
                                $"Do you want to delete the installation directory for {_game.Name}?\n\n{installDirectory}",
                                "Delete Installation Directory",
                                System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.Yes;
                        });
                        
                        // If user agreed to delete the directory
                        if (deleteDir && Directory.Exists(installDirectory))
                        {
                            try
                            {
                                _emuLibrary.Logger.Info($"Deleting installation directory: {installDirectory}");
                                
                                _emuLibrary.Playnite.Notifications.Add(
                                    _game.GameId,
                                    $"Deleting installation directory for {_game.Name}...",
                                    NotificationType.Info
                                );
                                
                                // Try to delete with retries
                                bool deleted = await DeleteDirectoryWithRetryAsync(installDirectory, 3, _watcherToken.Token);
                                
                                if (!deleted)
                                {
                                    _emuLibrary.Playnite.Notifications.Add(
                                        _game.GameId,
                                        $"Could not delete some files in {_game.Name}'s installation directory. You may need to delete them manually.",
                                        NotificationType.Warning
                                    );
                                }
                            }
                            catch (Exception ex)
                            {
                                _emuLibrary.Logger.Error($"Error deleting installation directory: {ex.Message}");
                                
                                _emuLibrary.Playnite.Notifications.Add(
                                    _game.GameId,
                                    $"Failed to delete installation directory for {_game.Name}: {ex.Message}",
                                    NotificationType.Error
                                );
                            }
                        }
                    }
                    
                    // Complete the uninstallation
                    CompleteUninstall();
                    
                    _emuLibrary.Playnite.Notifications.Add(
                        _game.GameId,
                        $"{_game.Name} has been uninstalled.",
                        NotificationType.Info
                    );
                }
                catch (OperationCanceledException)
                {
                    _emuLibrary.Logger.Info($"Uninstallation of {_game.Name} was cancelled");
                    
                    _emuLibrary.Playnite.Notifications.Add(
                        _game.GameId,
                        $"Uninstallation of {_game.Name} was cancelled.",
                        NotificationType.Info
                    );
                    
                    _game.IsUninstalling = false;
                    _emuLibrary.Playnite.Database.Games.Update(_game);
                }
                catch (Exception ex)
                {
                    _emuLibrary.Logger.Error($"Failed to uninstall {_game.Name}: {ex.Message}");
                    
                    _emuLibrary.Playnite.Notifications.Add(
                        _game.GameId,
                        $"Failed to uninstall {_game.Name}: {ex.Message}",
                        NotificationType.Error
                    );
                    
                    _game.IsUninstalling = false;
                    _emuLibrary.Playnite.Database.Games.Update(_game);
                }
            });
        }

        private async Task<bool> TryRunUninstaller(string installDir, CancellationToken cancellationToken)
        {
            try
            {
                _emuLibrary.Logger.Info($"Searching for uninstaller in {installDir}");
                
                // Common uninstaller patterns
                var uninstallerPatterns = new[]
                {
                    "unins*.exe",
                    "uninst*.exe",
                    "uninstall*.exe",
                    "setup.exe", // Some setups have uninstall capability
                    "Remove*.exe"
                };
                
                // Find uninstaller executables
                var uninstallers = new List<string>();
                foreach (var pattern in uninstallerPatterns)
                {
                    uninstallers.AddRange(Directory.GetFiles(installDir, pattern, SearchOption.AllDirectories));
                }
                
                if (uninstallers.Count == 0)
                {
                    _emuLibrary.Logger.Info("No uninstaller found");
                    return false;
                }
                
                string selectedUninstaller = uninstallers.FirstOrDefault(u =>
                    Path.GetFileName(u).ToLower().StartsWith("unins") || 
                    Path.GetFileName(u).ToLower().StartsWith("uninst"));
                
                // If no preferred uninstaller found, use the first one
                if (string.IsNullOrEmpty(selectedUninstaller))
                {
                    selectedUninstaller = uninstallers[0];
                }
                
                _emuLibrary.Logger.Info($"Running uninstaller: {selectedUninstaller}");
                
                _emuLibrary.Playnite.Notifications.Add(
                    _game.GameId,
                    $"Running uninstaller for {_game.Name}. Please follow the uninstallation prompts.",
                    NotificationType.Info
                );
                
                // Run the uninstaller
                using (var process = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = selectedUninstaller,
                        WorkingDirectory = Path.GetDirectoryName(selectedUninstaller),
                        UseShellExecute = true
                    }
                })
                {
                    process.Start();
                    
                    // Wait for uninstaller to complete
                    await Task.Run(() =>
                    {
                        while (!process.HasExited)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                try
                                {
                                    process.Kill();
                                    _emuLibrary.Logger.Info("Uninstallation process was cancelled and terminated");
                                    throw new OperationCanceledException();
                                }
                                catch (Exception ex) when (!(ex is OperationCanceledException))
                                {
                                    _emuLibrary.Logger.Error($"Failed to kill uninstallation process: {ex.Message}");
                                }
                            }
                            Thread.Sleep(500);
                        }
                    }, cancellationToken);
                    
                    _emuLibrary.Logger.Info($"Uninstaller exited with code: {process.ExitCode}");
                    
                    // Wait a bit for files to be released
                    await Task.Delay(1000, cancellationToken);
                    
                    return true;
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _emuLibrary.Logger.Error($"Error running uninstaller: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> DeleteDirectoryWithRetryAsync(string path, int maxRetries, CancellationToken cancellationToken)
        {
            int retryCount = 0;
            bool success = false;
            
            while (retryCount <= maxRetries && !success)
            {
                if (retryCount > 0)
                {
                    _emuLibrary.Logger.Info($"Retry {retryCount}/{maxRetries} to delete directory: {path}");
                    await Task.Delay(500 * retryCount, cancellationToken);
                }
                
                try
                {
                    // First try to force unlock any files with garbage collection
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    
                    try
                    {
                        // Try direct deletion first (faster)
                        Directory.Delete(path, true);
                        success = true;
                    }
                    catch (Exception)
                    {
                        // If direct deletion fails, try to delete files one by one
                        foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                            File.Delete(file);
                        }
                        
                        // Delete directories bottom-up
                        foreach (string dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories)
                            .OrderByDescending(d => d.Length))
                        {
                            if (Directory.Exists(dir))
                            {
                                Directory.Delete(dir, false);
                            }
                        }
                        
                        // Delete the root directory
                        if (Directory.Exists(path))
                        {
                            Directory.Delete(path, false);
                        }
                        
                        success = !Directory.Exists(path);
                    }
                }
                catch (Exception ex)
                {
                    _emuLibrary.Logger.Error($"Error during directory deletion (attempt {retryCount + 1}): {ex.Message}");
                    success = false;
                }
                
                retryCount++;
            }
            
            return success;
        }

        private void CompleteUninstall()
        {
            // Clear installation information
            var gameInfo = _game.GetArchiveInstallerGameInfo();
            gameInfo.InstallDirectory = null;
            gameInfo.PrimaryExecutable = null;
            gameInfo.ExtractedISOPath = null;
            gameInfo.ExtractedArchiveDir = null;
            gameInfo.ImportedArchivePath = null;
            gameInfo.MountPoint = null;
            gameInfo.SelectedInstaller = null;
            
            // Clear game actions
            if (_game.GameActions != null)
            {
                _game.GameActions.Clear();
            }
            
            // Mark as uninstalled
            _game.IsInstalled = false;
            _game.IsUninstalling = false;
            
            // Update the game
            _emuLibrary.Playnite.Database.Games.Update(_game);
            
            // Call event handler
            OnUninstalled(new GameUninstalledEventArgs());
        }
    }
}