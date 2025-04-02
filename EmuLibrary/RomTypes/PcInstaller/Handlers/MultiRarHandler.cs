using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EmuLibrary.RomTypes.PcInstaller.Handlers
{
    /// <summary>
    /// Handler for multi-part RAR archives that may contain ISO files
    /// </summary>
    public class MultiRarHandler : IArchiveHandler
    {
        private readonly ILogger _logger;
        private const string RAR_EXE_PATH = @"Tools\UnRAR.exe";
        
        // Regex pattern for part files (e.g., .part1.rar, .part01.rar, etc.)
        private static readonly Regex PartPattern = new Regex(@"\.part(\d+)\.rar$", RegexOptions.IgnoreCase);
        
        // Regex pattern for old-style RAR splits (e.g., .r00, .r01, etc.)
        private static readonly Regex OldSplitPattern = new Regex(@"\.r(\d+)$", RegexOptions.IgnoreCase);

        public MultiRarHandler(ILogger logger)
        {
            _logger = logger;
        }

        public bool CanHandle(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            string fileName = Path.GetFileName(filePath).ToLowerInvariant();

            // Check if it's a RAR file
            if (extension == ".rar")
                return true;

            // Check if it's a RAR split file (.r00, .r01, etc.)
            if (OldSplitPattern.IsMatch(fileName))
            {
                // Check if the corresponding .rar file exists
                string baseName = Path.Combine(
                    Path.GetDirectoryName(filePath),
                    Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(fileName)) + ".rar");
                
                return File.Exists(baseName);
            }

            return false;
        }

        public List<string> ListContents(string archivePath)
        {
            try
            {
                string rarPath = GetMainRarFile(archivePath);
                if (string.IsNullOrEmpty(rarPath))
                {
                    _logger.Error($"Failed to find main RAR file for {archivePath}");
                    return new List<string>();
                }

                var result = new List<string>();
                var unrarPath = GetUnrarPath();
                
                if (string.IsNullOrEmpty(unrarPath))
                {
                    _logger.Error("UnRAR.exe not found");
                    return result;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = unrarPath,
                    Arguments = $"lb \"{rarPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    while (!process.StandardOutput.EndOfStream)
                    {
                        var line = process.StandardOutput.ReadLine();
                        if (!string.IsNullOrEmpty(line))
                        {
                            result.Add(line.Trim());
                        }
                    }

                    process.WaitForExit();
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error listing contents of RAR archive: {archivePath}");
                return new List<string>();
            }
        }

        public async Task<string> ExtractAsync(string archivePath, string destinationPath, CancellationToken cancellationToken)
        {
            try
            {
                string rarPath = GetMainRarFile(archivePath);
                if (string.IsNullOrEmpty(rarPath))
                {
                    _logger.Error($"Failed to find main RAR file for {archivePath}");
                    return null;
                }

                Directory.CreateDirectory(destinationPath);
                var unrarPath = GetUnrarPath();
                
                if (string.IsNullOrEmpty(unrarPath))
                {
                    _logger.Error("UnRAR.exe not found");
                    return null;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = unrarPath,
                    Arguments = $"x -o+ \"{rarPath}\" \"{destinationPath}\"",
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
                        _logger.Error($"UnRAR exited with code {process.ExitCode}");
                        return null;
                    }
                }

                // Look for ISO files in the extracted contents
                var isoFiles = Directory.GetFiles(destinationPath, "*.iso", SearchOption.AllDirectories);
                if (isoFiles.Length > 0)
                {
                    _logger.Info($"Found {isoFiles.Length} ISO file(s) in the extracted archive");
                    return isoFiles[0]; // Return the first ISO file
                }

                return destinationPath;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error extracting RAR archive: {archivePath}");
                return null;
            }
        }

        public async Task<string> ExtractFileAsync(string archivePath, string fileToExtract, string destinationPath, CancellationToken cancellationToken)
        {
            try
            {
                string rarPath = GetMainRarFile(archivePath);
                if (string.IsNullOrEmpty(rarPath))
                {
                    _logger.Error($"Failed to find main RAR file for {archivePath}");
                    return null;
                }

                Directory.CreateDirectory(destinationPath);
                var unrarPath = GetUnrarPath();
                
                if (string.IsNullOrEmpty(unrarPath))
                {
                    _logger.Error("UnRAR.exe not found");
                    return null;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = unrarPath,
                    Arguments = $"e -o+ \"{rarPath}\" \"{fileToExtract}\" \"{destinationPath}\"",
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
                        _logger.Error($"UnRAR exited with code {process.ExitCode}");
                        return null;
                    }
                }

                string extractedFilePath = Path.Combine(destinationPath, Path.GetFileName(fileToExtract));
                return File.Exists(extractedFilePath) ? extractedFilePath : null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error extracting file from RAR archive: {archivePath}");
                return null;
            }
        }

        public string GetArchiveDisplayName(string archivePath)
        {
            string mainRarPath = GetMainRarFile(archivePath);
            if (string.IsNullOrEmpty(mainRarPath))
                return Path.GetFileNameWithoutExtension(archivePath);

            string fileName = Path.GetFileNameWithoutExtension(mainRarPath);
            
            // Remove .part1 or other part indicators
            fileName = PartPattern.Replace(fileName, string.Empty);
            
            // Clean up common patterns in archive names
            fileName = fileName.Replace(".", " ")
                              .Replace("_", " ")
                              .Replace("-", " ");
            
            // Title case the name
            var textInfo = new System.Globalization.CultureInfo("en-US", false).TextInfo;
            return textInfo.ToTitleCase(fileName.ToLower());
        }

        public ulong GetExpectedInstallSize(string archivePath)
        {
            try
            {
                string rarPath = GetMainRarFile(archivePath);
                if (string.IsNullOrEmpty(rarPath))
                    return 0;

                var unrarPath = GetUnrarPath();
                if (string.IsNullOrEmpty(unrarPath))
                    return 0;

                var startInfo = new ProcessStartInfo
                {
                    FileName = unrarPath,
                    Arguments = $"l \"{rarPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                ulong totalSize = 0;
                using (var process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    // Parse the output to find the total size
                    var sizeLines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Where(line => line.Contains("Size") && line.Contains("Name"));
                    
                    foreach (var line in sizeLines)
                    {
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1 && ulong.TryParse(parts[0], out ulong size))
                        {
                            totalSize += size;
                        }
                    }
                }

                return totalSize;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error getting expected size of RAR archive: {archivePath}");
                return 0;
            }
        }

        #region Helper Methods

        private string GetMainRarFile(string rarPath)
        {
            if (string.IsNullOrEmpty(rarPath))
                return null;

            string extension = Path.GetExtension(rarPath).ToLowerInvariant();
            string fileName = Path.GetFileName(rarPath).ToLowerInvariant();
            string directory = Path.GetDirectoryName(rarPath);

            // If it's already a main .rar file, return it
            if (extension == ".rar" && !PartPattern.IsMatch(fileName))
                return rarPath;

            // If it's a part file, find the first part
            if (extension == ".rar" && PartPattern.IsMatch(fileName))
            {
                string baseName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(fileName));
                string part1Path = Path.Combine(directory, $"{baseName}.part1.rar");
                string part01Path = Path.Combine(directory, $"{baseName}.part01.rar");

                if (File.Exists(part1Path))
                    return part1Path;
                if (File.Exists(part01Path))
                    return part01Path;
            }

            // If it's an old-style split file (.r00, .r01, etc.), find the .rar file
            if (OldSplitPattern.IsMatch(fileName))
            {
                string baseName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(fileName));
                string rarFilePath = Path.Combine(directory, $"{baseName}.rar");

                if (File.Exists(rarFilePath))
                    return rarFilePath;
            }

            return rarPath; // Return the original path if we couldn't find the main file
        }

        private string GetUnrarPath()
        {
            try
            {
                // Get the directory where the plugin is installed
                string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string pluginDirectory = Path.GetDirectoryName(assemblyLocation);
                
                // First check if UnRAR.exe exists in expected location
                string unrarPath = Path.Combine(pluginDirectory, RAR_EXE_PATH);
                
                if (File.Exists(unrarPath))
                {
                    return unrarPath;
                }
                
                // Fallback options - check in different locations
                string toolsDir = Path.Combine(pluginDirectory, "Tools");
                if (Directory.Exists(toolsDir))
                {
                    string fallbackPath = Path.Combine(toolsDir, "UnRAR.exe");
                    if (File.Exists(fallbackPath))
                    {
                        return fallbackPath;
                    }
                }
                
                // Check in parent directory's Tools folder
                string parentToolsDir = Path.Combine(Directory.GetParent(pluginDirectory).FullName, "Tools");
                if (Directory.Exists(parentToolsDir))
                {
                    string fallbackPath = Path.Combine(parentToolsDir, "UnRAR.exe");
                    if (File.Exists(fallbackPath))
                    {
                        return fallbackPath;
                    }
                }
                
                _logger.Error("UnRAR.exe not found. Please download and place in the Tools directory.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error finding UnRAR.exe");
                return null;
            }
        }

        #endregion
    }
}