﻿using EmuLibrary.Util.FileCopier;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.IO;
using System.Threading;

namespace EmuLibrary.RomTypes
{
    abstract class BaseInstallController : InstallController
    {
        protected readonly IEmuLibrary _emuLibrary;
        protected readonly ILogger _logger;
        protected CancellationTokenSource _watcherToken;

        internal BaseInstallController(Game game, IEmuLibrary emuLibrary) : base(game)
        {
            Name = "Install";
            _emuLibrary = emuLibrary;
            _logger = emuLibrary.Logger;
        }

        public override void Dispose()
        {
            try
            {
                if (_watcherToken != null)
                {
                    _watcherToken.Cancel();
                    _watcherToken.Dispose();
                    _watcherToken = null;
                }
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Error disposing cancellation token");
            }
            base.Dispose();
        }

        protected bool UseWindowsCopyDialog()
        {
            if (_emuLibrary.Playnite.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                return _emuLibrary.Settings.UseWindowsCopyDialogInDesktopMode;
            }
            else if (_emuLibrary.Playnite.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                return _emuLibrary.Settings.UseWindowsCopyDialogInFullscreenMode;
            }
            return false;
        }

        protected IFileCopier CreateFileCopier(FileSystemInfo source, DirectoryInfo destination)
        {
            if (UseWindowsCopyDialog())
            {
                return new WindowsFileCopier(source, destination);
            }
            return new SimpleFileCopier(source, destination);
        }
        
        /// <summary>
        /// Safely add a notification on the UI thread
        /// </summary>
        protected void SafelyAddNotification(string id, string message, NotificationType type)
        {
            try
            {
                if (_emuLibrary?.Playnite?.MainView?.UIDispatcher != null)
                {
                    _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                    {
                        try
                        {
                            _emuLibrary.Playnite.Notifications.Add(id, message, type);
                        }
                        catch (Exception ex)
                        {
                            _logger?.Error(ex, $"Error adding notification: {message}");
                        }
                    });
                }
                else
                {
                    // If UIDispatcher is not available, log the message
                    _logger?.Info($"Notification: {message}");
                }
            }
            catch (Exception ex)
            {
                // Last resort fallback if UI dispatcher throws an exception
                _logger?.Error(ex, $"Error invoking UI dispatcher for notification: {message}");
            }
        }
    }
}
