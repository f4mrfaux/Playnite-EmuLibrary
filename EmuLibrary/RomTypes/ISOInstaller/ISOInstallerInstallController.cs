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
                // Track installation state for logging
                var tempDir = string.Empty;
                var mountPoint = string.Empty;
                var localISOPath = string.Empty;
                
                try
                {
                    // Create a temporary directory for the ISO operation
                    UpdateProgress("Creating temporary directory...", 0);
                    
                    tempDir = Path.Combine(Path.GetTempPath(), "Playnite_ISOInstaller", Guid.NewGuid().ToString());
                    Directory.CreateDirectory(tempDir);
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _emuLibrary.Logger.Info($"Installation of {Game.Name} was cancelled during preparation");
                        Game.IsInstalling = false;
                        return;
                    }
                    
                    // Verify source file exists
                    if (string.IsNullOrEmpty(info.SourceFullPath))
                    {
                        // If SourceFullPath is null, check the InstallerFullPath directly
                        if (string.IsNullOrEmpty(info.InstallerFullPath))
                        {
                            throw new FileNotFoundException($"ISO file path is not set. Both SourceFullPath and InstallerFullPath are empty.");
                        }
                        else if (!File.Exists(info.InstallerFullPath))
                        {
                            throw new FileNotFoundException($"ISO file not found at InstallerFullPath: {info.InstallerFullPath}");
                        }
                        else
                        {
                            // Use InstallerFullPath directly
                            _emuLibrary.Logger.Info($"Using InstallerFullPath directly: {info.InstallerFullPath}");
                            // Fix the issue by setting the local variable to use later
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
                    
                    // Import the ISO file to local temp storage first
                    UpdateProgress("Importing ISO file to local storage...", 5);
                    
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Importing ISO file for {Game.Name} to local storage...",
                        NotificationType.Info
                    );
                    
                    // Get or create the AssetImporter
                    var assetImporter = Util.AssetImporter.AssetImporter.Instance ?? 
                        new Util.AssetImporter.AssetImporter(_emuLibrary.Logger, _emuLibrary.Playnite);
                    
                    // Register for progress updates
                    assetImporter.ImportProgress += (sender, e) => {
                        // Calculate progress percentage from 5% to 10% during import
                        int progressValue = 5 + (int)(e.Progress * 5);
                        UpdateProgress($"Importing ISO file: {e.BytesTransferred / (1024 * 1024)} MB / {e.TotalBytes / (1024 * 1024)} MB", progressValue);
                    };
                    
                    // Use app mode to determine dialog visibility
                    bool showDialog = _emuLibrary.Playnite.ApplicationInfo.Mode == ApplicationMode.Desktop ?
                        Settings.Settings.Instance.UseWindowsCopyDialogInDesktopMode :
                        Settings.Settings.Instance.UseWindowsCopyDialogInFullscreenMode;
                    
                    // Import the asset
                    var importResult = await assetImporter.ImportAsync(
                        info.SourceFullPath, 
                        showDialog, 
                        cancellationToken);
                    
                    if (!importResult.Success || string.IsNullOrEmpty(importResult.Path) || !File.Exists(importResult.Path))
                    {
                        if (importResult.Error != null)
                        {
                            throw new Exception($"Failed to import ISO file: {importResult.Error.Message}", importResult.Error);
                        }
                        else
                        {
                            throw new FileNotFoundException($"Failed to import ISO file to local storage: {info.SourceFullPath}");
                        }
                    }
                    
                    localISOPath = importResult.Path;
                    
                    if (importResult.FromCache)
                    {
                        _emuLibrary.Logger.Info($"Using cached ISO file: {localISOPath}");
                    }
                    else
                    {
                        _emuLibrary.Logger.Info($"ISO file imported successfully to {localISOPath}");
                    }
                    
                    // Mount the ISO file using PowerShell (now using the local copy)
                    UpdateProgress("Mounting ISO file...", 10);
                    
                    _emuLibrary.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Mounting ISO file for {Game.Name}...",
                        NotificationType.Info
                    );
                    
                    mountPoint = MountIsoFile(localISOPath);
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
                    UpdateProgress("Finding installers in ISO...", 20);
                    
                    var exeFiles = Directory.GetFiles(mountPoint, "*.exe", SearchOption.AllDirectories)
                        .OrderBy(f => Path.GetFileName(f))
                        .ToList();
                    
                    if (exeFiles.Count == 0)
                    {
                        throw new FileNotFoundException("No executable files found in the mounted ISO.");
                    }
                    
                    // Ask user to select installer from the ISO
                    UpdateProgress("Selecting installer from ISO...", 30);
                    
                    string selectedInstaller = null;
                    _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                    {
                        string gameName = Game.Name?.ToLowerInvariant() ?? "";
                        string gameBaseName = null;
                        
                        // Extract base game name (without numbers, special chars)
                        if (!string.IsNullOrEmpty(gameName))
                        {
                            gameBaseName = Regex.Replace(gameName, @"[^a-z]", "");
                            _emuLibrary.Logger.Info($"Derived base game name for installer matching: {gameBaseName}");
                        }
                        
                        // Common installer file patterns to look for
                        var commonInstallerNames = new[] { 
                            "setup.exe", "install.exe", "autorun.exe", "start.exe", "launch.exe", 
                            "launcher.exe", "game.exe", "installer.exe", "quicksetup.exe",
                            "steamsetup.exe", "origin.exe", "epicinstaller.exe",
                            Path.GetFileNameWithoutExtension(info.SourceFullPath) + ".exe" 
                        };
                        
                        // Score-based installer detection
                        var scoredInstallers = new Dictionary<string, int>();
                        
                        // First pass: exact name match with common installer names
                        foreach (var exePath in exeFiles)
                        {
                            string fileName = Path.GetFileName(exePath).ToLowerInvariant();
                            int score = 0;
                            
                            // Exact match with common installer patterns
                            foreach (var commonName in commonInstallerNames)
                            {
                                if (fileName.Equals(commonName, StringComparison.OrdinalIgnoreCase))
                                {
                                    score += 10;
                                    break;
                                }
                            }
                            
                            // Contains common installer keywords
                            if (fileName.Contains("setup") || fileName.Contains("install") || 
                                fileName.Contains("launch") || fileName.Contains("start"))
                            {
                                score += 5;
                            }
                            
                            // Contains game name (indicates main executable)
                            if (!string.IsNullOrEmpty(gameBaseName) && gameBaseName.Length > 3 && 
                                fileName.Contains(gameBaseName))
                            {
                                score += 3;
                            }
                            
                            // Location-based score (installers are often in the root)
                            var dirName = Path.GetDirectoryName(exePath)?.ToLowerInvariant() ?? "";
                            
                            // Root directory installers get higher score
                            if (dirName == mountPoint.ToLowerInvariant() || 
                                dirName == Path.Combine(mountPoint, "setup").ToLowerInvariant() ||
                                dirName == Path.Combine(mountPoint, "install").ToLowerInvariant())
                            {
                                score += 4;
                            }
                            
                            // Lower score for executables in subdirectories like "redist" or "support"
                            if (dirName.Contains("redist") || dirName.Contains("vcredist") || 
                                dirName.Contains("dotnet") || dirName.Contains("support") ||
                                dirName.Contains("unins") || dirName.Contains("prereq"))
                            {
                                score -= 5;
                            }
                            
                            // Size-based heuristics - setup files tend to be larger
                            try
                            {
                                var fileInfo = new FileInfo(exePath);
                                // Large files are more likely to be installers (>1MB)
                                if (fileInfo.Length > 1024 * 1024)
                                {
                                    score += 2;
                                }
                            }
                            catch {}
                            
                            // Store the score
                            scoredInstallers[exePath] = score;
                        }
                        
                        // Sort by score descending
                        var sortedInstallers = scoredInstallers.OrderByDescending(pair => pair.Value)
                            .Select(pair => new { Path = pair.Key, Score = pair.Value })
                            .ToList();
                            
                        if (sortedInstallers.Any())
                        {
                            _emuLibrary.Logger.Info($"Top 3 potential installers:");
                            foreach (var installer in sortedInstallers.Take(3))
                            {
                                _emuLibrary.Logger.Info($"- {Path.GetFileName(installer.Path)} (Score: {installer.Score})");
                            }
                            
                            // If highest scoring installer has a significant score (>5), use it automatically
                            if (sortedInstallers.First().Score > 5 && 
                               (sortedInstallers.Count == 1 || sortedInstallers.First().Score > sortedInstallers[1].Score + 3))
                            {
                                selectedInstaller = sortedInstallers.First().Path;
                                _emuLibrary.Logger.Info($"Auto-selected highest scoring installer: {Path.GetFileName(selectedInstaller)} (Score: {sortedInstallers.First().Score})");
                            }
                        }
                        
                        // If no suitable installer was automatically selected, ask the user
                        if (selectedInstaller == null)
                        {
                            _emuLibrary.Logger.Info("No clear installer identified. User will select one.");
                            
                            if (exeFiles.Count > 0)
                            {
                                // Show the top-scored installers first in the message
                                var recommendedInstallers = sortedInstallers
                                    .Where(i => i.Score > 0)
                                    .Take(3)
                                    .Select(i => Path.GetFileName(i.Path))
                                    .ToList();
                                
                                string exeOptionsMessage = "Found these executables in the ISO:";
                                
                                // Show recommended installers first if available
                                if (recommendedInstallers.Any())
                                {
                                    exeOptionsMessage += "\n\nRECOMMENDED INSTALLERS:";
                                    foreach (var exe in recommendedInstallers)
                                    {
                                        exeOptionsMessage += $"\n- {exe}";
                                    }
                                    exeOptionsMessage += "\n\nOTHER OPTIONS:";
                                }
                                
                                // Then show other executables
                                var otherInstallers = exeFiles
                                    .Where(f => !recommendedInstallers.Contains(Path.GetFileName(f)))
                                    .Take(5)
                                    .Select(f => Path.GetFileName(f))
                                    .ToList();
                                
                                foreach (var exe in otherInstallers)
                                {
                                    exeOptionsMessage += $"\n- {exe}";
                                }
                                
                                if (exeFiles.Count > 8) // 3 recommended + 5 others
                                {
                                    exeOptionsMessage += $"\n- Plus {exeFiles.Count - 8} more...";
                                }
                                
                                _emuLibrary.Playnite.Notifications.Add(
                                    Game.GameId,
                                    $"{exeOptionsMessage}\n\nPlease select an installer for {Game.Name}.",
                                    NotificationType.Info
                                );
                                
                                // Use Playnite's file selection dialog
                                var selectFileDialog = _emuLibrary.Playnite.Dialogs.SelectFile("Executable files (*.exe)|*.exe");
                                
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
                                        NotificationType.Error
                                    );
                                    
                                    // Try one more time with a more direct prompt
                                    var secondAttempt = _emuLibrary.Playnite.Dialogs.SelectFile("Executable files (*.exe)|*.exe");
                                    
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
                    
                    // Show a notification to the user
                    UpdateProgress("Running installer...", 40);
                    
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
                    
                    // Don't create nested tasks - use await directly
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
                        // No need to wait again, we already waited in the loop
                    }, cancellationToken);
                    
                    if (cancellationToken.IsCancellationRequested)
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
                    
                    // Update the game info with the installation directory
                    UpdateProgress("Configuring game...", 80);
                    
                    info.InstallDirectory = installDir;
                    
                    // Find executable files in the installation directory for potential play action
                    string primaryExe = null;
                    try
                    {
                        var installedExeFiles = Directory.GetFiles(installDir, "*.exe", SearchOption.AllDirectories);
                        
                        if (installedExeFiles.Length > 0)
                        {
                            _emuLibrary.Logger.Info($"Found {installedExeFiles.Length} executable files in installation directory");
                            
                            // Extract game name for matching
                            string gameName = Game.Name?.ToLowerInvariant() ?? "";
                            string gameBaseName = null;
                            
                            // Extract base game name (without numbers, special chars) for matching
                            if (!string.IsNullOrEmpty(gameName))
                            {
                                gameBaseName = Regex.Replace(gameName, @"[^a-z]", "");
                                _emuLibrary.Logger.Info($"Derived base game name for executable matching: {gameBaseName}");
                            }
                            
                            // More comprehensive list of common main executable names
                            var commonMainExeNames = new[] { 
                                "launcher.exe", "game.exe", "play.exe", "start.exe", "run.exe",
                                "client.exe", "app.exe", "main.exe", "bin.exe",
                                Game.Name.ToLower() + ".exe" 
                            };
                            
                            // Score all executables to find the most likely main game executable
                            var scoredExecutables = new Dictionary<string, int>();
                            
                            foreach (var exePath in installedExeFiles)
                            {
                                string fileName = Path.GetFileName(exePath).ToLowerInvariant();
                                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(exePath).ToLowerInvariant();
                                string directory = Path.GetDirectoryName(exePath)?.ToLowerInvariant() ?? "";
                                int score = 0;
                                
                                // Exact matches with common names
                                foreach (var commonName in commonMainExeNames)
                                {
                                    if (fileName.Equals(commonName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        score += 8;
                                        break;
                                    }
                                }
                                
                                // Check for exact game name match
                                if (fileNameWithoutExt.Equals(gameName, StringComparison.OrdinalIgnoreCase))
                                {
                                    score += 10;
                                    _emuLibrary.Logger.Info($"Found exact game name match: {fileName}");
                                }
                                // Check for close game name match (contains base name)
                                else if (!string.IsNullOrEmpty(gameBaseName) && gameBaseName.Length > 3 && 
                                        fileNameWithoutExt.Contains(gameBaseName))
                                {
                                    score += 7;
                                    _emuLibrary.Logger.Info($"Found partial game name match: {fileName} contains {gameBaseName}");
                                }
                                
                                // Penalize common utility applications
                                if (fileName.Contains("unins") || fileName.Contains("setup") || 
                                    fileName.Contains("config") || fileName.Contains("install") ||
                                    fileName.Contains("update") || fileName.Contains("patch") ||
                                    fileName.Contains("crash") || fileName.Contains("redist") ||
                                    fileName.Contains("settings"))
                                {
                                    score -= 6;
                                }
                                
                                // Location-based scoring
                                
                                // Prefer executables in common game binary locations
                                if (directory.EndsWith("\\bin") || directory.EndsWith("\\binaries") ||
                                    directory.EndsWith("\\game") || directory.EndsWith("\\app") ||
                                    directory.EndsWith("\\engine\\binaries\\win64")) // Unreal Engine pattern
                                {
                                    score += 4;
                                }
                                
                                // Penalize executables in system or utility folders
                                if (directory.Contains("\\redist") || directory.Contains("\\support") || 
                                    directory.Contains("\\tools") || directory.Contains("\\vcredist") ||
                                    directory.Contains("\\utilities") || directory.Contains("\\setup"))
                                {
                                    score -= 5;
                                }
                                
                                // File size heuristic (main executables are usually larger)
                                try
                                {
                                    var fileInfo = new FileInfo(exePath);
                                    // Main game executables are often larger (>10MB)
                                    if (fileInfo.Length > 10 * 1024 * 1024)
                                    {
                                        score += 3;
                                    }
                                    // Very small executables are likely utilities
                                    else if (fileInfo.Length < 1 * 1024 * 1024)
                                    {
                                        score -= 2;
                                    }
                                }
                                catch {}
                                
                                // Save the score
                                scoredExecutables[exePath] = score;
                            }
                            
                            // Sort executables by score descending
                            var sortedExecutables = scoredExecutables.OrderByDescending(pair => pair.Value)
                                .Select(pair => new { Path = pair.Key, Score = pair.Value, Name = Path.GetFileName(pair.Key) })
                                .ToList();
                            
                            // Log top executables
                            _emuLibrary.Logger.Info("Top 5 potential main executables:");
                            foreach (var exe in sortedExecutables.Take(5))
                            {
                                _emuLibrary.Logger.Info($"- {exe.Name} (Score: {exe.Score})");
                            }
                            
                            // If we have a clear winner (high score and significantly higher than second place)
                            if (sortedExecutables.Count > 0 && 
                                sortedExecutables[0].Score > 5 && 
                                (sortedExecutables.Count == 1 || 
                                 sortedExecutables[0].Score > sortedExecutables[1].Score + 3))
                            {
                                primaryExe = sortedExecutables[0].Path;
                                _emuLibrary.Logger.Info($"Auto-selected highest scoring executable: {Path.GetFileName(primaryExe)} (Score: {sortedExecutables[0].Score})");
                            }
                            // If no clear winner but we have multiple options
                            else if (sortedExecutables.Count > 1)
                            {
                                _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                                {
                                    // Collect top executables for display
                                    var topExecutables = sortedExecutables.Take(5)
                                        .Select(e => $"{e.Name} (Score: {e.Score})")
                                        .ToList();
                                    
                                    string message = $"Multiple potential executables found for {Game.Name}. Recommended options:\n";
                                    foreach (var exe in topExecutables)
                                    {
                                        message += $"- {exe}\n";
                                    }
                                    
                                    _emuLibrary.Playnite.Notifications.Add(
                                        Game.GameId,
                                        message,
                                        NotificationType.Info
                                    );
                                    
                                    // For now, use highest scoring executable
                                    // TODO: Implement a proper selection dialog here
                                    primaryExe = sortedExecutables[0].Path;
                                    _emuLibrary.Logger.Info($"Selected highest scoring executable: {Path.GetFileName(primaryExe)}");
                                });
                            }
                            else if (sortedExecutables.Count == 1)
                            {
                                // If only one executable, use it
                                primaryExe = sortedExecutables[0].Path;
                                _emuLibrary.Logger.Info($"Only one executable found: {Path.GetFileName(primaryExe)}");
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
                    
                    // Unmount the ISO
                    UpdateProgress("Unmounting ISO...", 85);
                    UnmountIsoFile(mountPoint);
                    
                    // Clean up temp directory
                    UpdateProgress("Cleaning up...", 90);
                    
                    try
                    {
                        // Clean up temp directories
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, true);
                        }
                        
                        // Clean up the imported ISO file if not cached
                        if (!Settings.Settings.Instance.EnableAssetCaching)
                        {
                            // Get or create the AssetImporter
                            var assetImporterForCleanup = Util.AssetImporter.AssetImporter.Instance ?? 
                                new Util.AssetImporter.AssetImporter(_emuLibrary.Logger, _emuLibrary.Playnite);
                                
                            assetImporterForCleanup.CleanupTempDirectory(localISOPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the full exception details
                        _emuLibrary.Logger.Warn($"Failed to clean up temp directories: {ex.Message}");
                    }
                    
                    // Create GameInstallationData
                    UpdateProgress("Finalizing installation...", 95);
                    
                    // Preserve store info in installation data
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
                        
                        // Clean up the imported ISO file if it exists
                        if (!string.IsNullOrEmpty(localISOPath) && File.Exists(localISOPath))
                        {
                            _emuLibrary.Logger.Info($"Cleaning up imported ISO file {localISOPath} after cancellation");
                            
                            // Get or create the AssetImporter
                            var assetImporterForCleanup = Util.AssetImporter.AssetImporter.Instance ?? 
                                new Util.AssetImporter.AssetImporter(_emuLibrary.Logger, _emuLibrary.Playnite);
                                
                            assetImporterForCleanup.CleanupTempDirectory(localISOPath);
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
                        
                        // Clean up the imported ISO file if it exists
                        if (!string.IsNullOrEmpty(localISOPath) && File.Exists(localISOPath))
                        {
                            _emuLibrary.Logger.Info($"Cleaning up imported ISO file {localISOPath} after failure");
                            
                            // Get or create the AssetImporter
                            var assetImporterForCleanup = Util.AssetImporter.AssetImporter.Instance ?? 
                                new Util.AssetImporter.AssetImporter(_emuLibrary.Logger, _emuLibrary.Playnite);
                                
                            assetImporterForCleanup.CleanupTempDirectory(Path.GetDirectoryName(localISOPath));
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