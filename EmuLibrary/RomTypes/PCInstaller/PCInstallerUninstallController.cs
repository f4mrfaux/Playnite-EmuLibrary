using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.IO;

namespace EmuLibrary.RomTypes.PCInstaller
{
    class PCInstallerUninstallController : ELUninstallController
    {
        internal PCInstallerUninstallController(Game game, IEmuLibrary emuLibrary) : base(game, emuLibrary)
        {
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            var info = Game.GetPCInstallerGameInfo();
            
            if (string.IsNullOrEmpty(info.InstallDirectory) || !Directory.Exists(info.InstallDirectory))
            {
                // Just mark as uninstalled if directory doesn't exist
                InvokeOnUninstalled(new GameUninstalledEventArgs());
                return;
            }

            // Check for common uninstaller executables in the installation directory
            var uninstallerPaths = new string[]
            {
                Path.Combine(info.InstallDirectory, "uninstall.exe"),
                Path.Combine(info.InstallDirectory, "unins000.exe"),
                Path.Combine(info.InstallDirectory, "uninst.exe")
            };

            var uninstallerPath = Array.Find(uninstallerPaths, File.Exists);

            if (uninstallerPath != null)
            {
                // If an uninstaller is found, ask if the user wants to run it
                var result = _emuLibrary.Playnite.Dialogs.ShowMessage(
                    $"Found uninstaller for {Game.Name}. Do you want to run it?",
                    "Run Uninstaller",
                    System.Windows.MessageBoxButton.YesNo
                );

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        var process = new System.Diagnostics.Process
                        {
                            StartInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = uninstallerPath,
                                UseShellExecute = true
                            }
                        };
                        process.Start();
                        process.WaitForExit();
                    }
                    catch (Exception ex)
                    {
                        _emuLibrary.Playnite.Notifications.Add(
                            Game.GameId,
                            $"Failed to run uninstaller: {ex.Message}",
                            NotificationType.Error
                        );
                    }
                }
            }

            // Ask if the user wants to remove the installation directory manually
            var shouldRemove = _emuLibrary.Playnite.Dialogs.ShowMessage(
                $"Do you want to remove the installation directory for {Game.Name}?\n\n{info.InstallDirectory}",
                "Remove Installation Directory",
                System.Windows.MessageBoxButton.YesNo
            );

            if (shouldRemove == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    Directory.Delete(info.InstallDirectory, true);
                }
                catch (Exception ex)
                {
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Failed to remove installation directory: {ex.Message}",
                        NotificationType.Error
                    );
                }
            }

            // Mark the game as uninstalled
            InvokeOnUninstalled(new GameUninstalledEventArgs());
        }
    }
}