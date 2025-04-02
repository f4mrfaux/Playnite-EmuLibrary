using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EmuLibrary.RomTypes.PcInstaller.Handlers
{
    /// <summary>
    /// Handler for ISO files containing game installers
    /// </summary>
    public class IsoHandler : IArchiveHandler
    {
        private readonly ILogger _logger;
        private const string SEVEN_ZIP_EXE_PATH = @"Tools\7z.exe";
        
        // Common installer names found in ISO files
        private static readonly string[] CommonInstallerNames = new[]
        {
            "setup.exe", "install.exe", "autorun.exe", "start.exe",
            "setup.msi", "install.msi", "installer.msi",
            "setup.bat", "install.bat", "autorun.bat", "start.bat"
        };

        // Common installer directories in ISO files
        private static readonly string[] CommonInstallerDirs = new[]
        {
            "setup", "install", "bin", "game", "app"
        };

        public IsoHandler(ILogger logger)
        {
            _logger = logger;
        }

        public bool CanHandle(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension == ".iso";
        }

        public List<string> ListContents(string isoPath)
        {
            try
            {
                var result = new List<string>();
                var sevenZipPath = Get7ZipPath();
                
                if (string.IsNullOrEmpty(sevenZipPath))
                {
                    _logger.Error("7z.exe not found");
                    return result;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = sevenZipPath,
                    Arguments = $"l \"{isoPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    // Parse the output to extract file paths
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    var fileLines = lines.Skip(lines.Length > 20 ? 20 : 0)
                                        .Where(line => !string.IsNullOrWhiteSpace(line) && 
                                               !line.Trim().StartsWith("--") && 
                                               !line.Trim().StartsWith("Date"));

                    foreach (var line in fileLines)
                    {
                        var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 5)
                        {
                            var filePath = string.Join(" ", parts.Skip(5));
                            if (!string.IsNullOrEmpty(filePath))
                            {
                                result.Add(filePath);
                            }
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error listing contents of ISO file: {isoPath}");
                return new List<string>();
            }
        }

        public async Task<string> ExtractAsync(string isoPath, string destinationPath, CancellationToken cancellationToken)
        {
            try
            {
                Directory.CreateDirectory(destinationPath);
                var sevenZipPath = Get7ZipPath();
                
                if (string.IsNullOrEmpty(sevenZipPath))
                {
                    _logger.Error("7z.exe not found");
                    return null;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = sevenZipPath,
                    Arguments = $"x \"{isoPath}\" -o\"{destinationPath}\" -y",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    // Register the cancellation token
                    cancellationToken.Register(() =>
                    {
                        try
                        {
                            if (!process.HasExited)
                                process.Kill();
                        }
                        catch { /* Ignore errors during cancellation */ }
                    });

                    // Compatible with .NET Framework 4.6.2
                    await Task.Run(() => process.WaitForExit(), cancellationToken);

                    if (process.ExitCode != 0)
                    {
                        _logger.Error($"7-Zip exited with code {process.ExitCode}");
                        return null;
                    }
                }

                // Check if an installer was extracted
                var installerPath = FindInstaller(destinationPath);
                return !string.IsNullOrEmpty(installerPath) ? installerPath : destinationPath;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error extracting ISO file: {isoPath}");
                return null;
            }
        }

        public async Task<string> ExtractFileAsync(string isoPath, string fileToExtract, string destinationPath, CancellationToken cancellationToken)
        {
            try
            {
                Directory.CreateDirectory(destinationPath);
                var sevenZipPath = Get7ZipPath();
                
                if (string.IsNullOrEmpty(sevenZipPath))
                {
                    _logger.Error("7z.exe not found");
                    return null;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = sevenZipPath,
                    Arguments = $"e \"{isoPath}\" -o\"{destinationPath}\" \"{fileToExtract}\" -y",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    // Register the cancellation token
                    cancellationToken.Register(() =>
                    {
                        try
                        {
                            if (!process.HasExited)
                                process.Kill();
                        }
                        catch { /* Ignore errors during cancellation */ }
                    });

                    // Compatible with .NET Framework 4.6.2
                    await Task.Run(() => process.WaitForExit(), cancellationToken);

                    if (process.ExitCode != 0)
                    {
                        _logger.Error($"7-Zip exited with code {process.ExitCode}");
                        return null;
                    }
                }

                string extractedFilePath = Path.Combine(destinationPath, Path.GetFileName(fileToExtract));
                return File.Exists(extractedFilePath) ? extractedFilePath : null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error extracting file from ISO: {isoPath}");
                return null;
            }
        }

        public string GetArchiveDisplayName(string isoPath)
        {
            string fileName = Path.GetFileNameWithoutExtension(isoPath);
            
            // Clean up common patterns in ISO names
            fileName = fileName.Replace(".", " ")
                              .Replace("_", " ")
                              .Replace("-", " ");
            
            // Title case the name
            var textInfo = new System.Globalization.CultureInfo("en-US", false).TextInfo;
            return textInfo.ToTitleCase(fileName.ToLower());
        }

        public ulong GetExpectedInstallSize(string isoPath)
        {
            // Typical installation is 2-3 times the ISO size
            try
            {
                var fileInfo = new FileInfo(isoPath);
                return (ulong)(fileInfo.Length * 2.5);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error getting expected size of ISO file: {isoPath}");
                return 0;
            }
        }

        #region Helper Methods

        /// <summary>
        /// Finds an installer executable within the extracted ISO contents
        /// </summary>
        private string FindInstaller(string extractedPath)
        {
            try
            {
                if (!Directory.Exists(extractedPath))
                    return null;

                _logger.Info($"Looking for installer in {extractedPath}");
                
                // Collect all executables first to avoid multiple directory scans
                var allExeFiles = new List<string>();
                try
                {
                    allExeFiles = Directory.GetFiles(extractedPath, "*.exe", SearchOption.AllDirectories).ToList();
                    _logger.Info($"Found {allExeFiles.Count} .exe files in extracted content");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error enumerating .exe files, using fallback search method");
                }
                
                // MSI files as secondary option
                var allMsiFiles = new List<string>();
                try
                {
                    allMsiFiles = Directory.GetFiles(extractedPath, "*.msi", SearchOption.AllDirectories).ToList();
                    _logger.Info($"Found {allMsiFiles.Count} .msi files in extracted content");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error enumerating .msi files");
                }

                // First, check for common installer names at the root
                foreach (var installerName in CommonInstallerNames)
                {
                    string installerPath = Path.Combine(extractedPath, installerName);
                    if (File.Exists(installerPath))
                    {
                        _logger.Info($"Found common installer at root: {installerPath}");
                        return installerPath;
                    }
                }

                // Then check for common installer names in common installer directories
                foreach (var directory in CommonInstallerDirs)
                {
                    string dirPath = Path.Combine(extractedPath, directory);
                    if (Directory.Exists(dirPath))
                    {
                        foreach (var installerName in CommonInstallerNames)
                        {
                            string installerPath = Path.Combine(dirPath, installerName);
                            if (File.Exists(installerPath))
                            {
                                _logger.Info($"Found common installer in {directory} directory: {installerPath}");
                                return installerPath;
                            }
                        }
                    }
                }

                // If not found, look for any .exe file that might be an installer
                var potentialInstallers = allExeFiles.Where(path => 
                {
                    var fileName = Path.GetFileName(path).ToLower();
                    return (fileName.Contains("setup") || 
                            fileName.Contains("install") || 
                            fileName.Contains("start") ||
                            fileName.Contains("launch")) && 
                           !fileName.Contains("unins") &&
                           !fileName.Contains("uninst") &&
                           !fileName.Contains("update") &&
                           !fileName.Contains("redist") &&
                           !fileName.Contains("vcredist") &&
                           !fileName.Contains("directx") &&
                           !fileName.Contains("dxsetup");
                }).ToList();

                if (potentialInstallers.Count > 0)
                {
                    _logger.Info($"Found installer by name matching: {potentialInstallers[0]}");
                    return potentialInstallers[0]; // Using indexer is safer than FirstOrDefault() in 4.6.2
                }

                // Check if there's an autorun.inf file that might point to an installer
                string autorunPath = Path.Combine(extractedPath, "autorun.inf");
                if (File.Exists(autorunPath))
                {
                    try
                    {
                        var autorunLines = File.ReadAllLines(autorunPath);
                        var openLine = autorunLines.FirstOrDefault(line => line.StartsWith("open=", StringComparison.OrdinalIgnoreCase));
                        
                        if (!string.IsNullOrEmpty(openLine))
                        {
                            var openCommand = openLine.Substring(5).Trim();
                            var openPath = Path.Combine(extractedPath, openCommand);
                            
                            if (File.Exists(openPath))
                            {
                                _logger.Info($"Found installer via autorun.inf: {openPath}");
                                return openPath;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error reading autorun.inf");
                    }
                }

                // If no EXE installers found, try MSI files
                if (allMsiFiles.Count > 0)
                {
                    _logger.Info($"Falling back to MSI file: {allMsiFiles[0]}");
                    return allMsiFiles[0];
                }

                // If all else fails, just return the first .exe file found that's not an uninstaller
                var safeExeFiles = allExeFiles.Where(f => 
                    !Path.GetFileName(f).ToLower().Contains("unins") && 
                    !Path.GetFileName(f).ToLower().Contains("uninst")).ToList();
                    
                if (safeExeFiles.Count > 0)
                {
                    _logger.Info($"Last resort: using first non-uninstaller EXE: {safeExeFiles[0]}");
                    return safeExeFiles[0];
                }

                _logger.Warning("No suitable installer found in extracted content");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error finding installer in extracted ISO: {extractedPath}");
                return null;
            }
        }

        /// <summary>
        /// An alternative approach to mount the ISO file using Windows APIs (for future implementation)
        /// </summary>
        private async Task<string> MountIsoAsync(string isoPath, CancellationToken cancellationToken)
        {
            // This would use Windows APIs to mount the ISO file
            // For now, we'll just return null as this is not implemented yet
            _logger.Info("ISO mounting via Windows APIs is not implemented yet");
            return null;
        }

        private string Get7ZipPath()
        {
            try
            {
                // Get the directory where the plugin is installed
                string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string pluginDirectory = Path.GetDirectoryName(assemblyLocation);
                
                // First check if 7z.exe exists in expected location
                string sevenZipPath = Path.Combine(pluginDirectory, SEVEN_ZIP_EXE_PATH);
                
                if (File.Exists(sevenZipPath))
                {
                    return sevenZipPath;
                }
                
                // Fallback options - check in different locations
                string toolsDir = Path.Combine(pluginDirectory, "Tools");
                if (Directory.Exists(toolsDir))
                {
                    string fallbackPath = Path.Combine(toolsDir, "7z.exe");
                    if (File.Exists(fallbackPath))
                    {
                        return fallbackPath;
                    }
                }
                
                // Check in parent directory's Tools folder
                string parentToolsDir = Path.Combine(Directory.GetParent(pluginDirectory).FullName, "Tools");
                if (Directory.Exists(parentToolsDir))
                {
                    string fallbackPath = Path.Combine(parentToolsDir, "7z.exe");
                    if (File.Exists(fallbackPath))
                    {
                        return fallbackPath;
                    }
                }
                
                // Check if 7z is in PATH - more defensive for 4.6.2
                string path7z = null;
                string pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathEnv))
                {
                    foreach (var p in pathEnv.Split(Path.PathSeparator))
                    {
                        if (!string.IsNullOrEmpty(p))
                        {
                            string testPath = Path.Combine(p, "7z.exe");
                            if (File.Exists(testPath))
                            {
                                path7z = testPath;
                                break;
                            }
                        }
                    }
                }
                    
                if (!string.IsNullOrEmpty(path7z))
                {
                    return path7z;
                }
                
                _logger.Error("7z.exe not found. Please download and place in the Tools directory.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error finding 7z.exe");
                return null;
            }
        }

        #endregion
    }
}