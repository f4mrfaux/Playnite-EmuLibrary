﻿using EmuLibrary.Util.AssetImporter;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
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
        
        private async Task<bool> ExtractArchiveAsync(string archivePath, string extractPath, string password, CancellationToken cancellationToken)
        {
            try
            {
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
                            // Update progress based on output
                            if (line.Contains("%")) {
                                try {
                                    // Try to parse percentage from output
                                    var percentStr = line.Substring(line.IndexOf(" ") + 1, line.IndexOf("%") - line.IndexOf(" ") - 1);
                                    if (int.TryParse(percentStr, out int percent)) {
                                        // Map extraction progress (15-30%)
                                        int progressValue = 15 + (int)(percent * 0.15);
                                        UpdateProgress($"Extracting archive: {percent}%", progressValue);
                                    }
                                } catch {
                                    // Ignore parsing errors
                                }
                            }
                        }
                    }, cancellationToken);
                    
                    var errorTask = Task.Run(() => {
                        string line;
                        while ((line = process.StandardError.ReadLine()) != null)
                        {
                            _emuLibrary.Logger.Error($"7-Zip error: {line}");
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
                    
                    // Check exit code
                    if (process.ExitCode != 0)
                    {
                        _emuLibrary.Logger.Error($"7-Zip process exited with code {process.ExitCode}");
                        return false;
                    }
                    
                    return true;
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _emuLibrary.Logger.Error($"Error extracting archive: {ex.Message}");
                return false;
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
        
        private List<string> FindISOFiles(string directory)
        {
            var isoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".iso", ".bin", ".cue", ".mdf", ".mds", ".img"
            };
            
            var results = new List<string>();
            
            try
            {
                foreach (var extension in isoExtensions)
                {
                    results.AddRange(Directory.GetFiles(directory, $"*{extension}", SearchOption.AllDirectories));
                }
            }
            catch (Exception ex)
            {
                _emuLibrary.Logger.Error($"Error finding ISO files: {ex.Message}");
            }
            
            return results;
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
                
                // Use PowerShell to mount the ISO
                using (var ps = PowerShell.Create())
                {
                    // Mount the ISO and get the drive letter with proper escaping
                    string escapedPath = isoPath.Replace("'", "''").Replace("\"", "\\\"");
                    ps.AddScript($"$result = Mount-DiskImage -ImagePath \"{escapedPath}\" -PassThru; Get-Volume -DiskImage $result | Select-Object -ExpandProperty DriveLetter");
                    var results = ps.Invoke();
                    
                    if (ps.HadErrors || results.Count == 0)
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