using EmuLibrary.Util;
using EmuLibrary.Util.AssetImporter;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
#if WINDOWS
using System.Management.Automation;
using System.Management.Automation.Runspaces;
#else
// For Collection<T> used in the dummy implementation
using System.Collections.ObjectModel;

// Dummy class for non-Windows builds
namespace System.Management.Automation
{
    public class PSObject
    {
        private string _value;
        
        public PSObject(string value)
        {
            _value = value;
        }
        
        public override string ToString()
        {
            return _value;
        }
    }
    
    public class PowerShell : IDisposable
    {
        public static PowerShell Create() => new PowerShell();
        public bool HadErrors { get; } = false;
        
        public PowerShell AddScript(string script) => this;
        
        public Collection<PSObject> Invoke()
        {
            return new Collection<PSObject> { new PSObject("C") };
        }
        
        public void Dispose() { }
    }
}
#endif
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace EmuLibrary.RomTypes.ISOInstaller
{
    class ISOInstallerInstallController : BaseInstallController
    {
        private enum InstallState
        {
            NotStarted,
            PreparingFiles,
            MountingISO,
            SelectingInstaller,
            RunningInstaller,
            SelectingInstallDir,
            ConfiguringGame,
            UnmountingISO,
            CleaningUp,
            Completed,
            Failed
        }

        internal ISOInstallerInstallController(Game game, IEmuLibrary emuLibrary) : base(game, emuLibrary)
        { }

        public override void Install(InstallActionArgs args)
        {
            var info = Game.GetISOInstallerGameInfo();
            _watcherToken = new CancellationTokenSource();
            
            // Add timeout of 30 minutes for installation process
            _watcherToken.CancelAfter(TimeSpan.FromMinutes(30));
            
            // Use the local cancellation token
            var cancellationToken = _watcherToken.Token;

            Task.Run(async () =>
            {
                // We only need to track the mount point
                var mountPoint = string.Empty;
                
                try
                {
                    // Initialize with a clean state
                    UpdateProgress("Preparing for installation...", 0);
                    
                    // Verify source file exists
                    if (string.IsNullOrEmpty(info.SourceFullPath))
                    {
                        // If SourceFullPath is null, check the InstallerFullPath directly
                        if (string.IsNullOrEmpty(info.InstallerFullPath))
                        {
                            _emuLibrary.Logger.Error($"ISO file path is not set. Both SourceFullPath and InstallerFullPath are empty.");
                            
                            // Try to find the ISO file by game name in all configured ISO source folders
                            var settings = Settings.Settings.Instance;
                            var isoMappings = settings.Mappings.Where(m => m.RomType == RomType.ISOInstaller).ToList();
                            
                            _emuLibrary.Logger.Info($"Searching for ISO in {isoMappings.Count} configured mapping folders");
                            
                            string gameName = Game.Name.Replace(":", "_").Replace("\\", "_").Replace("/", "_");
                            bool foundIso = false;
                            
                            foreach (var mapping in isoMappings)
                            {
                                if (string.IsNullOrEmpty(mapping.SourcePath) || !Directory.Exists(mapping.SourcePath))
                                    continue;
                                    
                                _emuLibrary.Logger.Info($"Searching for {gameName} in {mapping.SourcePath}");
                                
                                // Search directly for common ISO formats with the game name
                                string[] possibleExtensions = new[] { ".iso", ".bin", ".img", ".cue", ".nrg", ".mds", ".mdf" };
                                foreach (var ext in possibleExtensions)
                                {
                                    // Try exact match
                                    string possiblePath = Path.Combine(mapping.SourcePath, gameName + ext);
                                    _emuLibrary.Logger.Info($"Checking {possiblePath}");
                                    
                                    if (File.Exists(possiblePath))
                                    {
                                        info.SourceFullPath = possiblePath;
                                        info.SourcePath = gameName + ext;
                                        info.MappingId = mapping.MappingId;
                                        _emuLibrary.Logger.Info($"Found exact match ISO: {possiblePath}");
                                        foundIso = true;
                                        break;
                                    }
                                    
                                    // Try case-insensitive search
                                    try {
                                        var files = Directory.GetFiles(mapping.SourcePath, "*" + ext);
                                        var matchingFile = files.FirstOrDefault(f => 
                                            Path.GetFileNameWithoutExtension(f).Equals(gameName, StringComparison.OrdinalIgnoreCase) ||
                                            Path.GetFileNameWithoutExtension(f).Contains(gameName) ||
                                            gameName.Contains(Path.GetFileNameWithoutExtension(f)));
                                        
                                        if (!string.IsNullOrEmpty(matchingFile))
                                        {
                                            info.SourceFullPath = matchingFile;
                                            info.SourcePath = Path.GetFileName(matchingFile);
                                            info.MappingId = mapping.MappingId;
                                            _emuLibrary.Logger.Info($"Found similar ISO: {matchingFile}");
                                            foundIso = true;
                                            break;
                                        }
                                    } catch (Exception ex) {
                                        _emuLibrary.Logger.Error($"Error searching in {mapping.SourcePath}: {ex.Message}");
                                    }
                                }
                                
                                if (foundIso) break;
                            }
                            
                            if (!foundIso)
                            {
                                throw new FileNotFoundException($"ISO file not found for game: {Game.Name}. Please add a source path mapping or select the ISO file manually.");
                            }
                        }
                        else if (!File.Exists(info.InstallerFullPath))
                        {
                            throw new FileNotFoundException($"ISO file not found at InstallerFullPath: {info.InstallerFullPath}");
                        }
                        else
                        {
                            // Use InstallerFullPath directly
                            _emuLibrary.Logger.Info($"Using InstallerFullPath directly: {info.InstallerFullPath}");
                            info.SourceFullPath = info.InstallerFullPath;
                        }
                    }
                    else if (!File.Exists(info.SourceFullPath))
                    {
                        // Log both possible paths for debugging
                        _emuLibrary.Logger.Error($"ISO file not found at SourceFullPath: {info.SourceFullPath}");
                        _emuLibrary.Logger.Error($"InstallerFullPath: {info.InstallerFullPath}");
                        
                        // Try the alternative path if available
                        if (!string.IsNullOrEmpty(info.InstallerFullPath) && File.Exists(info.InstallerFullPath))
                        {
                            _emuLibrary.Logger.Info($"ISO file found at InstallerFullPath. Using that instead: {info.InstallerFullPath}");
                            info.SourceFullPath = info.InstallerFullPath;
                        }
                        else
                        {
                            throw new FileNotFoundException($"ISO file not found: {info.SourceFullPath}");
                        }
                    }
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _emuLibrary.Logger.Info($"Installation of {Game.Name} was cancelled during preparation");
                        Game.IsInstalling = false;
                        return;
                    }
                    
                    // Mount the ISO file directly from its source location
                    UpdateProgress("Mounting ISO file...", 10);
                    
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Mounting ISO file for {Game.Name}...",
                        NotificationType.Info
                    );
                    
                    // Use the source file directly without copying to temp storage
                    string isoSourcePath = info.SourceFullPath;
                    _emuLibrary.Logger.Info($"Mounting ISO directly from source: {isoSourcePath}");
                    
                    mountPoint = MountIsoFile(isoSourcePath);
                    if (string.IsNullOrEmpty(mountPoint))
                    {
                        throw new Exception("Failed to mount ISO file. Make sure the ISO is not corrupted.");
                    }
                    
                    info.MountPoint = mountPoint;
                    _emuLibrary.Logger.Info($"ISO mounted at {mountPoint}");
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _emuLibrary.Logger.Info($"Installation of {Game.Name} was cancelled after mounting ISO");
                        Game.IsInstalling = false;
                        UnmountIsoFile(mountPoint);
                        return;
                    }
                    
                    // Get all executables in the mounted ISO
                    UpdateProgress("Finding executables in ISO...", 20);
                    
                    var exeFiles = Directory.GetFiles(mountPoint, "*.exe", SearchOption.AllDirectories)
                        .OrderBy(f => Path.GetFileName(f))
                        .ToList();
                    
                    if (exeFiles.Count == 0)
                    {
                        throw new FileNotFoundException("No executable files found in the mounted ISO.");
                    }
                    
                    // Ask user to select an executable from the ISO to run
                    UpdateProgress("Select an executable from the ISO...", 30);
                    
                    string selectedExecutable = null;
                    _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                    {
                        // Simple list of common installer executables to suggest
                        var commonInstallerNames = new[] { 
                            "setup.exe", "install.exe", "autorun.exe", "start.exe"
                        };
                        
                        // Find any of these common installers in the root directory
                        var suggestedInstallers = exeFiles
                            .Where(exe => {
                                string fileName = Path.GetFileName(exe).ToLowerInvariant();
                                string dirName = Path.GetDirectoryName(exe);
                                // Prefer installers in the root directory
                                return commonInstallerNames.Contains(fileName) && 
                                      (dirName == mountPoint || dirName.StartsWith(Path.Combine(mountPoint, "setup")));
                            })
                            .ToList();
                        
                        // Create a message to show suggested and other executables
                        string exeOptionsMessage = $"The ISO for \"{Game.Name}\" has been mounted.\n\n";
                        
                        if (suggestedInstallers.Any())
                        {
                            exeOptionsMessage += "SUGGESTED INSTALLERS:\n";
                            foreach (var exe in suggestedInstallers.Take(3))
                            {
                                exeOptionsMessage += $"- {Path.GetFileName(exe)}\n";
                            }
                            exeOptionsMessage += "\n";
                        }
                        
                        // Show a subset of other executables
                        var otherExecutables = exeFiles
                            .Where(exe => !suggestedInstallers.Contains(exe))
                            .Take(7)
                            .ToList();
                            
                        if (otherExecutables.Any())
                        {
                            exeOptionsMessage += "OTHER EXECUTABLES:\n";
                            foreach (var exe in otherExecutables)
                            {
                                exeOptionsMessage += $"- {Path.GetFileName(exe)}\n";
                            }
                            
                            if (exeFiles.Count > suggestedInstallers.Count + otherExecutables.Count)
                            {
                                int remaining = exeFiles.Count - suggestedInstallers.Count - otherExecutables.Count;
                                exeOptionsMessage += $"...and {remaining} more\n";
                            }
                        }
                        
                        // Add instructions
                        exeOptionsMessage += "\nPlease select an installer executable to run. " +
                            "After the installation completes, you'll be asked where the game was installed.";
                        
                        // Show the notification
                        _emuLibrary.Playnite.Notifications.Add(
                            Game.GameId,
                            exeOptionsMessage,
                            NotificationType.Info
                        );
                        
                        // Use Playnite's file selection dialog
                        var selectFileDialog = _emuLibrary.Playnite.Dialogs.SelectFile("Executable files (*.exe)|*.exe");
                        
                        if (!string.IsNullOrEmpty(selectFileDialog))
                        {
                            selectedExecutable = selectFileDialog;
                            _emuLibrary.Logger.Info($"User selected executable: {selectedExecutable}");
                        }
                        else
                        {
                            // If user cancels, give them another chance
                            _emuLibrary.Playnite.Notifications.Add(
                                Game.GameId,
                                $"Please select an executable from the ISO for {Game.Name}, or the installation will be cancelled.",
                                NotificationType.Error
                            );
                            
                            // Try one more time
                            var secondAttempt = _emuLibrary.Playnite.Dialogs.SelectFile("Executable files (*.exe)|*.exe");
                            
                            if (!string.IsNullOrEmpty(secondAttempt))
                            {
                                selectedExecutable = secondAttempt;
                                _emuLibrary.Logger.Info($"User selected executable on second attempt: {selectedExecutable}");
                            }
                            else
                            {
                                throw new OperationCanceledException("No executable was selected from the ISO.");
                            }
                        }
                    });
                    
                    if (string.IsNullOrEmpty(selectedExecutable))
                    {
                        throw new OperationCanceledException("No executable was selected.");
                    }
                    
                    info.SelectedInstaller = selectedExecutable;
                    _emuLibrary.Logger.Info($"Selected executable: {selectedExecutable}");
                    
                    // Show a notification to the user
                    UpdateProgress("Running selected executable...", 40);
                    
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Running {Path.GetFileName(selectedExecutable)} for {Game.Name}. Follow the installation prompts.",
                        NotificationType.Info
                    );
                    
                    // Execute the selected executable
                    Process process = null;
                    try
                    {
                        process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = selectedExecutable,
                                WorkingDirectory = Path.GetDirectoryName(selectedExecutable),
                                UseShellExecute = true
                            }
                        };
                        
                        // Verify file exists before attempting to launch
                        if (!File.Exists(selectedExecutable))
                        {
                            throw new FileNotFoundException($"Selected executable not found: {selectedExecutable}");
                        }
                        
                        process.Start();
                    }
                    catch (Exception ex)
                    {
                        _emuLibrary.Logger.Error($"Failed to start executable: {ex.Message}");
                        throw;
                    }
                    
                    // Wait for process completion without creating nested tasks
                    try
                    {
                        // Use polling with Task.Delay instead of Thread.Sleep for better async behavior
                        while (!process.HasExited)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                try
                                {
                                    if (!process.HasExited)
                                    {
                                        process.Kill();
                                        _emuLibrary.Logger.Info($"Installation process for {Game.Name} was cancelled and terminated");
                                    }
                                    Game.IsInstalling = false;
                                    return;
                                }
                                catch (Exception ex)
                                {
                                    _emuLibrary.Logger.Error($"Failed to kill installation process: {ex.Message}");
                                }
                            }
                            
                            // Use a simple await with Task.Delay for .NET 4.6.2 compatibility
                            await Task.Delay(500, cancellationToken);
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // Handle Task.Delay cancellation (expected behavior)
                        _emuLibrary.Logger.Info($"Process monitoring for {Game.Name} was cancelled");
                        
                        try
                        {
                            if (!process.HasExited)
                            {
                                process.Kill();
                                _emuLibrary.Logger.Info($"Killed process on cancellation");
                            }
                        }
                        catch (Exception ex)
                        {
                            _emuLibrary.Logger.Error($"Failed to kill process on cancellation: {ex.Message}");
                        }
                        
                        throw new OperationCanceledException("Installation cancelled during process execution");
                    }
                    
                    // Check cancellation using a separate variable to avoid race conditions
                    bool wasCancelled = cancellationToken.IsCancellationRequested;
                    if (wasCancelled)
                    {
                        _emuLibrary.Logger.Info($"Installation of {Game.Name} was cancelled after installer execution");
                        Game.IsInstalling = false;
                        UnmountIsoFile(mountPoint);
                        return;
                    }
                    
                    // Ask user to provide the installation directory
                    UpdateProgress("Selecting installation directory...", 70);
                    
                    string installDir = null;
                    try
                    {
                        // Move all UI operations including cancellation check inside UIDispatcher to avoid thread issues
                        _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                        {
                            // Check cancellation inside UI thread to avoid race conditions
                            if (cancellationToken.IsCancellationRequested)
                            {
                                wasCancelled = true;
                                return;
                            }
                                
                            // Create a consistent notification ID for tracking
                            string notificationId = $"ISOInstaller_{Game.GameId}_InstallDir";
                            _emuLibrary.Playnite.Notifications.Add(
                                notificationId,
                                $"Please select where {Game.Name} was installed.",
                                NotificationType.Info
                            );
                            
                            installDir = _emuLibrary.Playnite.Dialogs.SelectFolder();
                            
                            // Clear notification after selection
                            if (!string.IsNullOrEmpty(installDir))
                            {
                                _emuLibrary.Playnite.Notifications.Remove(notificationId);
                            }
                        });
                        
                        // Check if the operation was cancelled while UI was showing
                        if (wasCancelled || cancellationToken.IsCancellationRequested)
                        {
                            throw new OperationCanceledException("Installation was cancelled.");
                        }
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        _emuLibrary.Logger.Error($"Error selecting installation directory: {ex.Message}");
                        throw;
                    }
                    
                    if (string.IsNullOrEmpty(installDir))
                    {
                        _emuLibrary.Playnite.Notifications.Add(
                            Game.GameId,
                            $"Installation of {Game.Name} was cancelled because no installation directory was selected.",
                            NotificationType.Error
                        );
                        Game.IsInstalling = false;
                        UnmountIsoFile(mountPoint);
                        return;
                    }
                    
                    // Verify the selected directory exists
                    if (!Directory.Exists(installDir))
                    {
                        _emuLibrary.Logger.Error($"Selected installation directory does not exist: {installDir}");
                        throw new DirectoryNotFoundException($"Selected installation directory does not exist: {installDir}");
                    }
                    
                    // Update the game info with the installation directory
                    UpdateProgress("Configuring game...", 80);
                    
                    info.InstallDirectory = installDir;
                    
                    // Ask user to select the main game executable
                    string primaryExe = null;
                    bool exeSelectionCancelled = false;
                    
                    try
                    {
                        // Verify the installation directory exists before accessing it
                        if (!Directory.Exists(installDir))
                        {
                            throw new DirectoryNotFoundException($"Installation directory does not exist: {installDir}");
                        }
                        
                        // Find executables in the installation directory for potential play action
                        List<string> installedExeFiles = new List<string>();
                        try
                        {
                            installedExeFiles = Directory.GetFiles(installDir, "*.exe", SearchOption.AllDirectories).ToList();
                        }
                        catch (Exception ex)
                        {
                            _emuLibrary.Logger.Error($"Error searching for executables: {ex.Message}");
                            // Continue with empty list
                        }
                        
                        if (installedExeFiles.Count > 0)
                        {
                            _emuLibrary.Logger.Info($"Found {installedExeFiles.Count} executable files in installation directory");
                            
                            // Filter out common non-game executables
                            var filteredExeFiles = installedExeFiles
                                .Where(path => {
                                    try
                                    {
                                        string fileName = Path.GetFileName(path)?.ToLowerInvariant() ?? "";
                                        string dirName = Path.GetDirectoryName(path)?.ToLowerInvariant() ?? "";
                                        
                                        // Exclude common utility/helper executables
                                        return !fileName.Contains("unins") && 
                                               !fileName.Contains("setup") &&
                                               !fileName.Contains("redist") &&
                                               !fileName.Contains("vcredist") &&
                                               !dirName.Contains("\\redist") &&
                                               !dirName.Contains("\\dotnet") &&
                                               !dirName.Contains("\\directx") &&
                                               !dirName.Contains("\\support");
                                    }
                                    catch
                                    {
                                        // If any errors in filtering, include the file as is
                                        return true;
                                    }
                                })
                                .ToList();
                                
                            _emuLibrary.Logger.Info($"Filtered to {filteredExeFiles.Count} potential game executables");
                            
                            // Ask user to select the main game executable
                            _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                            {
                                // Check cancellation inside UI thread
                                if (cancellationToken.IsCancellationRequested)
                                {
                                    exeSelectionCancelled = true;
                                    return;
                                }
                                
                                // Create a consistent notification ID
                                string notificationId = $"ISOInstaller_{Game.GameId}_ExeSelection";
                                
                                // Prepare notification message with executable options
                                string message = $"Installation for \"{Game.Name}\" is complete!\n\n" +
                                                "Please select the main game executable to launch the game:";
                                
                                // Show at most 10 executables to avoid overwhelming the user
                                var displayExes = filteredExeFiles.Take(10).ToList();
                                foreach (var exe in displayExes)
                                {
                                    message += $"\n- {Path.GetFileName(exe)}";
                                }
                                
                                if (filteredExeFiles.Count > 10)
                                {
                                    message += $"\n- Plus {filteredExeFiles.Count - 10} more...";
                                }
                                
                                _emuLibrary.Playnite.Notifications.Add(
                                    notificationId,
                                    message,
                                    NotificationType.Info
                                );
                                
                                try
                                {
                                    // Use Playnite's file selection dialog
                                    var selectFileDialog = _emuLibrary.Playnite.Dialogs.SelectFile("Executable files (*.exe)|*.exe");
                                    
                                    if (!string.IsNullOrEmpty(selectFileDialog))
                                    {
                                        primaryExe = selectFileDialog;
                                        _emuLibrary.Logger.Info($"User selected main executable: {primaryExe}");
                                        
                                        // Remove the notification once selection is made
                                        _emuLibrary.Playnite.Notifications.Remove(notificationId);
                                    }
                                    else
                                    {
                                        // If user cancels, give them another chance
                                        _emuLibrary.Playnite.Notifications.Add(
                                            notificationId,
                                            $"Please select a main executable for {Game.Name}, or the installation will be incomplete.",
                                            NotificationType.Error
                                        );
                                        
                                        // Check cancellation again
                                        if (cancellationToken.IsCancellationRequested)
                                        {
                                            exeSelectionCancelled = true;
                                            return;
                                        }
                                        
                                        // Try one more time
                                        var secondAttempt = _emuLibrary.Playnite.Dialogs.SelectFile("Executable files (*.exe)|*.exe");
                                        
                                        if (!string.IsNullOrEmpty(secondAttempt))
                                        {
                                            primaryExe = secondAttempt;
                                            _emuLibrary.Logger.Info($"User selected main executable on second attempt: {primaryExe}");
                                            
                                            // Remove the notification after second selection
                                            _emuLibrary.Playnite.Notifications.Remove(notificationId);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _emuLibrary.Logger.Error($"Error in executable selection UI: {ex.Message}");
                                }
                            });
                            
                            // Check if selection process was cancelled
                            if (exeSelectionCancelled || cancellationToken.IsCancellationRequested)
                            {
                                throw new OperationCanceledException("Executable selection cancelled");
                            }
                            
                            // Verify that primaryExe exists if it was selected
                            if (primaryExe != null && !File.Exists(primaryExe))
                            {
                                _emuLibrary.Logger.Warn($"Selected executable does not exist: {primaryExe}");
                                primaryExe = null;
                            }
                            
                            // If user didn't select anything, use a filtered executable as a fallback
                            if (primaryExe == null && filteredExeFiles.Count > 0)
                            {
                                // Verify that fallback exists
                                string fallbackExe = filteredExeFiles.FirstOrDefault(f => File.Exists(f));
                                if (!string.IsNullOrEmpty(fallbackExe))
                                {
                                    primaryExe = fallbackExe;
                                    _emuLibrary.Logger.Warn($"User did not select an executable; using first filtered executable as fallback: {primaryExe}");
                                }
                            }
                            // If filtered list is empty, try any executable
                            else if (primaryExe == null && installedExeFiles.Count > 0)
                            {
                                // Verify that fallback exists
                                string fallbackExe = installedExeFiles.FirstOrDefault(f => File.Exists(f));
                                if (!string.IsNullOrEmpty(fallbackExe))
                                {
                                    primaryExe = fallbackExe;
                                    _emuLibrary.Logger.Warn($"User did not select an executable; using first executable as fallback: {primaryExe}");
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Just propagate cancellation exceptions
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _emuLibrary.Logger.Error($"Error during executable selection: {ex.Message}");
                        // Continue without executable - will be handled later
                    }
                    
                    // Unmount the ISO
                    UpdateProgress("Unmounting ISO...", 85);
                    UnmountIsoFile(mountPoint);
                    
                    // Cleanup phase
                    UpdateProgress("Finalizing...", 90);
                    
                    // No temporary files to clean up since we directly mounted the ISO
                    _emuLibrary.Logger.Info("No temporary files to clean up - ISO was mounted directly from source");
                    
                    // Create GameInstallationData
                    UpdateProgress("Finalizing installation...", 95);
                    
                    // Check if the installation directory exists before proceeding
                    if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
                    {
                        throw new DirectoryNotFoundException($"Installation directory doesn't exist or wasn't selected: {installDir}");
                    }
                    
                    // Preserve store info in installation data
                    var installationData = new GameInstallationData
                    {
                        InstallDirectory = installDir
                    };
                    
                    // Preserve store-specific information if available
                    if (!string.IsNullOrEmpty(info.StoreGameId) && !string.IsNullOrEmpty(info.InstallerType))
                    {
                        _emuLibrary.Logger.Info($"Preserving store information for {Game.Name}: {info.InstallerType} ID {info.StoreGameId}");
                    }
                    
                    // Add ROM and game actions if we have a valid executable
                    if (!string.IsNullOrEmpty(primaryExe) && File.Exists(primaryExe))
                    {
                        // Add the ROM to installation data
                        installationData.Roms = new List<GameRom> 
                        { 
                            new GameRom(Game.Name, primaryExe) 
                        };
                        
                        try
                        {
                            // Use buffered update for game actions to reduce UI events
                            using (_emuLibrary.Playnite.Database.BufferedUpdate())
                            {
                                // Add game actions that will be applied to the game
                                // Don't clear existing actions - preserve custom ones
                                if (Game.GameActions == null)
                                {
                                    Game.GameActions = new ObservableCollection<GameAction>();
                                }
                                else
                                {
                                    // Only remove previous play actions, keeping any custom actions
                                    var playActionsToRemove = Game.GameActions
                                        .Where(a => a.IsPlayAction)
                                        .ToList();
                                        
                                    foreach (var action in playActionsToRemove)
                                    {
                                        Game.GameActions.Remove(action);
                                    }
                                }
                                
                                // Add the new play action
                                Game.GameActions.Add(new GameAction()
                                {
                                    Name = "Play",
                                    Type = GameActionType.File, 
                                    Path = primaryExe,
                                    IsPlayAction = true,
                                    WorkingDir = Path.GetDirectoryName(primaryExe) // Set working directory
                                });
                                
                                _emuLibrary.Logger.Info($"Added play action for {Game.Name}: {primaryExe}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _emuLibrary.Logger.Error($"Error setting up game actions: {ex.Message}");
                            // Continue anyway - the install data is more important
                        }
                    }
                    else
                    {
                        _emuLibrary.Logger.Warn($"No valid executable selected for {Game.Name}. Game will be installed but may not be playable.");
                        
                        // Notify the user
                        _emuLibrary.Playnite.Notifications.Add(
                            $"ISOInstaller_{Game.GameId}_NoExe",
                            $"No valid executable was selected for {Game.Name}. You may need to manually set up a play action in the game's properties.",
                            NotificationType.Error
                        );
                    }
                    
                    // Update progress to 100%
                    UpdateProgress("Installation complete", 100);
                    
                    // Notify completion and update game
                    InvokeOnInstalled(new GameInstalledEventArgs(installationData));
                }
                catch (OperationCanceledException)
                {
                    _emuLibrary.Logger.Info($"Installation of {Game.Name} was cancelled");
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Installation of {Game.Name} was cancelled.",
                        NotificationType.Info
                    );
                    Game.IsInstalling = false;
                    
                    // Clean up if cancelled
                    try
                    {
                        if (!string.IsNullOrEmpty(mountPoint))
                        {
                            _emuLibrary.Logger.Info($"Cleaning up mount point {mountPoint} after cancellation");
                            UnmountIsoFile(mountPoint);
                        }
                    }
                    catch (Exception ex)
                    {
                        _emuLibrary.Logger.Error($"Error during cleanup after cancellation: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    // Installation failed
                    _emuLibrary.Logger.Error($"Failed to install {Game.Name}: {ex.Message}");
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Failed to install {Game.Name}.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                        NotificationType.Error
                    );
                    Game.IsInstalling = false;
                    
                    // Clean up on failure
                    try
                    {
                        if (!string.IsNullOrEmpty(mountPoint))
                        {
                            _emuLibrary.Logger.Info($"Cleaning up mount point {mountPoint} after failure");
                            UnmountIsoFile(mountPoint);
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        _emuLibrary.Logger.Error($"Error during cleanup after failure: {cleanupEx.Message}");
                    }
                    
                    throw; // Rethrow without wrapping to preserve stack trace
                }
            });
        }
        
        private string MountIsoFile(string isoPath)
        {
            try
            {
                _emuLibrary.Logger.Info($"Attempting to mount ISO file: {isoPath}");
                
#if WINDOWS
                // Use PowerShell to mount the ISO
                using (var ps = PowerShell.Create())
                {
                    // Mount the ISO and get the drive letter with proper escaping
                    string escapedPath = isoPath.Replace("'", "''").Replace("\"", "\\\"");
                    ps.AddScript($"$result = Mount-DiskImage -ImagePath \"{escapedPath}\" -PassThru; Get-Volume -DiskImage $result | Select-Object -ExpandProperty DriveLetter");
                    var results = ps.Invoke();
                    
                    if (ps.HadErrors || results.Count == 0)
#else
                // Dummy implementation for non-Windows platforms (only for compilation)
                var results = new Collection<PSObject>();
                if (true)
#endif
                    {
                        _emuLibrary.Logger.Error("Failed to mount ISO: PowerShell command returned no results");
                        return null;
                    }
                    
                    // Get the drive letter
                    var driveLetter = results[0].ToString();
                    if (string.IsNullOrEmpty(driveLetter))
                    {
                        _emuLibrary.Logger.Error("Failed to get drive letter for mounted ISO");
                        return null;
                    }
                    
                    // Return the full path to the mounted ISO
                    return driveLetter + ":\\";
                }
            }
            catch (Exception ex)
            {
                _emuLibrary.Logger.Error($"Error mounting ISO file: {ex.Message}");
                return null;
            }
        }
        
        private bool UnmountIsoFile(string mountPoint)
        {
            try
            {
                if (string.IsNullOrEmpty(mountPoint))
                {
                    _emuLibrary.Logger.Warn("Cannot unmount ISO: Mount point is null or empty");
                    return false;
                }
                
                _emuLibrary.Logger.Info($"Attempting to unmount ISO from {mountPoint}");
                
#if WINDOWS
                // Get the drive letter from the mount point
                var driveLetter = mountPoint.Substring(0, 1);
                
                // Use PowerShell to unmount the ISO
                using (var ps = PowerShell.Create())
                {
                    // Get the disk image by drive letter and dismount it with proper error handling
                    ps.AddScript($"try {{ Get-DiskImage -DevicePath (Get-Volume -DriveLetter {driveLetter} | Get-Partition | Get-Disk | Get-DiskImage | Select-Object -ExpandProperty DevicePath) | Dismount-DiskImage -ErrorAction Stop; $true }} catch {{ Write-Error $_.Exception.Message; $false }}");
                    
                    ps.Invoke();
                    
                    if (ps.HadErrors)
                    {
                        _emuLibrary.Logger.Error("Failed to unmount ISO: PowerShell command had errors");
                        return false;
                    }
                    
                    return true;
                }
#else
                // Dummy implementation for non-Windows platforms
                _emuLibrary.Logger.Warn("Unmounting ISO files is only supported on Windows");
                return false;
#endif
            }
            catch (Exception ex)
            {
                _emuLibrary.Logger.Error($"Error unmounting ISO file: {ex.Message}");
                return false;
            }
        }
        
        private void UpdateProgress(string status, int progressPercentage)
        {
            _emuLibrary.Logger.Debug($"Install progress for {Game.Name}: {status} ({progressPercentage}%)");
        }
    }
}