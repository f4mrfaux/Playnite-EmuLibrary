using EmuLibrary.Util.FileCopier;
using Playnite.SDK;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EmuLibrary.Util.AssetImporter
{
    /// <summary>
    /// Simple asset importer that copies network files to local temp storage before processing
    /// </summary>
    public class AssetImporter : IAssetImporter
    {
        private readonly ILogger _logger;
        private readonly IPlayniteAPI _playnite;
        
        public AssetImporter(ILogger logger, IPlayniteAPI playnite)
        {
            _logger = logger;
            _playnite = playnite;
        }

        /// <summary>
        /// Imports a source file/directory to local temp storage
        /// </summary>
        /// <param name="sourcePath">Path to the network source file/directory</param>
        /// <param name="showProgress">Whether to show Windows copy dialog</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Path to the local temp copy</returns>
        public async Task<string> ImportToLocalAsync(string sourcePath, bool showProgress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                throw new ArgumentException("Source path cannot be null or empty", nameof(sourcePath));
            }

            try
            {
                _logger.Info($"Importing asset from {sourcePath} to local temp storage");
                
                // Create a unique temp directory for this import
                string tempDir = Path.Combine(Path.GetTempPath(), "EmuLibrary_Assets", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);
                
                FileSystemInfo source;
                if (Directory.Exists(sourcePath))
                {
                    source = new DirectoryInfo(sourcePath);
                }
                else if (File.Exists(sourcePath))
                {
                    source = new FileInfo(sourcePath);
                }
                else
                {
                    throw new FileNotFoundException($"Source path not found: {sourcePath}");
                }
                
                DirectoryInfo destination = new DirectoryInfo(tempDir);
                
                // Use Windows File Copier to show progress dialog if requested
                IFileCopier copier = showProgress 
                    ? new WindowsFileCopier(source, destination)
                    : new SimpleFileCopier(source, destination);
                
                await copier.CopyAsync(cancellationToken);
                
                string resultPath;
                if (source is FileInfo)
                {
                    // For files, return the path to the copied file
                    resultPath = Path.Combine(tempDir, Path.GetFileName(sourcePath));
                }
                else
                {
                    // For directories, return the temp directory path
                    resultPath = tempDir;
                }
                
                _logger.Info($"Successfully imported asset to {resultPath}");
                return resultPath;
            }
            catch (WindowsCopyDialogClosedException)
            {
                _logger.Warn("Asset import cancelled by user");
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.Warn("Asset import cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to import asset: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Cleans up the temp directory after processing
        /// </summary>
        /// <param name="tempPath">Path to the temp directory</param>
        public void CleanupTempDirectory(string tempPath)
        {
            try
            {
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, true);
                    _logger.Info($"Cleaned up temp directory: {tempPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to clean up temp directory {tempPath}: {ex.Message}");
            }
        }
    }
}