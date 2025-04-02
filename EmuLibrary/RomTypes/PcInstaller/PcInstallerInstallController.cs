using EmuLibrary.Util;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EmuLibrary.RomTypes.PcInstaller
{
    internal class PcInstallerInstallController : BaseInstallController
    {
        private readonly ILogger _logger;
        private readonly Handlers.ArchiveHandlerFactory _archiveHandlerFactory;
        
        public PcInstallerInstallController(Game game, IEmuLibrary emuLibrary) : base(game, emuLibrary)
        {
            _logger = emuLibrary.Logger;
            _archiveHandlerFactory = new Handlers.ArchiveHandlerFactory(_logger);
        }
        
        public override void Install(InstallActionArgs args)
        {
            var info = Game.GetELGameInfo() as PcInstallerGameInfo;
            
            var dstBasePath = info.Mapping?.DestinationPathResolved ??
                throw new Exception("Mapped destination data cannot be found. Please try removing and re-adding.");
                
            // Create game-specific installation directory
            string gameInstallDir = Path.Combine(dstBasePath, StringExtensions.GetSafePathName(Game.Name));
            
            _watcherToken = new CancellationTokenSource();
            
            Task.Run(async () =>
            {
                try
                {
                    _logger.Info($"Starting installation of {Game.Name} from {info.SourceFullPath} to {gameInstallDir}");
                    
                    // Create the destination directory if it doesn't exist
                    Directory.CreateDirectory(gameInstallDir);
                    
                    // Handle installation based on file type
                    bool installSuccess = false;
                    string extension = Path.GetExtension(info.SourceFullPath).ToLower();
                    
                    // Check if it's an archive type we can handle
                    var archiveHandler = _archiveHandlerFactory.GetHandler(info.SourceFullPath);
                    if (archiveHandler != null)
                    {
                        _logger.Info($"Using specialized handler for {info.SourceFullPath}");
                        
                        // Create a temporary directory with the game name for easier identification
                        var tempPath = Path.Combine(
                            Path.GetTempPath(), 
                            "EmuLibrary_" + StringExtensions.GetSafePathName(Game.Name) + "_" + Path.GetRandomFileName());
                        
                        try
                        {
                            Directory.CreateDirectory(tempPath);
                            
                            SafelyAddNotification(
                                Guid.NewGuid().ToString(),
                                $"Extracting {Game.Name}... This may take a while depending on archive size.",
                                NotificationType.Info);
                                
                            // Add detailed extraction notification
                            SafelyAddNotification(
                                Guid.NewGuid().ToString(),
                                $"EmuLibrary is extracting {Path.GetFileName(info.SourceFullPath)}. Large archives may take several minutes.",
                                NotificationType.Info);
                                
                            var extractedPath = await archiveHandler.ExtractAsync(info.SourceFullPath, tempPath, _watcherToken.Token);
                            
                            if (string.IsNullOrEmpty(extractedPath))
                            {
                                _logger.Error($"Failed to extract {info.SourceFullPath}");
                                installSuccess = false;
                            }
                            else if (Directory.Exists(extractedPath))
                            {
                                // Check if the extracted content has an installer
                                var installerFiles = Directory.GetFiles(extractedPath, "*.exe", SearchOption.AllDirectories)
                                    .Where(f => !Path.GetFileName(f).ToLower().Contains("unins"))
                                    .ToList();
                                    
                                if (installerFiles.Any())
                                {
                                    string installerFile;
                                    
                                    // If we have multiple installers and the setting is enabled, prompt the user
                                    if (installerFiles.Count > 1 && _emuLibrary.Settings.PromptForInstallerSelection)
                                    {
                                        // Create a list of options with relative paths for cleaner display
                                        var options = installerFiles
                                            .Select(f => 
                                            {
                                                var relativePath = f.Replace(extractedPath, "").TrimStart('\\', '/');
                                                return new { DisplayName = relativePath, FullPath = f };
                                            })
                                            .ToList();
                                            
                                        // Sort the options by path length (shorter paths first)
                                        options = options.OrderBy(o => o.DisplayName.Length).ToList();
                                        
                                        // Create a dictionary for the selection dialog
                                        var selectionOptions = options.ToDictionary(
                                            o => o.DisplayName,
                                            o => o.FullPath
                                        );
                                        
                                        // Show selection dialog
                                        SafelyAddNotification(
                                            Guid.NewGuid().ToString(),
                                            $"Multiple installers found for {Game.Name}. Please select which one to use.",
                                            NotificationType.Info);
                                            
                                        // We need to use the UI thread for this
                                        string selectedOption = null;
                                        if (_emuLibrary?.Playnite?.MainView?.UIDispatcher != null)
                                        {
                                            _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                                            {
                                                selectedOption = _emuLibrary.Playnite.Dialogs.SelectString(
                                                    "Multiple installers found. Please select which one to use:",
                                                    "Select Installer",
                                                    selectionOptions.Keys.ToList());
                                            });
                                        }
                                        
                                        if (!string.IsNullOrEmpty(selectedOption) && selectionOptions.TryGetValue(selectedOption, out string selectedPath))
                                        {
                                            installerFile = selectedPath;
                                            _logger.Info($"User selected installer: {installerFile}");
                                        }
                                        else
                                        {
                                            // User cancelled or dialog failed, use the first one
                                            installerFile = installerFiles.First();
                                            _logger.Info($"Using default installer: {installerFile}");
                                        }
                                    }
                                    else
                                    {
                                        // Use the first installer
                                        installerFile = installerFiles.First();
                                        _logger.Info($"Found installer in extracted content: {installerFile}");
                                    }
                                    
                                    installSuccess = await RunInstallerExecutableAsync(installerFile, gameInstallDir, _watcherToken.Token);
                                }
                                else
                                {
                                    // No installer found, just copy the extracted content
                                    _logger.Info($"No installer found, copying extracted content to {gameInstallDir}");
                                    CopyDirectory(extractedPath, gameInstallDir);
                                    installSuccess = true;
                                }
                            }
                            else if (File.Exists(extractedPath))
                            {
                                // The handler extracted a specific file (likely an installer)
                                _logger.Info($"Handler extracted a specific file: {extractedPath}");
                                installSuccess = await RunInstallerExecutableAsync(extractedPath, gameInstallDir, _watcherToken.Token);
                            }
                        }
                        finally
                        {
                            // Clean up the temp directory
                            try
                            {
                                if (Directory.Exists(tempPath))
                                    Directory.Delete(tempPath, true);
                            }
                            catch (Exception ex)
                            {
                                _logger.Error(ex, $"Error cleaning up temp directory: {tempPath}");
                            }
                        }
                    }
                    else if (extension == ".exe" || extension == ".msi")
                    {
                        installSuccess = await RunInstallerExecutableAsync(info.SourceFullPath, gameInstallDir, _watcherToken.Token);
                    }
                    else if (extension == ".iso")
                    {
                        // Try to use the IsoHandler directly
                        var isoHandler = new Handlers.IsoHandler(_logger);
                        if (isoHandler.CanHandle(info.SourceFullPath))
                        {
                            // Create a temporary directory with the game name for easier identification
                            var tempPath = Path.Combine(
                                Path.GetTempPath(), 
                                "EmuLibrary_ISO_" + StringExtensions.GetSafePathName(Game.Name) + "_" + Path.GetRandomFileName());
                            
                            try
                            {
                                Directory.CreateDirectory(tempPath);
                                
                                SafelyAddNotification(
                                    Guid.NewGuid().ToString(),
                                    $"Extracting ISO {Game.Name}... This may take a while.",
                                    NotificationType.Info);
                                    
                                var extractedPath = await isoHandler.ExtractAsync(info.SourceFullPath, tempPath, _watcherToken.Token);
                                
                                if (!string.IsNullOrEmpty(extractedPath) && File.Exists(extractedPath))
                                {
                                    _logger.Info($"Running installer from ISO: {extractedPath}");
                                    installSuccess = await RunInstallerExecutableAsync(extractedPath, gameInstallDir, _watcherToken.Token);
                                }
                                else if (!string.IsNullOrEmpty(extractedPath) && Directory.Exists(extractedPath))
                                {
                                    var installerPath = FindInstallerInDirectory(extractedPath);
                                    if (!string.IsNullOrEmpty(installerPath))
                                    {
                                        _logger.Info($"Running installer from ISO: {installerPath}");
                                        installSuccess = await RunInstallerExecutableAsync(installerPath, gameInstallDir, _watcherToken.Token);
                                    }
                                    else
                                    {
                                        // No installer found, just copy the ISO contents
                                        _logger.Info($"No installer found in ISO, copying contents to {gameInstallDir}");
                                        CopyDirectory(extractedPath, gameInstallDir);
                                        installSuccess = true;
                                    }
                                }
                            }
                            finally
                            {
                                // Clean up the temp directory
                                try
                                {
                                    if (Directory.Exists(tempPath))
                                        Directory.Delete(tempPath, true);
                                }
                                catch (Exception ex)
                                {
                                    _logger.Error(ex, $"Error cleaning up temp directory: {tempPath}");
                                }
                            }
                        }
                        else
                        {
                            _logger.Warn($"Cannot handle ISO file: {info.SourceFullPath}");
                            installSuccess = false;
                        }
                    }
                    
                    if (!installSuccess)
                    {
                        SafelyAddNotification(
                            Game.GameId, 
                            $"Failed to install {Game.Name}. The installer did not complete successfully.", 
                            NotificationType.Error);
                        Game.IsInstalling = false;
                        return;
                    }
                    
                    // Only find game executable if not already manually set by user
                    string executablePath;
                    
                    if (info.IsExecutablePathManuallySet && !string.IsNullOrEmpty(info.ExecutablePath) && File.Exists(info.ExecutablePath))
                    {
                        // Keep the manually selected executable
                        executablePath = info.ExecutablePath;
                        _logger.Info($"Using manually selected executable: {executablePath}");
                        
                        SafelyAddNotification(
                            Guid.NewGuid().ToString(),
                            $"Using custom executable for {Game.Name}: {Path.GetFileName(executablePath)}",
                            NotificationType.Info);
                    }
                    else
                    {
                        // Auto-detect the executable
                        executablePath = FindGameExecutable(gameInstallDir);
                        
                        if (string.IsNullOrEmpty(executablePath))
                        {
                            SafelyAddNotification(
                                Game.GameId, 
                                $"Game {Game.Name} was installed, but no executable could be found.", 
                                NotificationType.Warning);
                                
                            // If the prompt setting is enabled, ask user to locate the installed executable
                            if (_emuLibrary.Settings.PromptForInstallLocation)
                            {
                                // We need to use the UI thread for this
                                if (_emuLibrary?.Playnite?.MainView?.UIDispatcher != null)
                                {
                                    string selectedExe = null;
                                    
                                    _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                                    {
                                        SafelyAddNotification(
                                            Guid.NewGuid().ToString(),
                                            $"Please select the executable for {Game.Name}...",
                                            NotificationType.Info);
                                        
                                        // First ask the user to provide the installation directory if we don't have one
                                        string installDir = gameInstallDir;
                                        if (!Directory.Exists(gameInstallDir) || Directory.GetFiles(gameInstallDir, "*.*", SearchOption.AllDirectories).Length == 0)
                                        {
                                            installDir = _emuLibrary.Playnite.Dialogs.SelectFolder(
                                                $"Please select the folder where {Game.Name} was installed");
                                                
                                            // Update the installation directory if user selected one
                                            if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
                                            {
                                                gameInstallDir = installDir;
                                                info.InstallDirectory = installDir;
                                            }
                                        }
                                        
                                        // Then ask for the specific executable 
                                        if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
                                        {
                                            selectedExe = _emuLibrary.Playnite.Dialogs.SelectFile(
                                                "Select Game Executable (*.exe)|*.exe", 
                                                installDir);
                                        }
                                    });
                                    
                                    if (!string.IsNullOrEmpty(selectedExe))
                                    {
                                        executablePath = selectedExe;
                                        info.IsExecutablePathManuallySet = true;
                                        
                                        _logger.Info($"User selected executable: {executablePath}");
                                        
                                        SafelyAddNotification(
                                            Guid.NewGuid().ToString(),
                                            $"Using custom executable for {Game.Name}: {Path.GetFileName(executablePath)}",
                                            NotificationType.Info);
                                    }
                                    else
                                    {
                                        SafelyAddNotification(
                                            Guid.NewGuid().ToString(),
                                            $"No executable selected. Right-click the game and select 'Select Custom Executable' to set it manually.", 
                                            NotificationType.Warning);
                                    }
                                }
                            }
                            else
                            {
                                SafelyAddNotification(
                                    Guid.NewGuid().ToString(),
                                    $"Right-click the game and select 'Select Custom Executable' to set it manually.", 
                                    NotificationType.Info);
                            }
                        }
                        
                        // Reset the manual flag as we're auto-detecting
                        info.IsExecutablePathManuallySet = false;
                    }
                    
                    // Update the game info
                    info.InstallDirectory = gameInstallDir;
                    info.ExecutablePath = executablePath;
                    
                    // Notify user about executable detection
                    if (!string.IsNullOrEmpty(executablePath))
                    {
                        SafelyAddNotification(
                            Guid.NewGuid().ToString(),
                            $"Detected game executable: {Path.GetFileName(executablePath)}",
                            NotificationType.Info);
                    }
                    
                    // Adjust paths for portable mode if needed
                    string adjustedInstallDir = gameInstallDir;
                    string adjustedExePath = executablePath;
                    
                    if (_emuLibrary.Playnite.ApplicationInfo.IsPortable)
                    {
                        adjustedInstallDir = adjustedInstallDir.Replace(
                            _emuLibrary.Playnite.Paths.ApplicationPath, 
                            ExpandableVariables.PlayniteDirectory);
                            
                        if (!string.IsNullOrEmpty(executablePath))
                        {
                            adjustedExePath = adjustedExePath.Replace(
                                _emuLibrary.Playnite.Paths.ApplicationPath, 
                                ExpandableVariables.PlayniteDirectory);
                        }
                    }
                    
                    // Update the game in Playnite
                    var installData = new GameInstallationData()
                    {
                        InstallDirectory = adjustedInstallDir,
                        Roms = !string.IsNullOrEmpty(adjustedExePath) 
                            ? new List<GameRom>() { new GameRom(Game.Name, adjustedExePath) }
                            : new List<GameRom>()
                    };
                    
                    // Explicitly mark as installed
                    Game.IsInstalled = true;
                    
                    // Invoke the installed event
                    InvokeOnInstalled(new GameInstalledEventArgs(installData));
                    
                    // Update the GameId with the updated PcInstallerGameInfo
                    Game.GameId = info.AsGameId();
                    
                    // Make sure to run this on the UI thread
                    if (_emuLibrary?.Playnite?.MainView?.UIDispatcher != null)
                    {
                        _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                        {
                            _emuLibrary.Playnite.Database.Games.Update(Game);
                        });
                    }
                    else
                    {
                        _emuLibrary.Playnite.Database.Games.Update(Game);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Error installing game {Game.Name}");
                    SafelyAddNotification(
                        Game.GameId, 
                        $"Failed to install {Game.Name}.{Environment.NewLine}{Environment.NewLine}{ex.Message}", 
                        NotificationType.Error);
                    Game.IsInstalling = false;
                }
            });
        }
        
        private async Task<bool> RunInstallerExecutableAsync(string installerPath, string destDir, CancellationToken ct)
        {
            _logger.Info($"Running installer: {installerPath} with destination: {destDir}");
            
            try
            {
                // Detect installer type and use appropriate silent parameters
                string arguments = DetermineInstallerParameters(installerPath, destDir);
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = installerPath,
                        Arguments = arguments,
                        UseShellExecute = true
                    }
                };
                
                SafelyAddNotification(
                    Guid.NewGuid().ToString(),
                    $"Installing {Game.Name}... This may take a while. Please wait for the installation to complete.",
                    NotificationType.Info);
                    
                // Add more detailed installation information
                SafelyAddNotification(
                    Guid.NewGuid().ToString(),
                    $"Running installer: {Path.GetFileName(installerPath)}\nDestination: {destDir}\n" +
                    $"Note: If installation windows appear, you may need to interact with them.",
                    NotificationType.Info);
                    
                process.Start();
                
                // Wait for the process to exit with cancellation support
                var tcs = new TaskCompletionSource<bool>();
                process.EnableRaisingEvents = true;
                process.Exited += (sender, args) => tcs.TrySetResult(true);
                
                using (ct.Register(() => 
                {
                    if (!process.HasExited)
                    {
                        try { process.Kill(); } catch { }
                    }
                    tcs.TrySetCanceled();
                }))
                {
                    // Use a background thread to wait for process exit (compatible with .NET 4.6.2)
                    await Task.Run(() => 
                    {
                        if (!process.WaitForExit(600000)) // 10 minute timeout
                        {
                            try { process.Kill(); } catch { }
                            tcs.TrySetException(new TimeoutException("Installer process timed out after 10 minutes"));
                        }
                        else if (!tcs.Task.IsCompleted)
                        {
                            tcs.TrySetResult(true);
                        }
                    }, ct);
                    
                    // Wait for the task to complete with proper exception handling
                    bool isCancelled = false;
                    try
                    {
                        await tcs.Task;
                    }
                    catch (TaskCanceledException)
                    {
                        _logger.Info("Installation was cancelled by user");
                        isCancelled = true;
                    }
                    
                    // If cancelled, throw after all resources are cleaned up
                    if (isCancelled)
                    {
                        throw new OperationCanceledException("Installation was cancelled");
                    }
                    await tcs.Task;
                }
                
                _logger.Info($"Installer exited with code: {process.ExitCode}");
                
                // Check if the destination directory contains files
                if (Directory.Exists(destDir) && 
                    Directory.GetFiles(destDir, "*.*", SearchOption.AllDirectories).Length > 0)
                {
                    return true;
                }
                
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error running installer");
                return false;
            }
        }
        
        private string DetermineInstallerParameters(string installerPath, string destDir)
        {
            try
            {
                string extension = Path.GetExtension(installerPath).ToLower();
                string filename = Path.GetFileName(installerPath).ToLower();
                
                // Check for known installer types based on file properties or name
                if (extension == ".msi")
                {
                    // MSI silent parameters
                    return $"/quiet /qn TARGETDIR=\"{destDir}\"";
                }
                
                if (extension == ".exe")
                {
                    try
                    {
                        var versionInfo = FileVersionInfo.GetVersionInfo(installerPath);
                        
                        // InnoSetup installers
                        if (versionInfo.FileDescription?.Contains("Inno Setup") == true ||
                            Regex.IsMatch(filename, @"(inno|is).*setup", RegexOptions.IgnoreCase))
                        {
                            return $"/VERYSILENT /SP- /SUPPRESSMSGBOXES /DIR=\"{destDir}\" /NOICONS /NORESTART";
                        }
                        
                        // NSIS installers
                        if (versionInfo.FileDescription?.Contains("Nullsoft") == true ||
                            Regex.IsMatch(filename, @"nsis", RegexOptions.IgnoreCase))
                        {
                            return $"/S /D={destDir}";
                        }
                        
                        // InstallShield installers
                        if (versionInfo.FileDescription?.Contains("InstallShield") == true)
                        {
                            return $"/s /v\"/qn INSTALLDIR=\"{destDir}\"\"";
                        }
                    }
                    catch
                    {
                        // Ignore file property reading errors
                    }
                    
                    // Try to guess based on filename patterns
                    if (Regex.IsMatch(filename, @"inno|isetup", RegexOptions.IgnoreCase))
                    {
                        // Assume InnoSetup
                        return $"/VERYSILENT /SP- /SUPPRESSMSGBOXES /DIR=\"{destDir}\" /NOICONS /NORESTART";
                    }
                    
                    if (Regex.IsMatch(filename, @"nsis", RegexOptions.IgnoreCase))
                    {
                        // Assume NSIS
                        return $"/S /D={destDir}";
                    }
                }
                
                // Default to the most common parameter format (InnoSetup-like)
                return $"/VERYSILENT /SP- /SUPPRESSMSGBOXES /DIR=\"{destDir}\" /NOICONS /NORESTART";
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error determining installer parameters");
                return $"/VERYSILENT /SP- /SUPPRESSMSGBOXES /DIR=\"{destDir}\"";
            }
        }
        
        private string FindGameExecutable(string installDir)
        {
            try
            {
                if (!Directory.Exists(installDir))
                    return null;
                    
                _logger.Info($"Searching for game executable in: {installDir}");
                
                // Get all executables in the installation directory
                var allExes = Directory.GetFiles(installDir, "*.exe", SearchOption.AllDirectories)
                    .ToList();
                    
                if (allExes.Count == 0)
                    return null;
                    
                // Exclude common non-game executables
                var filteredExes = allExes.Where(path => 
                {
                    var filename = Path.GetFileNameWithoutExtension(path).ToLower();
                    return !filename.Contains("unins") && 
                           !filename.Contains("uninst") && 
                           !filename.Contains("setup") && 
                           !filename.Contains("update") &&
                           !filename.Contains("redist") &&
                           !filename.Contains("dotnet") &&
                           !filename.Contains("vcredist") &&
                           !filename.Contains("dxsetup");
                }).ToList();
                
                if (filteredExes.Count == 0)
                    return null;
                    
                // Look for common game executable names
                string[] commonGameExeNames = {
                    "game.exe", "play.exe", "launcher.exe", "start.exe", 
                    Game.Name.ToLower() + ".exe"
                };
                
                foreach (var exeName in commonGameExeNames)
                {
                    var matches = filteredExes.Where(path => 
                        Path.GetFileName(path).Equals(exeName, StringComparison.OrdinalIgnoreCase)).ToList();
                        
                    if (matches.Count > 0)
                        return matches[0];
                }
                
                // Look for executables containing the game name
                var nameMatches = filteredExes.Where(path => 
                    Path.GetFileNameWithoutExtension(path).ToLower()
                        .Contains(Game.Name.ToLower())).ToList();
                        
                if (nameMatches.Count > 0)
                    return nameMatches[0];
                    
                // Look for executables in "bin", "game", or "app" directories
                string[] priorityDirs = { "bin", "game", "app" };
                
                foreach (var dir in priorityDirs)
                {
                    var dirPath = Path.Combine(installDir, dir);
                    if (Directory.Exists(dirPath))
                    {
                        var dirExes = filteredExes.Where(path => path.StartsWith(dirPath)).ToList();
                        if (dirExes.Count > 0)
                            return dirExes.OrderByDescending(f => new FileInfo(f).Length).First();
                    }
                }
                
                // As a last resort, return the largest executable
                return filteredExes.OrderByDescending(f => new FileInfo(f).Length).First();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error finding game executable");
                return null;
            }
        }
        
        private string FindInstallerInDirectory(string directory)
        {
            try
            {
                if (!Directory.Exists(directory))
                    return null;
                    
                // Common installer names
                string[] commonInstallerNames = {
                    "setup.exe", "install.exe", "autorun.exe", "start.exe",
                    "setup.msi", "install.msi", "installer.msi"
                };
                
                // First look for common installer names at the root
                foreach (var installerName in commonInstallerNames)
                {
                    string path = Path.Combine(directory, installerName);
                    if (File.Exists(path))
                        return path;
                }
                
                // Look in common installer directories
                string[] commonDirs = { "setup", "install", "bin" };
                foreach (var dir in commonDirs)
                {
                    string dirPath = Path.Combine(directory, dir);
                    if (Directory.Exists(dirPath))
                    {
                        foreach (var installerName in commonInstallerNames)
                        {
                            string path = Path.Combine(dirPath, installerName);
                            if (File.Exists(path))
                                return path;
                        }
                    }
                }
                
                // Look for any .exe that might be an installer
                var exeFiles = Directory.GetFiles(directory, "*.exe", SearchOption.AllDirectories)
                    .Where(path => !Path.GetFileName(path).ToLower().Contains("unins"))
                    .ToList();
                
                if (exeFiles.Count == 0)
                    return null;
                
                // If we have multiple options and the setting is enabled, ask the user to choose
                if (exeFiles.Count > 1 && _emuLibrary.Settings.PromptForInstallerSelection)
                {
                    // Create a list of options with relative paths for cleaner display
                    var options = exeFiles
                        .Select(f => 
                        {
                            var relativePath = f.Replace(directory, "").TrimStart('\\', '/');
                            return new { DisplayName = relativePath, FullPath = f };
                        })
                        .ToList();
                        
                    // Sort the options - prioritize likely installer names
                    options = options.OrderBy(o => {
                        var name = Path.GetFileName(o.FullPath).ToLower();
                        if (name.Contains("setup") || name.Contains("install"))
                            return 0; // Highest priority
                        else if (name.Contains("start") || name.Contains("run"))
                            return 1; // Medium priority
                        else
                            return 2; // Lowest priority
                    }).ThenBy(o => o.DisplayName.Length).ToList();
                    
                    // Create a dictionary for the selection dialog
                    var selectionOptions = options.ToDictionary(
                        o => o.DisplayName,
                        o => o.FullPath
                    );
                    
                    // Show selection dialog
                    SafelyAddNotification(
                        Guid.NewGuid().ToString(),
                        $"Multiple potential installers found for {Game.Name}. Please select which one to use.",
                        NotificationType.Info);
                        
                    // We need to use the UI thread for this
                    string selectedOption = null;
                    if (_emuLibrary?.Playnite?.MainView?.UIDispatcher != null)
                    {
                        _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
                        {
                            selectedOption = _emuLibrary.Playnite.Dialogs.SelectString(
                                "Multiple potential installers found. Please select which one to use:",
                                "Select Installer",
                                selectionOptions.Keys.ToList());
                        });
                    }
                    
                    if (!string.IsNullOrEmpty(selectedOption) && selectionOptions.TryGetValue(selectedOption, out string selectedPath))
                    {
                        _logger.Info($"User selected installer: {selectedPath}");
                        return selectedPath;
                    }
                }
                
                // Filter for likely installers if we have multiple options
                var possibleInstallers = exeFiles.Where(path => {
                    var fileName = Path.GetFileName(path).ToLower();
                    return (fileName.Contains("setup") || 
                            fileName.Contains("install") || 
                            fileName.Contains("start")) && 
                           !fileName.Contains("unins");
                }).ToList();
                
                if (possibleInstallers.Count > 0)
                {
                    _logger.Info($"Found installer by name pattern: {possibleInstallers[0]}");
                    return possibleInstallers[0]; // Using indexer is safer than First() in 4.6.2
                }
                
                // If all else fails, return the first .exe file
                _logger.Info($"No specific installer found, using first executable: {exeFiles[0]}");
                return exeFiles[0];
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error finding installer in directory: {directory}");
                return null;
            }
        }
        
        private void CopyDirectory(string sourceDir, string destDir)
        {
            try
            {
                // Create the destination directory if it doesn't exist
                Directory.CreateDirectory(destDir);
                
                // Copy all files
                foreach (string filePath in Directory.GetFiles(sourceDir))
                {
                    try
                    {
                        string fileName = Path.GetFileName(filePath);
                        string destPath = Path.Combine(destDir, fileName);
                        
                        // Make sure destination directory exists (in case path is too long)
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                        
                        File.Copy(filePath, destPath, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Error copying file {filePath}");
                        // Continue with other files
                    }
                }
                
                // Copy all subdirectories
                foreach (string dirPath in Directory.GetDirectories(sourceDir))
                {
                    try
                    {
                        string dirName = Path.GetFileName(dirPath);
                        string destPath = Path.Combine(destDir, dirName);
                        
                        // Skip likely system or hidden directories
                        if (dirName.StartsWith(".") || dirName.Equals("$RECYCLE.BIN") || 
                            dirName.Equals("System Volume Information"))
                        {
                            continue;
                        }
                        
                        CopyDirectory(dirPath, destPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Error copying directory {dirPath}");
                        // Continue with other directories
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error in CopyDirectory from {sourceDir} to {destDir}");
                throw;
            }
        }
    }
}