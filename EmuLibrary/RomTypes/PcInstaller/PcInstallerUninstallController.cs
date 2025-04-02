using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EmuLibrary.RomTypes.PcInstaller
{
    internal class PcInstallerUninstallController : BaseUninstallController
    {
        private readonly new ILogger _logger;
        
        public PcInstallerUninstallController(Game game, IEmuLibrary emuLibrary) : base(game, emuLibrary)
        {
            _logger = emuLibrary.Logger;
        }
        
        public override void Uninstall(UninstallActionArgs args)
        {
            Task.Run(() =>
            {
                try
                {
                    var info = Game.GetELGameInfo() as PcInstallerGameInfo;
                    
                    if (string.IsNullOrEmpty(info.InstallDirectory) || !Directory.Exists(info.InstallDirectory))
                    {
                        _logger.Warning($"Can't uninstall {Game.Name}: Installation directory not found");
                        InvokeOnUninstalled(new GameUninstalledEventArgs());
                        return;
                    }
                    
                    string installDir = info.InstallDirectory;
                    
                    // Look for uninstaller
                    string uninstallerPath = FindUninstaller(installDir);
                    
                    bool uninstallSuccess = false;
                    
                    if (!string.IsNullOrEmpty(uninstallerPath))
                    {
                        _logger.Info($"Running uninstaller: {uninstallerPath}");
                        
                        SafelyAddNotification(
                            Guid.NewGuid().ToString(),
                            $"Uninstalling {Game.Name}... This may take a while.",
                            NotificationType.Info);
                            
                        var process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = uninstallerPath,
                                Arguments = DetermineUninstallerParameters(uninstallerPath),
                                UseShellExecute = true
                            }
                        };
                        
                        process.Start();
                        process.WaitForExit();
                        
                        // Give the uninstaller some time to clean up
                        System.Threading.Thread.Sleep(1000);
                        
                        if (process.ExitCode == 0 && !Directory.Exists(installDir))
                        {
                            uninstallSuccess = true;
                        }
                    }
                    
                    // Fall back to manual deletion if uninstaller failed or wasn't found
                    if (!uninstallSuccess)
                    {
                        _logger.Info($"Attempting manual deletion of {installDir}");
                        
                        try
                        {
                            Directory.Delete(installDir, true);
                            uninstallSuccess = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, $"Failed to delete directory: {installDir}");
                            
                            SafelyAddNotification(
                                Game.GameId,
                                $"Failed to uninstall {Game.Name}. The directory could not be deleted.",
                                NotificationType.Error);
                                
                            Game.IsUninstalling = false;
                            return;
                        }
                    }
                    
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
                        // Sort by path length - usually uninstallers at the root are the main ones
                        return files.OrderBy(f => f.Length).First();
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error finding uninstaller");
                return null;
            }
        }
        
        private string DetermineUninstallerParameters(string uninstallerPath)
        {
            string filename = Path.GetFileName(uninstallerPath).ToLower();
            
            // Check for known uninstaller types based on filename
            if (filename.StartsWith("unins") || filename.Contains("inno"))
            {
                // InnoSetup uninstaller
                return "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART";
            }
            
            if (filename.Contains("nsis"))
            {
                // NSIS uninstaller
                return "/S";
            }
            
            if (filename.Contains("msiexec"))
            {
                // MSI uninstaller
                return "/x /qn";
            }
            
            // Default to InnoSetup silent parameters (most common)
            return "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART"; 
        }
    }
}