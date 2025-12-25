using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EmuLibrary.Util
{
    /// <summary>
    /// Utility class for extracting archive files using 7-Zip CLI.
    /// Supports ZIP, RAR, 7Z, and multi-part archives.
    /// </summary>
    public class ArchiveExtractor
    {
        private readonly ILogger _logger;
        private static readonly string[] SupportedExtensions = { ".zip", ".rar", ".7z", ".7zip" };
        private static readonly string[] MultiPartRarPatterns = { 
            ".part1.rar", ".part01.rar", ".part001.rar", 
            ".rar", ".r00", ".r01", ".r02" 
        };

        public ArchiveExtractor(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Checks if a file is a supported archive format.
        /// </summary>
        public static bool IsArchiveFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return SupportedExtensions.Contains(extension);
        }

        /// <summary>
        /// Checks if a file is part of a multi-part RAR archive.
        /// </summary>
        public static bool IsMultiPartRar(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            var fileName = Path.GetFileName(filePath).ToLowerInvariant();
            return MultiPartRarPatterns.Any(pattern => fileName.EndsWith(pattern));
        }

        /// <summary>
        /// Finds all parts of a multi-part RAR archive.
        /// </summary>
        public static List<string> FindMultiPartRarFiles(string firstPartPath)
        {
            var parts = new List<string>();
            if (string.IsNullOrEmpty(firstPartPath) || !File.Exists(firstPartPath))
                return parts;

            var directory = Path.GetDirectoryName(firstPartPath);
            var fileName = Path.GetFileNameWithoutExtension(firstPartPath);
            var extension = Path.GetExtension(firstPartPath).ToLowerInvariant();

            // Handle different naming patterns:
            // game.part1.rar, game.part2.rar, etc.
            // game.rar, game.r00, game.r01, etc.
            // game.part01.rar, game.part02.rar, etc.

            if (extension == ".rar")
            {
                // Check if it's a numbered part (part1.rar, part01.rar, etc.)
                if (fileName.EndsWith(".part1") || fileName.EndsWith(".part01") || fileName.EndsWith(".part001"))
                {
                    var baseName = fileName;
                    // Remove .part1, .part01, or .part001
                    baseName = System.Text.RegularExpressions.Regex.Replace(baseName, @"\.part\d+$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    
                    int partNum = 1;
                    while (true)
                    {
                        // Try different patterns
                        var partFile = Path.Combine(directory, $"{baseName}.part{partNum}.rar");
                        if (!File.Exists(partFile))
                            partFile = Path.Combine(directory, $"{baseName}.part{partNum:D2}.rar");
                        if (!File.Exists(partFile))
                            partFile = Path.Combine(directory, $"{baseName}.part{partNum:D3}.rar");
                        
                        if (File.Exists(partFile))
                        {
                            parts.Add(partFile);
                            partNum++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else
                {
                    // Standard multi-part RAR: game.rar, game.r00, game.r01, etc.
                    parts.Add(firstPartPath); // Add the .rar file itself
                    
                    int rNum = 0;
                    while (true)
                    {
                        var rFile = Path.Combine(directory, $"{fileName}.r{rNum:D2}");
                        if (File.Exists(rFile))
                        {
                            parts.Add(rFile);
                            rNum++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            return parts.OrderBy(p => p).ToList();
        }

        /// <summary>
        /// Finds the 7-Zip executable (7z.exe) in common installation locations.
        /// </summary>
        private string Find7ZipExecutable()
        {
            var commonPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7za.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7za.exe"),
                "7z.exe", // In PATH
                "7za.exe"  // In PATH
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    _logger.Debug($"Found 7-Zip at: {path}");
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts an archive file to a destination directory using 7-Zip CLI.
        /// </summary>
        /// <param name="archivePath">Path to the archive file</param>
        /// <param name="destinationDir">Directory to extract to</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="password">Optional password for protected archives</param>
        /// <returns>True if extraction succeeded, false otherwise</returns>
        public async Task<bool> ExtractArchiveAsync(
            string archivePath, 
            string destinationDir, 
            CancellationToken cancellationToken,
            string password = null)
        {
            if (string.IsNullOrEmpty(archivePath))
                throw new ArgumentException("Archive path cannot be null or empty", nameof(archivePath));
            if (string.IsNullOrEmpty(destinationDir))
                throw new ArgumentException("Destination directory cannot be null or empty", nameof(destinationDir));
            if (!File.Exists(archivePath))
                throw new FileNotFoundException($"Archive file not found: {archivePath}");

            var sevenZipPath = Find7ZipExecutable();
            if (string.IsNullOrEmpty(sevenZipPath))
            {
                _logger.Error("7-Zip executable (7z.exe) not found. Please install 7-Zip from https://www.7-zip.org/");
                throw new FileNotFoundException("7-Zip executable not found. Please install 7-Zip.");
            }

            // Ensure destination directory exists
            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            // Handle multi-part RAR files
            var archiveFiles = new List<string>();
            if (IsMultiPartRar(archivePath))
            {
                var parts = FindMultiPartRarFiles(archivePath);
                if (parts.Count > 1)
                {
                    _logger.Info($"Detected multi-part RAR archive with {parts.Count} parts");
                    archiveFiles.AddRange(parts);
                }
                else
                {
                    archiveFiles.Add(archivePath);
                }
            }
            else
            {
                archiveFiles.Add(archivePath);
            }

            // Use the first archive file for extraction (7-Zip will automatically find other parts)
            var mainArchive = archiveFiles[0];

            try
            {
                var arguments = new StringBuilder();
                arguments.Append("x "); // Extract with full paths
                arguments.Append($"\"{mainArchive}\" "); // Archive path
                arguments.Append($"-o\"{destinationDir}\" "); // Output directory
                arguments.Append("-y "); // Assume Yes on all queries
                arguments.Append("-aoa "); // Overwrite all existing files without prompt

                if (!string.IsNullOrEmpty(password))
                {
                    arguments.Append($"-p\"{password}\" "); // Password
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = sevenZipPath,
                    Arguments = arguments.ToString(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                _logger.Info($"Extracting archive: {Path.GetFileName(mainArchive)} to {destinationDir}");

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        _logger.Error("Failed to start 7-Zip process");
                        return false;
                    }

                    // Read output asynchronously
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    // Wait for process with cancellation support
                    await Task.Run(() =>
                    {
                        while (!process.HasExited)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                try
                                {
                                    process.Kill();
                                    _logger.Info("Archive extraction cancelled");
                                }
                                catch (Exception ex)
                                {
                                    _logger.Warn($"Error killing extraction process: {ex.Message}");
                                }
                                throw new OperationCanceledException("Archive extraction was cancelled", cancellationToken);
                            }
                            Thread.Sleep(100);
                        }
                        process.WaitForExit();
                    }, cancellationToken);

                    var output = await outputTask;
                    var error = await errorTask;

                    if (process.ExitCode == 0)
                    {
                        _logger.Info($"Successfully extracted archive to {destinationDir}");
                        return true;
                    }
                    else
                    {
                        _logger.Error($"7-Zip extraction failed with exit code {process.ExitCode}");
                        _logger.Error($"Error output: {error}");
                        if (!string.IsNullOrEmpty(output))
                        {
                            _logger.Debug($"Output: {output}");
                        }
                        return false;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Archive extraction was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error extracting archive: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Detects the type of content in an extracted archive.
        /// </summary>
        /// <param name="extractedDir">Directory containing extracted files</param>
        /// <returns>Content type information</returns>
        public static ExtractedContentInfo DetectContentType(string extractedDir)
        {
            var info = new ExtractedContentInfo();

            if (string.IsNullOrEmpty(extractedDir) || !Directory.Exists(extractedDir))
                return info;

            // Search for ISO files
            var isoFiles = Directory.GetFiles(extractedDir, "*.iso", SearchOption.AllDirectories);
            if (isoFiles.Length > 0)
            {
                info.HasIsoFiles = true;
                info.IsoFiles = isoFiles.ToList();
            }

            // Search for EXE files
            var exeFiles = Directory.GetFiles(extractedDir, "*.exe", SearchOption.AllDirectories);
            if (exeFiles.Length > 0)
            {
                info.HasExeFiles = true;
                info.ExeFiles = exeFiles.ToList();
            }

            // Determine primary content type
            if (info.HasIsoFiles && info.HasExeFiles)
            {
                info.PrimaryContentType = ContentType.Mixed;
            }
            else if (info.HasIsoFiles)
            {
                info.PrimaryContentType = ContentType.Iso;
            }
            else if (info.HasExeFiles)
            {
                info.PrimaryContentType = ContentType.Exe;
            }
            else
            {
                info.PrimaryContentType = ContentType.Unknown;
            }

            return info;
        }
    }

    /// <summary>
    /// Information about extracted archive content.
    /// </summary>
    public class ExtractedContentInfo
    {
        public bool HasIsoFiles { get; set; }
        public bool HasExeFiles { get; set; }
        public List<string> IsoFiles { get; set; } = new List<string>();
        public List<string> ExeFiles { get; set; } = new List<string>();
        public ContentType PrimaryContentType { get; set; } = ContentType.Unknown;
    }

    /// <summary>
    /// Type of content found in an extracted archive.
    /// </summary>
    public enum ContentType
    {
        Unknown,
        Iso,
        Exe,
        Mixed
    }
}

