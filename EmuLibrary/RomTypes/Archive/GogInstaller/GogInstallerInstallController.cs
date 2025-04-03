using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace EmuLibrary.RomTypes.GogInstaller
{
    internal sealed class GogInstallerInstallController : BaseInstallController
    {
        private readonly new ILogger _logger;

        internal GogInstallerInstallController(Game game, IEmuLibrary emuLibrary) 
            : base(game, emuLibrary)
        {
            _logger = emuLibrary.Logger;
        }

        public override void Install(InstallActionArgs args)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var gameInfo = Game.GetELGameInfo() as GogInstallerGameInfo;
                    if (gameInfo == null)
                    {
                        _logger.Error($"Failed to get game info for {Game.Name}");
                        InvokeOnInstalled(new GameInstalledEventArgs());
                        return;
                    }

                    // Get installation directory from game or use a default location
                    string destDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        StringExtensions.GetSafePathName(Game.Name));
                    _logger.Info($"Installing GOG game: {gameInfo.Name} from {gameInfo.Path}");

                    // Ensure destination directory exists
                    Directory.CreateDirectory(destDir);

                    // Run the installer with silent parameters
                    bool installResult = RunInstaller(gameInfo.Path, destDir);

                    if (!installResult)
                    {
                        _logger.Error($"Installation failed for {gameInfo.Name}");
                        InvokeOnInstalled(new GameInstalledEventArgs());
                        return;
                    }

                    // Find the game executable in the installation directory
                    string exePath = FindGameExecutable(destDir);

                    if (!string.IsNullOrEmpty(exePath))
                    {
                        // Create installation data with Rom
                        var installData = new GameInstallationData
                        {
                            InstallDirectory = destDir,
                            Roms = new System.Collections.Generic.List<GameRom> { new GameRom(Game.Name, exePath) }
                        };
                        
                        // Update the game
                        InvokeOnInstalled(new GameInstalledEventArgs(installData));
                        return;
                    }

                    InvokeOnInstalled(new GameInstalledEventArgs());
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Error installing game {Game.Name}");
                    SafelyAddNotification(
                        Game.GameId,
                        $"Failed to install {Game.Name}.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                        NotificationType.Error);
                    InvokeOnInstalled(new GameInstalledEventArgs());
                }
            });
        }

        /// <summary>
        /// Runs the GOG installer
        /// </summary>
        private bool RunInstaller(string installerPath, string destinationDir)
        {
            try
            {
                _logger.Info($"Running installer: {installerPath} with destination: {destinationDir}");
                
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
                
                _logger.Info($"Installer completed with exit code: {process.ExitCode}");
                
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
                _logger.Error(ex, $"Error running installer: {ex.Message}");
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
                _logger.Error(ex, $"Error finding game executable: {ex.Message}");
                return null;
            }
        }
    }
}
