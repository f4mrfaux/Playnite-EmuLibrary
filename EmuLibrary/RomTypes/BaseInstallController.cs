using EmuLibrary.Util.FileCopier;
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
        protected CancellationTokenSource _watcherToken;

        internal BaseInstallController(Game game, IEmuLibrary emuLibrary) : base(game)
        {
            Name = "Install";
            _emuLibrary = emuLibrary;
        }

        public override void Dispose()
        {
            _watcherToken?.Cancel();
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
    }

    // Base class for handling uninstallation operations
    abstract class ELUninstallController : Playnite.SDK.Plugins.UninstallController
    {
        protected readonly IEmuLibrary _emuLibrary;
        protected CancellationTokenSource _watcherToken;

        internal ELUninstallController(Game game, IEmuLibrary emuLibrary) : base(game)
        {
            Name = "Uninstall";
            _emuLibrary = emuLibrary;
        }

        // Install method is already implemented in the base class
        // No need to override it here

        // This must be implemented by derived classes
        public override void Uninstall(UninstallActionArgs args)
        {
            throw new NotImplementedException("This method must be implemented by derived classes");
        }

        public override void Dispose()
        {
            _watcherToken?.Cancel();
            base.Dispose();
        }
    }
}