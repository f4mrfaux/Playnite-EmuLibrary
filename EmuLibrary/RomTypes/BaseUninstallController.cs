using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Threading;

namespace EmuLibrary.RomTypes
{
    internal abstract class BaseUninstallController : UninstallController
    {
        protected readonly IEmuLibrary _emuLibrary;
        protected readonly ILogger _logger;
        // This field is used in derived classes
        protected CancellationTokenSource _watcherToken = null;

        internal BaseUninstallController(Game game, IEmuLibrary emuLibrary) : base(game)
        {
            Name = "Uninstall";
            _emuLibrary = emuLibrary;
            _logger = emuLibrary.Logger;
        }

        public override void Dispose()
        {
            _watcherToken?.Cancel();
            base.Dispose();
        }

        /// <summary>
        /// Safely add a notification on the UI thread
        /// </summary>
        protected void SafelyAddNotification(string id, string message, NotificationType type)
        {
            if (_emuLibrary?.Playnite?.MainView?.UIDispatcher != null)
            {
                _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                {
                    _emuLibrary.Playnite.Notifications.Add(id, message, type);
                });
            }
            else
            {
                // If UIDispatcher is not available, log the message
                _logger?.Info($"Notification: {message}");
            }
        }
    }
}