using EmuLibrary.Settings;
using EmuLibrary.Util.FileCopier;
using Playnite.SDK;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace EmuLibrary.Util.AssetImporter
{
    /// <summary>
    /// Asset importer that copies network files to local temp storage before processing
    /// Enhanced with progress tracking, caching, and error handling
    /// </summary>
    public class AssetImporter : IAssetImporter
    {
        private static AssetImporter _instance;
        public static AssetImporter Instance => _instance;
        
        private readonly ILogger _logger;
        private readonly IPlayniteAPI _playnite;
        private readonly string _cachePath;
        private readonly ConcurrentDictionary<string, string> _cachedAssets = new ConcurrentDictionary<string, string>();
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);
        
        public event EventHandler<ImportProgressEventArgs> ImportProgress;
        
        public AssetImporter(ILogger logger, IPlayniteAPI playnite)
        {
            _logger = logger;
            _playnite = playnite;
            _cachePath = Path.Combine(Path.GetTempPath(), "EmuLibrary_AssetCache");
            
            // Ensure cache directory exists
            if (!Directory.Exists(_cachePath))
            {
                Directory.CreateDirectory(_cachePath);
            }
            
            // Load cache registry if it exists
            if (Settings.Settings.Instance.EnableAssetCaching)
            {
                LoadCacheRegistry();
            }
            
            _instance = this;
        }

        /// <summary>
        /// Imports a source file/directory to local temp storage with advanced features
        /// </summary>
        /// <param name="sourcePath">Path to the network source file/directory</param>
        /// <param name="showProgress">Whether to show Windows copy dialog</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Object with details about the imported asset</returns>
        public async Task<ImportResult> ImportAsync(string sourcePath, bool showProgress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                throw new ArgumentException("Source path cannot be null or empty", nameof(sourcePath));
            }
            
            // Create a unique identifier for this import
            string sourceKey = GenerateSourceKey(sourcePath);
            
            try
            {
                _logger.Info($"Importing asset from {sourcePath} to local temp storage");
                
                // Check cache if enabled
                if (Settings.Settings.Instance.EnableAssetCaching)
                {
                    string cachedPath = GetCachedAsset(sourceKey);
                    if (!string.IsNullOrEmpty(cachedPath) && (File.Exists(cachedPath) || Directory.Exists(cachedPath)))
                    {
                        _logger.Info($"Using cached asset for {sourcePath} at {cachedPath}");
                        long assetSize;
                if (File.Exists(cachedPath))
                    assetSize = new FileInfo(cachedPath).Length;
                else
                    assetSize = GetDirectorySize(new DirectoryInfo(cachedPath));

                return new ImportResult(
                            cachedPath,
                            true,
                            null,
                            assetSize,
                            true);
                    }
                }
                
                // Determine the root temp directory
                string baseTempDir;
                if (Settings.Settings.Instance.EnableAssetCaching)
                    baseTempDir = _cachePath;
                else
                    baseTempDir = Path.GetTempPath();
                
                // Create a unique temp directory for this import
                string tempDir = Path.Combine(baseTempDir, "EmuLibrary_Assets", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);
                
                // Check if source file/directory exists
                FileSystemInfo source;
                if (Directory.Exists(sourcePath))
                {
                    source = new DirectoryInfo(sourcePath);
                }
                else if (File.Exists(sourcePath))
                {
                    source = new FileInfo(sourcePath);
                    
                    // Show warning for large files if configured
                    if (source is FileInfo fileInfo && 
                        Settings.Settings.Instance.LargeFileSizeWarningThresholdMB > 0 &&
                        fileInfo.Length > Settings.Settings.Instance.LargeFileSizeWarningThresholdMB * 1024 * 1024)
                    {
                        bool proceed = true;
                        
                        // Use UI dispatcher to show dialog
                        _playnite.MainView.UIDispatcher.Invoke(() =>
                        {
                            proceed = _playnite.Dialogs.ShowMessage(
                                $"The file you are trying to import is large ({fileInfo.Length / (1024 * 1024)} MB). " +
                                $"Do you want to continue?",
                                "Large File Warning",
                                System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.Yes;
                        });
                        
                        if (!proceed)
                        {
                            _logger.Info($"User cancelled import of large file: {sourcePath}");
                            return new ImportResult(null, false, new OperationCanceledException("User cancelled large file import"), 0, false);
                        }
                    }
                }
                else
                {
                    throw new FileNotFoundException($"Source path not found: {sourcePath}");
                }
                
                DirectoryInfo destination = new DirectoryInfo(tempDir);
                string resultPath = null;
                
                // Implement retry logic for network operations
                int retryCount = 0;
                int maxRetries = Settings.Settings.Instance.NetworkRetryAttempts;
                Exception lastException = null;
                bool success = false;
                
                while (retryCount <= maxRetries && !success)
                {
                    try
                    {
                        if (retryCount > 0)
                        {
                            _logger.Info($"Retry {retryCount}/{maxRetries} for {sourcePath}");
                            await Task.Delay(500 * retryCount, cancellationToken);
                        }
                        
                        // Setup progress tracking if needed
                        IProgress<FileCopyProgress> progress = null;
                        if (!showProgress) // Only track progress internally when not showing Windows dialog
                        {
                            progress = new Progress<FileCopyProgress>(p => 
                            {
                                OnImportProgress(new ImportProgressEventArgs(
                                    p.ProgressPercentage / 100.0,
                                    p.BytesTransferred,
                                    p.TotalBytes,
                                    p.BytesPerSecond,
                                    p.SecondsRemaining
                                ));
                            });
                        }
                        
                        // Use Windows File Copier to show progress dialog if requested
                        IFileCopier copier;
                        if (showProgress)
                            copier = new WindowsFileCopier(source, destination);
                        else
                            copier = new SimpleFileCopier(source, destination);
                        
                        // Copy with or without progress tracking
                        if (progress != null)
                        {
                            await copier.CopyWithProgressAsync(cancellationToken, progress);
                        }
                        else
                        {
                            await copier.CopyAsync(cancellationToken);
                        }
                        
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
                        
                        // Verify the import if enabled
                        if (Settings.Settings.Instance.VerifyImportedAssets && source is FileInfo)
                        {
                            _logger.Info($"Verifying imported asset: {resultPath}");
                            
                            // Check file size
                            var sourceInfo = new FileInfo(sourcePath);
                            var destInfo = new FileInfo(resultPath);
                            
                            if (sourceInfo.Length != destInfo.Length)
                            {
                                throw new IOException($"Verification failed: File sizes don't match. " +
                                    $"Source: {sourceInfo.Length}, Destination: {destInfo.Length}");
                            }
                            
                            // Check hash for smaller files
                            if (sourceInfo.Length < 100 * 1024 * 1024) // Only for files under 100MB
                            {
                                string sourceHash = ComputeFileHash(sourcePath);
                                string destHash = ComputeFileHash(resultPath);
                                
                                if (sourceHash != destHash)
                                {
                                    throw new IOException($"Verification failed: File checksums don't match.");
                                }
                            }
                        }
                        
                        success = true;
                    }
                    catch (WindowsCopyDialogClosedException)
                    {
                        _logger.Warn("Asset import cancelled by user");
                        throw; // Don't retry if explicitly cancelled
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Warn("Asset import cancelled");
                        throw; // Don't retry if cancelled
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        _logger.Error($"Import attempt {retryCount + 1} failed: {ex.Message}");
                        retryCount++;
                        
                        // Clean up for retry
                        try
                        {
                            if (Directory.Exists(tempDir))
                            {
                                Directory.Delete(tempDir, true);
                                Directory.CreateDirectory(tempDir);
                            }
                        }
                        catch (Exception cleanupEx)
                        {
                            _logger.Error($"Failed to clean up for retry: {cleanupEx.Message}");
                        }
                    }
                }
                
                if (!success)
                {
                    throw new IOException($"Import failed after {maxRetries} attempts", lastException);
                }
                
                // Update cache registry if caching is enabled
                if (Settings.Settings.Instance.EnableAssetCaching)
                {
                    await UpdateCacheRegistryAsync(sourceKey, resultPath);
                }
                
                long fileSize = CalculateAssetSize(resultPath);
                _logger.Info($"Successfully imported asset to {resultPath}, size: {fileSize / (1024 * 1024)} MB");
                
                return new ImportResult(resultPath, true, null, fileSize, false);
            }
            catch (WindowsCopyDialogClosedException)
            {
                _logger.Warn("Asset import cancelled by user");
                return new ImportResult(null, false, new OperationCanceledException("Import cancelled by user"), 0, false);
            }
            catch (OperationCanceledException)
            {
                _logger.Warn("Asset import cancelled");
                return new ImportResult(null, false, new OperationCanceledException("Import cancelled"), 0, false);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to import asset: {ex.Message}");
                return new ImportResult(null, false, ex, 0, false);
            }
        }
        
        /// <summary>
        /// Legacy interface method - use ImportAsync instead for new code
        /// </summary>
        public async Task<string> ImportToLocalAsync(string sourcePath, bool showProgress, CancellationToken cancellationToken)
        {
            var result = await ImportAsync(sourcePath, showProgress, cancellationToken);
            if (result.Success)
                return result.Path;
            else
                return null;
        }
        
        /// <summary>
        /// Cleans up the temp directory after processing
        /// </summary>
        /// <param name="tempPath">Path to the temp directory</param>
        public void CleanupTempDirectory(string tempPath)
        {
            try
            {
                // Skip cleanup if this is a cached asset and caching is enabled
                if (Settings.Settings.Instance.EnableAssetCaching && 
                    !string.IsNullOrEmpty(tempPath) && 
                    tempPath.StartsWith(_cachePath))
                {
                    _logger.Info($"Skipping cleanup of cached asset: {tempPath}");
                    return;
                }
                
                // Clean up the file or directory
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, true);
                    _logger.Info($"Cleaned up temp directory: {tempPath}");
                }
                else if (File.Exists(tempPath))
                {
                    // For single file, get the parent directory and determine if it's a temp directory
                    string parentDir = Path.GetDirectoryName(tempPath);
                    if (parentDir != null && parentDir.Contains("EmuLibrary_Assets"))
                    {
                        Directory.Delete(parentDir, true);
                        _logger.Info($"Cleaned up temp directory: {parentDir}");
                    }
                    else
                    {
                        File.Delete(tempPath);
                        _logger.Info($"Cleaned up temp file: {tempPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to clean up temp directory {tempPath}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Clears the asset cache
        /// </summary>
        public void ClearCache()
        {
            try
            {
                _cacheLock.Wait();
                
                if (Directory.Exists(_cachePath))
                {
                    // Only delete subdirectories, not the cache directory itself
                    foreach (var dir in Directory.GetDirectories(_cachePath))
                    {
                        try
                        {
                            Directory.Delete(dir, true);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Failed to delete cache directory {dir}: {ex.Message}");
                        }
                    }
                    
                    // Clear the cache registry
                    _cachedAssets.Clear();
                    
                    // Delete the cache registry file
                    string registryPath = Path.Combine(_cachePath, "cache_registry.json");
                    if (File.Exists(registryPath))
                    {
                        File.Delete(registryPath);
                    }
                }
                
                _logger.Info("Asset cache cleared");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to clear asset cache: {ex.Message}");
            }
            finally
            {
                _cacheLock.Release();
            }
        }
        
        /// <summary>
        /// Gets information about the cache
        /// </summary>
        public CacheInfo GetCacheInfo()
        {
            try
            {
                if (!Directory.Exists(_cachePath))
                {
                    return new CacheInfo(0, 0);
                }
                
                long totalSize = GetDirectorySize(new DirectoryInfo(_cachePath));
                int itemCount = _cachedAssets.Count;
                
                return new CacheInfo(totalSize, itemCount);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to get cache info: {ex.Message}");
                return new CacheInfo(0, 0);
            }
        }
        
        #region Private methods
        
        private void LoadCacheRegistry()
        {
            try
            {
                string registryPath = Path.Combine(_cachePath, "cache_registry.json");
                if (File.Exists(registryPath))
                {
                    string json = File.ReadAllText(registryPath);
                    var registry = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    
                    if (registry != null)
                    {
                        foreach (var entry in registry)
                        {
                            if (File.Exists(entry.Value) || Directory.Exists(entry.Value))
                            {
                                _cachedAssets[entry.Key] = entry.Value;
                            }
                        }
                        
                        _logger.Info($"Loaded {_cachedAssets.Count} cached assets");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to load cache registry: {ex.Message}");
            }
        }
        
        private async Task UpdateCacheRegistryAsync(string key, string path)
        {
            try
            {
                await _cacheLock.WaitAsync();
                
                _cachedAssets[key] = path;
                
                string registryPath = Path.Combine(_cachePath, "cache_registry.json");
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(_cachedAssets);
                
                using (var writer = new StreamWriter(registryPath))
                {
                    await writer.WriteAsync(json);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to update cache registry: {ex.Message}");
            }
            finally
            {
                _cacheLock.Release();
            }
        }
        
        private string GetCachedAsset(string key)
        {
            _cachedAssets.TryGetValue(key, out string path);
            return path;
        }
        
        private long CalculateAssetSize(string path)
        {
            if (File.Exists(path))
            {
                return new FileInfo(path).Length;
            }
            else if (Directory.Exists(path))
            {
                return GetDirectorySize(new DirectoryInfo(path));
            }
            
            return 0;
        }
        
        private long GetDirectorySize(DirectoryInfo directory)
        {
            long size = 0;
            
            try
            {
                // Add size of all files
                foreach (FileInfo file in directory.GetFiles())
                {
                    size += file.Length;
                }
                
                // Add size of all subdirectories
                foreach (DirectoryInfo dir in directory.GetDirectories())
                {
                    size += GetDirectorySize(dir);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error calculating directory size: {ex.Message}");
            }
            
            return size;
        }
        
        private string GenerateSourceKey(string sourcePath)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sourcePath));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
        
        private string ComputeFileHash(string filePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
        
        private void OnImportProgress(ImportProgressEventArgs args)
        {
            ImportProgress?.Invoke(this, args);
        }
        
        #endregion
        
        public void Dispose()
        {
            _cacheLock?.Dispose();
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
    
    public class ImportProgressEventArgs : EventArgs
    {
        public double Progress { get; } // 0.0 - 1.0
        public long BytesTransferred { get; }
        public long TotalBytes { get; }
        public long BytesPerSecond { get; }
        public double SecondsRemaining { get; }
        
        public ImportProgressEventArgs(
            double progress,
            long bytesTransferred,
            long totalBytes,
            long bytesPerSecond,
            double secondsRemaining)
        {
            Progress = progress;
            BytesTransferred = bytesTransferred;
            TotalBytes = totalBytes;
            BytesPerSecond = bytesPerSecond;
            SecondsRemaining = secondsRemaining;
        }
    }
    
    public class ImportResult
    {
        public string Path { get; }
        public bool Success { get; }
        public Exception Error { get; }
        public long Size { get; }
        public bool FromCache { get; }
        
        public ImportResult(string path, bool success, Exception error, long size, bool fromCache)
        {
            Path = path;
            Success = success;
            Error = error;
            Size = size;
            FromCache = fromCache;
        }
    }
    
    public class CacheInfo
    {
        public long TotalSize { get; }
        public int ItemCount { get; }
        
        public CacheInfo(long totalSize, int itemCount)
        {
            TotalSize = totalSize;
            ItemCount = itemCount;
        }
    }
}