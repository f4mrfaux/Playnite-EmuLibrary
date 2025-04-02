using System;
using System.Diagnostics;
using System.IO;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Threading.Tasks;

namespace EmuLibrary.RomTypes.GogInstaller
{
    internal sealed class GogInstallerUninstallController : BaseUninstallController
    {
        private readonly new ILogger _logger;

        internal GogInstallerUninstallController(Game game, IEmuLibrary emuLibrary) : base(game, emuLibrary)
        {
            _logger = emuLibrary.Logger;
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            Task.Run(() =>
            {
                try
                {
                    var info = Game.GetELGameInfo() as GogInstallerGameInfo;
                    string installDir = Game.InstallDirectory;
                    
                    if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
                    {
                        _logger.Warning($"Install directory not found for {Game.Name}");
                        InvokeOnUninstalled(new GameUninstalledEventArgs());
                        return;
                    }
                    
                    UninstallRom(info, installDir);
                    InvokeOnUninstalled(new GameUninstalledEventArgs());
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Error uninstalling game {Game.Name}");
                    SafelyAddNotification(
                        Game.GameId,
                        $"Failed to uninstall {Game.Name}.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                        NotificationType.Error);
                    Game.IsUninstalling = false;
                }
            });
        }
        
        private bool UninstallRom(ELGameInfo gameInfo, string installDir)
        {
            _logger.Info($"Uninstalling GOG game: {gameInfo.Name}");
            
            try
            {
                if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
                {
                    _logger.Warning($"Install directory not found for {gameInfo.Name}");
                    return false;
                }
                
                // Look for uninstaller
                string uninstallerPath = FindUninstaller(installDir);
                
                if (!string.IsNullOrEmpty(uninstallerPath))
                {
                    // Run the uninstaller
                    _logger.Info($"Running uninstaller: {uninstallerPath}");
                    
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = uninstallerPath,
                            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES",
                            UseShellExecute = false
                        }
                    };
                    
                    process.Start();
                    process.WaitForExit();
                    
                    _logger.Info($"Uninstaller completed with exit code: {process.ExitCode}");
                }
                else
                {
                    _logger.Warning($"No uninstaller found, attempting manual deletion");
                    
                    // Try to manually delete the directory
                    try
                    {
                        Directory.Delete(installDir, true);
                        _logger.Info($"Manually deleted installation directory: {installDir}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to delete directory: {ex.Message}");
                        return false;
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error uninstalling game: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Finds the uninstaller in an installation directory
        /// </summary>
        private string FindUninstaller(string installDir)
        {
            try
            {
                // Common uninstaller patterns
                string[] uninstallerPatterns = {
                    "unins*.exe", "uninst*.exe", "*uninstall*.exe",
                    "remove*.exe", "*remove.exe"
                };
                
                foreach (var pattern in uninstallerPatterns)
                {
                    var files = Directory.GetFiles(installDir, pattern, SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        return files[0];
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error finding uninstaller: {ex.Message}");
                return null;
            }
        }
    }
}
