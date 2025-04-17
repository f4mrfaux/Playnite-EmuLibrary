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
#if WINDOWS
using System.Management.Automation;
#endif
// We can keep using System.Collections.ObjectModel in all cases
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace EmuLibrary.RomTypes.ArchiveInstaller
{
    class ArchiveInstallerInstallController : BaseInstallController
    {
        private enum InstallState
        {
            NotStarted,
            PreparingFiles,
            ImportingArchive,
            ExtractingArchive,
            FindingISO,
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

        internal ArchiveInstallerInstallController(Game game, IEmuLibrary emuLibrary) : base(game, emuLibrary)
        { }

        public override void Install(InstallActionArgs args)
        {
            var info = Game.GetArchiveInstallerGameInfo();
            _watcherToken = new CancellationTokenSource();
            
            // Add timeout of 60 minutes for installation process (archives take longer)
            _watcherToken.CancelAfter(TimeSpan.FromMinutes(60));
            
            // Use the local cancellation token
            var cancellationToken = _watcherToken.Token;

            Task.Run(async () =>
            {
                // Track installation state and paths
                var tempDir = string.Empty;
                var mountPoint = string.Empty;
                var extractDir = string.Empty;
                var localArchivePath = string.Empty;
                var extractedISOPath = string.Empty;
                
                try
                {
                    // Create a temporary directory for operations
                    UpdateProgress("Creating temporary directory...", 0);
                    
                    tempDir = Path.Combine(Path.GetTempPath(), "Playnite_ArchiveInstaller", Guid.NewGuid().ToString());
                    Directory.CreateDirectory(tempDir);
                    
                    // Create extraction directory within temp dir
                    extractDir = Path.Combine(tempDir, "extracted");
                    Directory.CreateDirectory(extractDir);
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _emuLibrary.Logger.Info($"Installation of {Game.Name} was cancelled during preparation");
                        Game.IsInstalling = false;
                        return;
                    }
                    
                    // Verify source file exists
                    var sourceFullPath = info.SourceFullPath;
                    if (string.IsNullOrEmpty(info.MainArchivePath) || !File.Exists(sourceFullPath))
                    {
                        throw new FileNotFoundException($"Archive file not found: {sourceFullPath}");
                    }
                    
                    // Step 1: Import the archive file to local temp storage first
                    UpdateProgress("Importing archive file to local storage...", 5);
                    
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Importing archive file for {Game.Name} to local storage...",
                        NotificationType.Info
                    );
                    
                    // Get or create the AssetImporter
                    var assetImporter = AssetImporter.AssetImporter.Instance ?? 
                        new AssetImporter.AssetImporter(_emuLibrary.Logger, _emuLibrary.Playnite);
                    
                    // Register for progress updates
                    assetImporter.ImportProgress += (sender, e) => {
                        // Calculate progress percentage from 5% to 15% during import
                        int progressValue = 5 + (int)(e.Progress * 10);
                        UpdateProgress($"Importing archive file: {e.BytesTransferred / (1024 * 1024)} MB / {e.TotalBytes / (1024 * 1024)} MB", progressValue);
                    };
                    
                    // For multi-part archives, we need to import the parent directory 
                    // to handle all parts together
                    var sourceDir = Path.GetDirectoryName(sourceFullPath);
                    var isMultiPartArchive = info.ArchiveParts != null && info.ArchiveParts.Count > 1;
                    string importSource;
                    
                    if (isMultiPartArchive)
                    {
                        _emuLibrary.Logger.Info($"Multi-part archive detected with {info.ArchiveParts.Count} parts. Importing parent folder.");
                        importSource = sourceDir;
                    }
                    else
                    {
                        importSource = sourceFullPath;
                    }
                    
                    // Use app mode to determine dialog visibility
                    bool showDialog = _emuLibrary.Playnite.ApplicationInfo.Mode == ApplicationMode.Desktop ?
                        Settings.Settings.Instance.UseWindowsCopyDialogInDesktopMode :
                        Settings.Settings.Instance.UseWindowsCopyDialogInFullscreenMode;
                    
                    // Import the asset (file or directory)
                    var importResult = await assetImporter.ImportAsync(
                        importSource, 
                        showDialog, 
                        cancellationToken);
                    
                    if (!importResult.Success)
                    {
                        if (importResult.Error != null)
                        {
                            throw new Exception($"Failed to import archive: {importResult.Error.Message}", importResult.Error);
                        }
                        else
                        {
                            throw new FileNotFoundException($"Failed to import archive to local storage: {importSource}");
                        }
                    }
                    
                    // Set the path to our local copy
                    if (isMultiPartArchive)
                    {
                        var importedDir = importResult.Path;
                        
                        // Find the main part in the imported directory
                        var mainPartName = Path.GetFileName(sourceFullPath);
                        localArchivePath = Path.Combine(importedDir, mainPartName);
                        
                        if (!File.Exists(localArchivePath))
                        {
                            _emuLibrary.Logger.Warn($"Main archive part not found in imported directory. Searching for it...");
                            
                            // Try to find any file that matches the main archive pattern
                            var files = Directory.GetFiles(importedDir, "*.rar").Union(
                                     Directory.GetFiles(importedDir, "*.zip")).Union(
                                     Directory.GetFiles(importedDir, "*.7z")).ToList();
                                     
                            if (files.Count > 0)
                            {
                                localArchivePath = files[0];
                                _emuLibrary.Logger.Info($"Using {Path.GetFileName(localArchivePath)} as main archive part");
                            }
                            else
                            {
                                throw new FileNotFoundException("No archive files found in the imported directory.");
                            }
                        }
                    }
                    else
                    {
                        localArchivePath = importResult.Path;
                    }
                    
                    info.ImportedArchivePath = localArchivePath;
                    
                    if (importResult.FromCache)
                    {
                        _emuLibrary.Logger.Info($"Using cached archive: {localArchivePath}");
                    }
                    else
                    {
                        _emuLibrary.Logger.Info($"Archive imported successfully to {localArchivePath}");
                    }
                    
                    // Step 2: Extract the archive using 7-Zip
                    UpdateProgress("Extracting archive...", 15);
                    
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Extracting archive for {Game.Name}...",
                        NotificationType.Info
                    );
                    
                    bool extractSuccess = await ExtractArchiveAsync(localArchivePath, extractDir, info.ArchivePassword, cancellationToken);
                    if (!extractSuccess)
                    {
                        throw new Exception("Failed to extract archive. The archive may be corrupt or unsupported.");
                    }
                    
                    info.ExtractedArchiveDir = extractDir;
                    _emuLibrary.Logger.Info($"Archive extracted to {extractDir}");
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _emuLibrary.Logger.Info($"Installation of {Game.Name} was cancelled after extraction");
                        Game.IsInstalling = false;
                        return;
                    }
                    
                    // Step 3: Find ISO files in the extracted directory
                    UpdateProgress("Finding ISO file in extracted archive...", 30);
                    
                    var isoFiles = FindISOFiles(extractDir);
                    if (isoFiles.Count == 0)
                    {
                        throw new FileNotFoundException("No ISO files found in the extracted archive.");
                    }
                    
                    // If multiple ISOs, ask user to select one
                    extractedISOPath = isoFiles.Count > 1 ?
                        await SelectISOFileAsync(isoFiles, cancellationToken) :
                        isoFiles[0];
                    
                    if (string.IsNullOrEmpty(extractedISOPath))
                    {
                        throw new OperationCanceledException("No ISO file was selected from the archive.");
                    }
                    
                    info.ExtractedISOPath = extractedISOPath;
                    _emuLibrary.Logger.Info($"Selected ISO: {extractedISOPath}");
                    
                    // Step 4: Mount the ISO file using PowerShell
                    UpdateProgress("Mounting ISO file...", 40);
                    
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Mounting ISO file for {Game.Name}...",
                        NotificationType.Info
                    );
                    
                    mountPoint = MountIsoFile(extractedISOPath);
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
                    
                    // Step 5: Get all executables in the mounted ISO
                    UpdateProgress("Finding installers in ISO...", 45);
                    
                    var exeFiles = Directory.GetFiles(mountPoint, "*.exe", SearchOption.AllDirectories)
                        .OrderBy(f => Path.GetFileName(f))
                        .ToList();
                    
                    if (exeFiles.Count == 0)
                    {
                        throw new FileNotFoundException("No executable files found in the mounted ISO.");
                    }
                    
                    // Step 6: Ask user to select installer from the ISO
                    UpdateProgress("Selecting installer from ISO...", 50);
                    
                    string selectedInstaller = null;
                    _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                    {
                        // Try to find common installer names first
                        var commonInstallerNames = new[] { 
                            "setup.exe", "install.exe", "autorun.exe", "start.exe", 
                            Path.GetFileNameWithoutExtension(extractedISOPath) + ".exe" 
                        };
                        
                        foreach (var commonName in commonInstallerNames)
                        {
                            var matchingExe = exeFiles.FirstOrDefault(exe => 
                                Path.GetFileName(exe).Equals(commonName, StringComparison.OrdinalIgnoreCase));
                            
                            if (matchingExe != null)
                            {
                                selectedInstaller = matchingExe;
                                break;
                            }
                        }
                        
                        // If no common installer found or multiple options, let user select
                        if (selectedInstaller == null)
                        {
                            _emuLibrary.Logger.Info("Multiple executables found in ISO. User will select one.");
                            
                            if (exeFiles.Count > 0)
                            {
                                // First, create a friendly message with the top executable options
                                var topExeOptions = exeFiles.Take(5)
                                    .Select(p => Path.GetFileName(p))
                                    .ToList();
                                
                                string exeOptionsMessage = "Found these executables in the ISO:";
                                foreach (var exe in topExeOptions)
                                {
                                    exeOptionsMessage += $"\n- {exe}";
                                }
                                
                                if (exeFiles.Count > 5)
                                {
                                    exeOptionsMessage += $"\n- Plus {exeFiles.Count - 5} more...";
                                }
                                
                                _emuLibrary.Playnite.Notifications.Add(
                                    Game.GameId,
                                    $"{exeOptionsMessage}\n\nPlease select an installer for {Game.Name}.",
                                    NotificationType.Info
                                );
                                
                                // Use Playnite's file selection dialog
                                var selectFileDialog = _emuLibrary.Playnite.Dialogs.SelectFile(
                                    "Executable files (*.exe)|*.exe", 
                                    Path.GetDirectoryName(exeFiles[0])
                                );
                                
                                if (!string.IsNullOrEmpty(selectFileDialog))
                                {
                                    selectedInstaller = selectFileDialog;
                                    _emuLibrary.Logger.Info($"User selected installer: {selectedInstaller}");
                                }
                                else
                                {
                                    // If user cancels, give them another chance
                                    _emuLibrary.Playnite.Notifications.Add(
                                        Game.GameId,
                                        $"Please select an installer from the ISO for {Game.Name}, or the installation will be cancelled.",
                                        NotificationType.Warning
                                    );
                                    
                                    // Try one more time with a more direct prompt
                                    var secondAttempt = _emuLibrary.Playnite.Dialogs.SelectFile(
                                        "Executable files (*.exe)|*.exe", 
                                        Path.GetDirectoryName(exeFiles[0])
                                    );
                                    
                                    if (!string.IsNullOrEmpty(secondAttempt))
                                    {
                                        selectedInstaller = secondAttempt;
                                        _emuLibrary.Logger.Info($"User selected installer on second attempt: {selectedInstaller}");
                                    }
                                    else
                                    {
                                        throw new OperationCanceledException("No installer was selected from the ISO.");
                                    }
                                }
                            }
                        }
                    });
                    
                    if (string.IsNullOrEmpty(selectedInstaller))
                    {
                        throw new OperationCanceledException("No installer was selected.");
                    }
                    
                    info.SelectedInstaller = selectedInstaller;
                    _emuLibrary.Logger.Info($"Selected installer: {selectedInstaller}");
                    
                    // Step 7: Run the installer
                    UpdateProgress("Running installer...", 55);
                    
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Running installer for {Game.Name}. Follow the installation prompts.",
                        NotificationType.Info
                    );
                    
                    // Execute the installer
                    Process process = null;
                    try
                    {
                        process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = selectedInstaller,
                                WorkingDirectory = Path.GetDirectoryName(selectedInstaller),
                                UseShellExecute = true
                            }
                        };
                        
                        // Verify installer file exists before attempting to launch
                        if (!File.Exists(selectedInstaller))
                        {
                            throw new FileNotFoundException($"Selected installer not found: {selectedInstaller}");
                        }
                        
                        process.Start();
                    }
                    catch (Exception ex)
                    {
                        _emuLibrary.Logger.Error($"Failed to start installer: {ex.Message}");
                        throw;
                    }
                    
                    // Wait for installer to complete
                    await Task.Run(() => 
                    {
                        while (!process.HasExited)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                try
                                {
                                    process.Kill();
                                    _emuLibrary.Logger.Info($"Installation process for {Game.Name} was cancelled and terminated");
                                    Game.IsInstalling = false;
                                    return;
                                }
                                catch (Exception ex)
                                {
                                    _emuLibrary.Logger.Error($"Failed to kill installation process: {ex.Message}");
                                }
                            }
                            Thread.Sleep(500);
                        }
                    }, cancellationToken);
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _emuLibrary.Logger.Info($"Installation of {Game.Name} was cancelled after installer execution");
                        Game.IsInstalling = false;
                        UnmountIsoFile(mountPoint);
                        return;
                    }
                    
                    // Step 8: Ask user to provide the installation directory
                    UpdateProgress("Selecting installation directory...", 75);
                    
                    string installDir = null;
                    try
                    {
                        _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                        {
                            // Check if cancelled before showing dialog
                            if (cancellationToken.IsCancellationRequested)
                                return;
                                
                            installDir = _emuLibrary.Playnite.Dialogs.SelectFolder();
                        });
                        
                        // Check cancellation after UI operation
                        if (cancellationToken.IsCancellationRequested)
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
                    
                    // Step 9: Configure the game
                    UpdateProgress("Configuring game...", 85);
                    
                    info.InstallDirectory = installDir;
                    
                    // Find executable files in the installation directory for potential play action
                    string primaryExe = null;
                    try
                    {
                        var installedExeFiles = Directory.GetFiles(installDir, "*.exe", SearchOption.AllDirectories);
                        
                        if (installedExeFiles.Length > 0)
                        {
                            // Try to find a primary exe (launcher.exe, game.exe, etc.)
                            var commonMainExeNames = new[] { "launcher.exe", "game.exe", Game.Name.ToLower() + ".exe" };
                            
                            foreach (var commonName in commonMainExeNames)
                            {
                                var matchingExe = installedExeFiles.FirstOrDefault(exe => 
                                    Path.GetFileName(exe).Equals(commonName, StringComparison.OrdinalIgnoreCase));
                                
                                if (matchingExe != null)
                                {
                                    primaryExe = matchingExe;
                                    break;
                                }
                            }
                            
                            // If no primary exe found, use first executable
                            if (primaryExe == null)
                            {
                                // For now, use first executable as a placeholder
                                _emuLibrary.Logger.Info($"No matching primary executable found. Using first one found.");
                                primaryExe = installedExeFiles[0];
                            }
                            
                            // Store the primary executable path in the game info
                            if (!string.IsNullOrEmpty(primaryExe))
                            {
                                info.PrimaryExecutable = primaryExe;
                                _emuLibrary.Logger.Info($"Selected primary executable for {Game.Name}: {primaryExe}");
                            }
                        }
                        else
                        {
                            _emuLibrary.Logger.Warn($"No executable files found in installation directory: {installDir}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _emuLibrary.Logger.Error($"Error finding executable files: {ex.Message}");
                    }
                    
                    // Step 10: Unmount the ISO
                    UpdateProgress("Unmounting ISO...", 90);
                    UnmountIsoFile(mountPoint);
                    
                    // Step 11: Clean up temp directory
                    UpdateProgress("Cleaning up...", 95);
                    
                    try
                    {
                        // Clean up temp directories
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, true);
                        }
                        
                        // Clean up the imported archive file if not cached
                        if (!Settings.Settings.Instance.EnableAssetCaching)
                        {
                            var assetImporterForCleanup = AssetImporter.AssetImporter.Instance ?? 
                                new AssetImporter.AssetImporter(_emuLibrary.Logger, _emuLibrary.Playnite);
                                
                            // If we imported a directory (multi-part), clean up the directory
                            if (isMultiPartArchive && importResult.Path != localArchivePath)
                            {
                                assetImporterForCleanup.CleanupTempDirectory(importResult.Path);
                            }
                            else
                            {
                                assetImporterForCleanup.CleanupTempDirectory(localArchivePath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the full exception details
                        _emuLibrary.Logger.Warn($"Failed to clean up temp directories: {ex.Message}");
                    }
                    
                    // Step 12: Create GameInstallationData
                    UpdateProgress("Finalizing installation...", 98);
                    
                    // Prepare installation data
                    var installationData = new GameInstallationData
                    {
                        InstallDirectory = installDir
                    };
                    
                    // Preserve store-specific information
                    if (!string.IsNullOrEmpty(info.StoreGameId) && !string.IsNullOrEmpty(info.InstallerType))
                    {
                        _emuLibrary.Logger.Info($"Preserving store information for {Game.Name}: {info.InstallerType} ID {info.StoreGameId}");
                    }
                    
                    if (!string.IsNullOrEmpty(primaryExe))
                    {
                        installationData.Roms = new List<GameRom> 
                        { 
                            new GameRom(Game.Name, primaryExe) 
                        };
                        
                        // Use buffered update for game actions to reduce UI events
                        using (_emuLibrary.Playnite.Database.BufferedUpdate())
                        {
                            // Add game actions that will be applied to the game
                            if (Game.GameActions == null)
                            {
                                Game.GameActions = new ObservableCollection<GameAction>();
                            }
                            else
                            {
                                Game.GameActions.Clear();
                            }
                            
                            Game.GameActions.Add(new GameAction()
                            {
                                Name = "Play",
                                Type = GameActionType.File, 
                                Path = primaryExe,
                                IsPlayAction = true
                            });
                        }
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
                        
                        // Clean up temp directories
                        if (!string.IsNullOrEmpty(tempDir) && Directory.Exists(tempDir))
                        {
                            _emuLibrary.Logger.Info($"Cleaning up temp directory {tempDir} after cancellation");
                            Directory.Delete(tempDir, true);
                        }
                        
                        // Clean up the imported archive file if it exists
                        if (!string.IsNullOrEmpty(localArchivePath) && File.Exists(localArchivePath))
                        {
                            _emuLibrary.Logger.Info($"Cleaning up imported archive file {localArchivePath} after cancellation");
                            
                            var assetImporterForCleanup = AssetImporter.AssetImporter.Instance ?? 
                                new AssetImporter.AssetImporter(_emuLibrary.Logger, _emuLibrary.Playnite);
                                
                            assetImporterForCleanup.CleanupTempDirectory(localArchivePath);
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
                        
                        // Clean up temp directories
                        if (!string.IsNullOrEmpty(tempDir) && Directory.Exists(tempDir))
                        {
                            _emuLibrary.Logger.Info($"Cleaning up temp directory {tempDir} after failure");
                            Directory.Delete(tempDir, true);
                        }
                        
                        // Clean up the imported archive file if it exists
                        if (!string.IsNullOrEmpty(localArchivePath) && File.Exists(localArchivePath))
                        {
                            _emuLibrary.Logger.Info($"Cleaning up imported archive file {localArchivePath} after failure");
                            
                            var assetImporterForCleanup = AssetImporter.AssetImporter.Instance ?? 
                                new AssetImporter.AssetImporter(_emuLibrary.Logger, _emuLibrary.Playnite);
                                
                            assetImporterForCleanup.CleanupTempDirectory(Path.GetDirectoryName(localArchivePath));
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
        
        private class ExtractionMetrics
        {
            public DateTime StartTime { get; set; }
            public long TotalBytesProcessed { get; set; }
            public int FilesProcessed { get; set; }
            public int TotalFiles { get; set; }
            public string CurrentFile { get; set; }
            public int PercentComplete { get; set; }
            public long BytesPerSecond { get; set; }
            public long ArchiveSize { get; set; }
            public bool HasCorruptionWarning { get; set; }
            public bool HasPasswordError { get; set; }
            public List<string> ErrorMessages { get; set; } = new List<string>();
            
            public double GetTimeElapsed()
            {
                return (DateTime.Now - StartTime).TotalSeconds;
            }
            
            public void UpdateSpeed()
            {
                double seconds = GetTimeElapsed();
                if (seconds > 0)
                {
                    BytesPerSecond = (long)(TotalBytesProcessed / seconds);
                }
            }
            
            public string GetStatusMessage()
            {
                if (HasPasswordError)
                {
                    return "Wrong password for archive";
                }
                else if (HasCorruptionWarning)
                {
                    return "Archive may be corrupted";
                }
                else if (TotalFiles > 0)
                {
                    return $"Extracting: {PercentComplete}% - {FilesProcessed}/{TotalFiles} files";
                }
                else
                {
                    return $"Extracting: {PercentComplete}%";
                }
            }
        }
        
        // Maximum number of retries for archive extraction
        private const int MaxExtractionRetries = 3;
        // Parallel processing for large files
        private const bool UseParallelProcessing = true;
        
        private async Task<bool> ExtractArchiveAsync(string archivePath, string extractPath, string password, CancellationToken cancellationToken)
        {
            ExtractionMetrics metrics = new ExtractionMetrics
            {
                StartTime = DateTime.Now
            };
            
            // Get archive size if available
            try
            {
                var fileInfo = new FileInfo(archivePath);
                metrics.ArchiveSize = fileInfo.Length;
            }
            catch
            {
                // Ignore errors getting file size
            }
            
            int retryCount = 0;
            bool success = false;
            
            while (retryCount < MaxExtractionRetries && !success)
            {
                try
                {
                    // If this is a retry, clear the extraction directory and reset metrics
                    if (retryCount > 0)
                    {
                        _emuLibrary.Logger.Info($"Retry {retryCount}/{MaxExtractionRetries} for extracting {archivePath}");
                        
                        // Notify the user of the retry
                        _emuLibrary.Playnite.Notifications.Add(
                            Game.GameId,
                            $"Retrying archive extraction ({retryCount}/{MaxExtractionRetries})...",
                            NotificationType.Info
                        );
                        
                        try
                        {
                            // Clear extraction directory for clean retry
                            if (Directory.Exists(extractPath))
                            {
                                Directory.Delete(extractPath, true);
                                Directory.CreateDirectory(extractPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            _emuLibrary.Logger.Error($"Failed to clear extraction directory for retry: {ex.Message}");
                        }
                        
                        // Reset metrics for the retry
                        metrics = new ExtractionMetrics
                        {
                            StartTime = DateTime.Now,
                            ArchiveSize = metrics.ArchiveSize
                        };
                        
                        // Wait briefly before retry
                        await Task.Delay(1000 * retryCount, cancellationToken);
                    }
                
                    _emuLibrary.Logger.Info($"Extracting archive {archivePath} to {extractPath}");
                    
                    // Create process to run 7-Zip
                    var sevenZipPath = FindSevenZipPath();
                    if (string.IsNullOrEmpty(sevenZipPath))
                    {
                        throw new FileNotFoundException("7-Zip executable not found. Please make sure 7-Zip is installed and in PATH.");
                    }
                    
                    // Build command line arguments
                    var args = "x ";
                    args += $"\"{archivePath}\" "; // Quote path to handle spaces
                    args += $"-o\"{extractPath}\" "; // Output directory
                    args += "-y "; // Yes to all prompts
                    
                    // Add parallel processing for large archives
                    if (UseParallelProcessing && metrics.ArchiveSize > 100 * 1024 * 1024) // 100MB threshold
                    {
                        args += "-mmt=on "; // Enable multi-threading
                    }
                    
                    // Add password if provided
                    if (!string.IsNullOrEmpty(password))
                    {
                        args += $"-p{password} ";
                    }
                    
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = sevenZipPath,
                        Arguments = args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    
                    _emuLibrary.Logger.Debug($"Running 7-Zip: {sevenZipPath} {args}");
                    
                    using (var process = new Process { StartInfo = processStartInfo })
                    {
                        // Execute the process
                        process.Start();
                        
                        // Read output and error asynchronously
                        var outputTask = Task.Run(() => {
                            string line;
                            while ((line = process.StandardOutput.ReadLine()) != null)
                            {
                                _emuLibrary.Logger.Debug($"7-Zip output: {line}");
                                
                                // Parse the output line for extraction information
                                ParseExtractionOutput(line, metrics);
                                
                                // Update progress based on extraction metrics
                                metrics.UpdateSpeed();
                                int progressValue = 15 + (int)(metrics.PercentComplete * 0.15);
                                
                                // Format the progress message
                                string speedStr = metrics.BytesPerSecond > 0 
                                    ? $" ({metrics.BytesPerSecond / (1024 * 1024)} MB/s)" 
                                    : "";
                                
                                UpdateProgress($"{metrics.GetStatusMessage()}{speedStr}", progressValue);
                            }
                        }, cancellationToken);
                        
                        var errorTask = Task.Run(() => {
                            string line;
                            while ((line = process.StandardError.ReadLine()) != null)
                            {
                                _emuLibrary.Logger.Error($"7-Zip error: {line}");
                                metrics.ErrorMessages.Add(line);
                                
                                // Check for specific error types
                                if (line.Contains("Wrong password", StringComparison.OrdinalIgnoreCase))
                                {
                                    metrics.HasPasswordError = true;
                                }
                                else if (line.Contains("corrupt", StringComparison.OrdinalIgnoreCase) ||
                                         line.Contains("CRC failed", StringComparison.OrdinalIgnoreCase) ||
                                         line.Contains("data error", StringComparison.OrdinalIgnoreCase))
                                {
                                    metrics.HasCorruptionWarning = true;
                                }
                            }
                        }, cancellationToken);
                        
                        // Check for cancellation while waiting
                        while (!process.HasExited)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                try
                                {
                                    process.Kill();
                                    _emuLibrary.Logger.Info("7-Zip process was cancelled and terminated");
                                    return false;
                                }
                                catch (Exception ex)
                                {
                                    _emuLibrary.Logger.Error($"Failed to kill 7-Zip process: {ex.Message}");
                                }
                            }
                            await Task.Delay(100, cancellationToken);
                        }
                        
                        // Wait for output/error processing to complete
                        await Task.WhenAll(outputTask, errorTask);
                        
                        // Check for specific error types that would benefit from retrying
                        if (metrics.HasPasswordError)
                        {
                            throw new InvalidOperationException("Wrong password for the archive. Please check the password and try again.");
                        }
                        
                        // Check exit code
                        if (process.ExitCode != 0)
                        {
                            string errorDetail = metrics.ErrorMessages.Count > 0 
                                ? $": {string.Join(", ", metrics.ErrorMessages)}" 
                                : "";
                                
                            if (metrics.HasCorruptionWarning)
                            {
                                throw new IOException($"Archive appears to be corrupted{errorDetail}");
                            }
                            else
                            {
                                throw new IOException($"7-Zip process exited with code {process.ExitCode}{errorDetail}");
                            }
                        }
                        
                        // Verify the extraction was successful
                        var extractedFiles = Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories);
                        if (extractedFiles.Length == 0)
                        {
                            throw new IOException("No files were extracted from the archive. The archive may be empty or corrupted.");
                        }
                        
                        success = true;
                        return true;
                    }
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Wrong password"))
                {
                    // Specific handling for password errors - don't retry
                    _emuLibrary.Logger.Error($"Wrong password for archive: {archivePath}");
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Failed to extract {Game.Name}: Wrong password for archive.",
                        NotificationType.Error
                    );
                    return false;
                }
                catch (IOException ex) when (ex.Message.Contains("corrupted") || ex.Message.Contains("CRC") || ex.Message.Contains("data error"))
                {
                    // Archive corruption errors are worth retrying
                    _emuLibrary.Logger.Error($"Archive corruption detected: {ex.Message}");
                    retryCount++;
                    
                    // If this was the last retry, notify the user
                    if (retryCount >= MaxExtractionRetries)
                    {
                        _emuLibrary.Playnite.Notifications.Add(
                            Game.GameId,
                            $"Failed to extract {Game.Name} after {MaxExtractionRetries} attempts: Archive appears to be corrupted.",
                            NotificationType.Error
                        );
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _emuLibrary.Logger.Error($"Error extracting archive: {ex.Message}");
                    retryCount++;
                    
                    // If this was the last retry, notify the user
                    if (retryCount >= MaxExtractionRetries)
                    {
                        _emuLibrary.Playnite.Notifications.Add(
                            Game.GameId,
                            $"Failed to extract {Game.Name} after {MaxExtractionRetries} attempts: {ex.Message}",
                            NotificationType.Error
                        );
                    }
                }
            }
            
            return success;
        }
        
        private void ParseExtractionOutput(string line, ExtractionMetrics metrics)
        {
            try
            {
                // Example: " 47% 12 - file.bin"
                if (line.Contains("%"))
                {
                    // Try to parse percentage
                    int percentIndex = line.IndexOf("%");
                    if (percentIndex > 0)
                    {
                        string percentStr = line.Substring(0, percentIndex).Trim();
                        if (int.TryParse(percentStr, out int percent))
                        {
                            metrics.PercentComplete = percent;
                        }
                    }
                    
                    // Try to parse file count if available
                    // Format: " 47% 12 - file.bin"
                    int dashIndex = line.IndexOf(" - ");
                    if (dashIndex > percentIndex)
                    {
                        string fileCountStr = line.Substring(percentIndex + 1, dashIndex - percentIndex - 1).Trim();
                        if (int.TryParse(fileCountStr, out int fileCount))
                        {
                            metrics.FilesProcessed = fileCount;
                        }
                        
                        // Get current file
                        if (line.Length > dashIndex + 3)
                        {
                            metrics.CurrentFile = line.Substring(dashIndex + 3);
                        }
                    }
                }
                // Look for total files count
                else if (line.Contains("files"))
                {
                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        if (parts[i + 1].Equals("files", StringComparison.OrdinalIgnoreCase) &&
                            int.TryParse(parts[i], out int totalFiles))
                        {
                            metrics.TotalFiles = totalFiles;
                            break;
                        }
                    }
                }
                // Look for size information
                else if (line.Contains("bytes"))
                {
                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        if (parts[i + 1].Equals("bytes", StringComparison.OrdinalIgnoreCase) &&
                            long.TryParse(parts[i], out long bytes))
                        {
                            metrics.TotalBytesProcessed = bytes;
                            break;
                        }
                    }
                }
                // Check for warnings or errors
                else if (line.Contains("Warning", StringComparison.OrdinalIgnoreCase) ||
                         line.Contains("Error", StringComparison.OrdinalIgnoreCase))
                {
                    metrics.ErrorMessages.Add(line);
                    
                    // Check for specific error types
                    if (line.Contains("corrupt", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("CRC failed", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("data error", StringComparison.OrdinalIgnoreCase))
                    {
                        metrics.HasCorruptionWarning = true;
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }
        }
        
        private string FindSevenZipPath()
        {
            // Check common installation paths
            var possiblePaths = new[]
            {
                "7z", // If in PATH
                "7z.exe", // If in PATH (Windows)
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe"),
                "/usr/bin/7z", // Linux
                "/usr/local/bin/7z" // macOS
            };
            
            foreach (var path in possiblePaths)
            {
                try
                {
                    // For PATH entries, check using where/which
                    if (path == "7z" || path == "7z.exe")
                    {
                        var command = Environment.OSVersion.Platform == PlatformID.Win32NT ? "where" : "which";
                        var psi = new ProcessStartInfo
                        {
                            FileName = command,
                            Arguments = path,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        };
                        
                        using (var process = new Process { StartInfo = psi })
                        {
                            process.Start();
                            var output = process.StandardOutput.ReadToEnd().Trim();
                            process.WaitForExit();
                            
                            if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                            {
                                // Return the first line if multiple paths returned
                                var firstPath = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                                if (!string.IsNullOrEmpty(firstPath) && File.Exists(firstPath))
                                {
                                    return firstPath;
                                }
                            }
                        }
                    }
                    else if (File.Exists(path))
                    {
                        return path;
                    }
                }
                catch (Exception ex)
                {
                    _emuLibrary.Logger.Debug($"Error checking 7-Zip path {path}: {ex.Message}");
                }
            }
            
            return null;
        }
        
        // Struct to hold information about found ISO files
        private struct ISOFileInfo
        {
            public string FilePath { get; set; }
            public long FileSize { get; set; }
            public DateTime LastModified { get; set; }
            public bool IsPrimaryDisc { get; set; }
            public int DiscNumber { get; set; }
            public string GameName { get; set; }
            
            public override string ToString()
            {
                return Path.GetFileName(FilePath);
            }
        }
        
        private List<string> FindISOFiles(string directory)
        {
            var isoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".iso", ".bin", ".cue", ".mdf", ".mds", ".img", ".nrg", ".cdi"
            };
            
            var results = new List<string>();
            var detectedIsoInfo = new List<ISOFileInfo>();
            
            try
            {
                foreach (var extension in isoExtensions)
                {
                    var files = Directory.GetFiles(directory, $"*{extension}", SearchOption.AllDirectories);
                    
                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        var isoInfo = new ISOFileInfo
                        {
                            FilePath = file,
                            FileSize = fileInfo.Length,
                            LastModified = fileInfo.LastWriteTime,
                            IsPrimaryDisc = false,
                            DiscNumber = -1,
                            GameName = Path.GetFileNameWithoutExtension(file)
                        };
                        
                        // Try to detect disc information from the filename
                        DetectDiscInformation(ref isoInfo);
                        
                        detectedIsoInfo.Add(isoInfo);
                    }
                }
                
                // Log found ISOs for debugging
                if (detectedIsoInfo.Count > 0)
                {
                    _emuLibrary.Logger.Info($"Found {detectedIsoInfo.Count} ISO files in extracted archive");
                    
                    // Check for multi-disc games
                    var discGroups = detectedIsoInfo
                        .Where(iso => iso.DiscNumber > 0)
                        .GroupBy(iso => iso.GameName)
                        .Where(g => g.Count() > 1)
                        .ToList();
                    
                    if (discGroups.Count > 0)
                    {
                        foreach (var group in discGroups)
                        {
                            _emuLibrary.Logger.Info($"Detected multi-disc game: {group.Key} with {group.Count()} discs");
                            foreach (var disc in group.OrderBy(d => d.DiscNumber))
                            {
                                _emuLibrary.Logger.Debug($"  Disc {disc.DiscNumber}: {Path.GetFileName(disc.FilePath)}");
                            }
                        }
                    }
                }
                
                // Return the file paths in a specific order:
                // 1. Largest ISO files first (if no disc number detected)
                // 2. Primary discs before secondary discs
                // 3. Lower disc numbers before higher disc numbers
                results = detectedIsoInfo
                    .OrderByDescending(iso => iso.IsPrimaryDisc)
                    .ThenBy(iso => iso.DiscNumber > 0 ? iso.DiscNumber : int.MaxValue)
                    .ThenByDescending(iso => iso.FileSize)
                    .Select(iso => iso.FilePath)
                    .ToList();
            }
            catch (Exception ex)
            {
                _emuLibrary.Logger.Error($"Error finding ISO files: {ex.Message}");
            }
            
            return results;
        }
        
        private void DetectDiscInformation(ref ISOFileInfo isoInfo)
        {
            string filename = Path.GetFileNameWithoutExtension(isoInfo.FilePath).ToLower();
            
            // Common disc indicators in filenames
            var discPatterns = new Dictionary<string, int>
            {
                { "disc1", 1 }, { "disc 1", 1 }, { "disc_1", 1 }, { "disc-1", 1 }, { "disk1", 1 }, { "disk 1", 1 }, { "disk_1", 1 }, { "disk-1", 1 }, { "cd1", 1 }, { "cd 1", 1 }, { "cd_1", 1 }, { "cd-1", 1 },
                { "disc2", 2 }, { "disc 2", 2 }, { "disc_2", 2 }, { "disc-2", 2 }, { "disk2", 2 }, { "disk 2", 2 }, { "disk_2", 2 }, { "disk-2", 2 }, { "cd2", 2 }, { "cd 2", 2 }, { "cd_2", 2 }, { "cd-2", 2 },
                { "disc3", 3 }, { "disc 3", 3 }, { "disc_3", 3 }, { "disc-3", 3 }, { "disk3", 3 }, { "disk 3", 3 }, { "disk_3", 3 }, { "disk-3", 3 }, { "cd3", 3 }, { "cd 3", 3 }, { "cd_3", 3 }, { "cd-3", 3 },
                { "disc4", 4 }, { "disc 4", 4 }, { "disc_4", 4 }, { "disc-4", 4 }, { "disk4", 4 }, { "disk 4", 4 }, { "disk_4", 4 }, { "disk-4", 4 }, { "cd4", 4 }, { "cd 4", 4 }, { "cd_4", 4 }, { "cd-4", 4 }
            };
            
            // Check for disc number in parentheses
            var parenthesesPatterns = new[]
            {
                @"\(disc (\d+)\)",
                @"\(disk (\d+)\)",
                @"\(cd (\d+)\)",
                @"\[disc (\d+)\]",
                @"\[disk (\d+)\]",
                @"\[cd (\d+)\]"
            };
            
            // Check for disc patterns
            foreach (var pattern in discPatterns)
            {
                if (filename.Contains(pattern.Key))
                {
                    isoInfo.DiscNumber = pattern.Value;
                    isoInfo.IsPrimaryDisc = (pattern.Value == 1);
                    
                    // Extract game name by removing disc info
                    string discPart = pattern.Key;
                    int discIndex = filename.IndexOf(discPart, StringComparison.OrdinalIgnoreCase);
                    if (discIndex > 0)
                    {
                        isoInfo.GameName = filename.Substring(0, discIndex).Trim();
                    }
                    
                    return;
                }
            }
            
            // Check for disc number in parentheses
            foreach (var pattern in parenthesesPatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(filename, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1 && int.TryParse(match.Groups[1].Value, out int discNum))
                {
                    isoInfo.DiscNumber = discNum;
                    isoInfo.IsPrimaryDisc = (discNum == 1);
                    
                    // Extract game name by removing parentheses part
                    string fullMatch = match.Groups[0].Value;
                    int matchIndex = filename.IndexOf(fullMatch, StringComparison.OrdinalIgnoreCase);
                    if (matchIndex > 0)
                    {
                        isoInfo.GameName = filename.Substring(0, matchIndex).Trim();
                    }
                    
                    return;
                }
            }
        }
        
        private async Task<string> SelectISOFileAsync(List<string> isoFiles, CancellationToken cancellationToken)
        {
            string selectedISO = null;
            
            await Task.Run(() =>
            {
                _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                {
                    // Check if cancelled before showing dialog
                    if (cancellationToken.IsCancellationRequested)
                        return;
                        
                    // Create a message listing the ISO files found
                    var topIsoOptions = isoFiles.Take(10)
                        .Select(p => Path.GetFileName(p))
                        .ToList();
                    
                    string isoOptionsMessage = "Multiple ISO files found:";
                    foreach (var iso in topIsoOptions)
                    {
                        isoOptionsMessage += $"\n- {iso}";
                    }
                    
                    if (isoFiles.Count > 10)
                    {
                        isoOptionsMessage += $"\n- Plus {isoFiles.Count - 10} more...";
                    }
                    
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"{isoOptionsMessage}\n\nPlease select an ISO to mount.",
                        NotificationType.Info
                    );
                    
                    // Use Playnite's file selection dialog in the extraction directory
                    var selectFileDialog = _emuLibrary.Playnite.Dialogs.SelectFile(
                        "ISO files|*.iso;*.bin;*.cue;*.mdf;*.mds;*.img", 
                        Path.GetDirectoryName(isoFiles[0])
                    );
                    
                    if (!string.IsNullOrEmpty(selectFileDialog))
                    {
                        selectedISO = selectFileDialog;
                        _emuLibrary.Logger.Info($"User selected ISO: {selectedISO}");
                    }
                    else
                    {
                        // If user cancels, try to use the first ISO
                        selectedISO = isoFiles[0];
                        _emuLibrary.Logger.Info($"User cancelled ISO selection. Using first ISO: {selectedISO}");
                    }
                });
            }, cancellationToken);
            
            return selectedISO;
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
#else
                // Dummy implementation for non-Windows platforms (only for compilation)
                var results = new Collection<PSObject>();
#endif
                    
#if WINDOWS
                    if (ps.HadErrors || results.Count == 0)
#else
                    if (results.Count == 0)
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