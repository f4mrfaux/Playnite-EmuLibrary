using System;
using System.Diagnostics;
using System.IO;
using Playnite.SDK;
using Playnite.SDK.Models;
using EmuLibrary.Util;

namespace EmuLibrary.RomTypes.GogInstaller
{
    public class GogInstallerInstallController : BaseInstallController
    {
        public GogInstallerInstallController(ILogger logger, FileSystemWatcherFactory fileSystemWatcherFactory) 
            : base(logger, fileSystemWatcherFactory)
        {
        }

        public override bool InstallRom(ELGameInfo gameInfo, string destDir, Game game)
        {
            Logger.Info($"Installing GOG game: {gameInfo.Name} from {gameInfo.Path}");
            
            try
            {
                // Ensure destination directory exists
                Directory.CreateDirectory(destDir);
                
                // Run the installer with silent parameters
                bool installResult = RunInstaller(gameInfo.Path, destDir);
                
                if (!installResult)
                {
                    Logger.Error($"Installation failed for {gameInfo.Name}");
                    return false;
                }
                
                // Find the game executable in the installation directory
                string exePath = FindGameExecutable(destDir);
                
                if (!string.IsNullOrEmpty(exePath))
                {
                    // Update game with executable path
                    game.GameImagePath = exePath;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error installing game: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Runs the GOG installer
        /// </summary>
        private bool RunInstaller(string installerPath, string destinationDir)
        {
            try
            {
                Logger.Info($"Running installer: {installerPath} with destination: {destinationDir}");
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = installerPath,
                        // GOG-specific silent parameters
                        Arguments = $"/VERYSILENT /SP- /SUPPRESSMSGBOXES /DIR=\"{destinationDir}\"",
                        UseShellExecute = true
                    }
                };
                
                process.Start();
                process.WaitForExit();
                
                Logger.Info($"Installer completed with exit code: {process.ExitCode}");
                
                // Check if the destination directory now exists and has files
                if (Directory.Exists(destinationDir) && 
                    Directory.GetFiles(destinationDir, "*.*", SearchOption.AllDirectories).Length > 0)
                {
                    return true;
                }
                
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error running installer: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Finds the game executable in the installation directory
        /// </summary>
        private string FindGameExecutable(string installDir)
        {
            try
            {
                // Look for common executable patterns
                string[] exePatterns = {
                    "game.exe", "launcher.exe", "start.exe", "play.exe"
                };
                
                // Check common subdirectories first
                string[] commonDirs = { "bin", "game", "program", "app" };
                
                foreach (var dir in commonDirs)
                {
                    string subDir = Path.Combine(installDir, dir);
                    if (Directory.Exists(subDir))
                    {
                        foreach (var pattern in exePatterns)
                        {
                            var files = Directory.GetFiles(subDir, pattern);
                            if (files.Length > 0)
                            {
                                return files[0];
                            }
                        }
                    }
                }
                
                // If not found in common dirs, search the entire install directory
                var allExes = Directory.GetFiles(installDir, "*.exe", SearchOption.AllDirectories);
                if (allExes.Length > 0)
                {
                    // Try to find an exe with "game" or "play" in the name
                    foreach (var exe in allExes)
                    {
                        var filename = Path.GetFileName(exe).ToLower();
                        if (filename.Contains("game") || filename.Contains("play") || 
                            filename.Contains("start") || filename.Contains("launch"))
                        {
                            return exe;
                        }
                    }
                    
                    // If still not found, get the largest exe in the whole directory tree
                    return allExes.OrderByDescending(f => new FileInfo(f).Length).First();
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error finding game executable: {ex.Message}");
                return null;
            }
        }
    }
}
