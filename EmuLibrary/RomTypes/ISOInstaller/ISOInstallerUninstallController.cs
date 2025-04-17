using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EmuLibrary.RomTypes.ISOInstaller
{
    class ISOInstallerUninstallController : UninstallController
    {
        internal ISOInstallerUninstallController(Game game, IEmuLibrary emuLibrary) : base(game, emuLibrary)
        { }
        
        public override void Uninstall(UninstallActionArgs args)
        {
            _watcherToken = new CancellationTokenSource();
            var info = Game.GetISOInstallerGameInfo();

            // Use the local cancellation token
            var cancellationToken = _watcherToken.Token;

            Task.Run(() =>
            {
                try
                {
                    // Notify the user about the uninstallation
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Uninstalling {Game.Name}...",
                        NotificationType.Info
                    );
                    
                    // Get the actual installation directory
                    string installDir = info.InstallDirectory;
                    
                    // Check if the installation directory exists
                    if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
                    {
                        _emuLibrary.Logger.Error($"Installation directory does not exist: {installDir}");
                        _emuLibrary.Playnite.Notifications.Add(
                            Game.GameId,
                            $"Installation directory for {Game.Name} does not exist.",
                            NotificationType.Error
                        );
                        
                        // Complete the uninstallation anyway
                        InvokeOnUninstalled(null);
                        return;
                    }
                    
                    // Ask user whether to delete installation files or just unregister the game
                    bool deleteFiles = false;
                    _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                    {
                        var message = $"Do you want to delete the installation files for {Game.Name}?";
                        var title = "Delete Files?";
                        if (_emuLibrary.Playnite.Dialogs.ShowMessage(message, title, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            deleteFiles = true;
                        }
                    });
                    
                    if (deleteFiles)
                    {
                        try
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                _emuLibrary.Logger.Info($"Uninstallation of {Game.Name} was cancelled");
                                Game.IsUninstalling = false;
                                return;
                            }
                            
                            // Try to delete the installation directory
                            _emuLibrary.Logger.Info($"Deleting installation directory: {installDir}");
                            Directory.Delete(installDir, true);
                            
                            _emuLibrary.Playnite.Notifications.Add(
                                Game.GameId,
                                $"Successfully deleted installation files for {Game.Name}.",
                                NotificationType.Info
                            );
                        }
                        catch (Exception ex)
                        {
                            _emuLibrary.Logger.Error($"Failed to delete installation directory {installDir}: {ex.Message}");
                            _emuLibrary.Playnite.Notifications.Add(
                                Game.GameId,
                                $"Failed to delete installation files for {Game.Name}: {ex.Message}",
                                NotificationType.Error
                            );
                        }
                    }
                    
                    // Complete the uninstallation
                    InvokeOnUninstalled(null);
                }
                catch (Exception ex)
                {
                    // Uninstallation failed
                    _emuLibrary.Logger.Error($"Failed to uninstall {Game.Name}: {ex.Message}");
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Failed to uninstall {Game.Name}. {ex.Message}",
                        NotificationType.Error
                    );
                    Game.IsUninstalling = false;
                    throw; // Rethrow the exception to be handled by Playnite
                }
            });
        }
    }
}