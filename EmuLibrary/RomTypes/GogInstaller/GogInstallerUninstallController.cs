using System;
using System.Diagnostics;
using System.IO;
using Playnite.SDK;
using EmuLibrary.Util;

namespace EmuLibrary.RomTypes.GogInstaller
{
    public class GogInstallerUninstallController : BaseUninstallController
    {
        public GogInstallerUninstallController(ILogger logger) : base(logger)
        {
        }

        public override bool UninstallRom(ELGameInfo gameInfo, string installDir)
        {
            Logger.Info($"Uninstalling GOG game: {gameInfo.Name}");
            
            try
            {
                if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
                {
                    Logger.Warning($"Install directory not found for {gameInfo.Name}");
                    return false;
                }
                
                // Look for uninstaller
                string uninstallerPath = FindUninstaller(installDir);
                
                if (!string.IsNullOrEmpty(uninstallerPath))
                {
                    // Run the uninstaller
                    Logger.Info($"Running uninstaller: {uninstallerPath}");
                    
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
                    
                    Logger.Info($"Uninstaller completed with exit code: {process.ExitCode}");
                }
                else
                {
                    Logger.Warning($"No uninstaller found, attempting manual deletion");
                    
                    // Try to manually delete the directory
                    try
                    {
                        Directory.Delete(installDir, true);
                        Logger.Info($"Manually deleted installation directory: {installDir}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to delete directory: {ex.Message}");
                        return false;
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error uninstalling game: {ex.Message}");
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
                Logger.Error($"Error finding uninstaller: {ex.Message}");
                return null;
            }
        }
    }
}
