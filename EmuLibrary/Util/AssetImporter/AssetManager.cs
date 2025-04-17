using EmuLibrary.Settings;
using EmuLibrary.Util.FileCopier;
using Playnite.SDK;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace EmuLibrary.Util.AssetImporter
{
    /// <summary>
    /// Manages importing and caching of assets from network to local temp storage
    /// </summary>
    public class AssetManager : IDisposable
    {
        private readonly ILogger _logger;
        private readonly IPlayniteAPI _playnite;
        private readonly string _cachePath;
        private readonly ConcurrentDictionary<string, string> _activeImports = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, string> _cachedAssets = new ConcurrentDictionary<string, string>();
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);
        
        // Singleton instance
        private static AssetManager _instance;
        public static AssetManager Instance => _instance;
        
        public event EventHandler<ImportProgressEventArgs> ImportProgress;
        
        public AssetManager(ILogger logger, IPlayniteAPI playnite)
        {
            _logger = logger;
            _playnite = playnite;
            _cachePath = Path.Combine(Path.GetTempPath(), "EmuLibrary_AssetCache");
            
            // Ensure cache directory exists
            if (!Directory.Exists(_cachePath))
            {
                Directory.CreateDirectory(_cachePath);
            }
            
            _instance = this;
            
            // Load cached asset registry if it exists
            LoadCacheRegistry();
        }
        
        /// <summary>
        /// Imports a source file/directory to local temp storage with resilience and progress reporting
        /// </summary>
        /// <param name="sourcePath">Path to source file/directory</param>
        /// <param name="showProgress">Whether to show Windows copy dialog</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Path to local copy of the asset</returns>
        public async Task<ImportResult> ImportAssetAsync(
            string sourcePath, 
            bool showProgress, 
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                throw new ArgumentException("Source path cannot be null or empty", nameof(sourcePath));
            }
            
            // Create a unique identifier for this import based on the source path
            // This helps with caching and preventing duplicate imports
            string sourceKey = GenerateSourceKey(sourcePath);
            
            try
            {
                _logger.Info($"Starting asset import for {sourcePath}");
                
                // Check for cached asset if caching is enabled
                if (Settings.Instance.EnableAssetCaching)
                {
                    string cachedPath = GetCachedAsset(sourceKey);
                    if (!string.IsNullOrEmpty(cachedPath) && 
                        (File.Exists(cachedPath) || Directory.Exists(cachedPath)))
                    {
                        _logger.Info($"Using cached asset for {sourcePath} at {cachedPath}");
                        return new ImportResult(
                            cachedPath,
                            true,
                            null,
                            File.Exists(cachedPath) 
                                ? new FileInfo(cachedPath).Length 
                                : GetDirectorySize(new DirectoryInfo(cachedPath)),
                            true);
                    }
                }
                
                // Check if source file/directory exists
                FileSystemInfo source;
                if (Directory.Exists(sourcePath))
                {
                    source = new DirectoryInfo(sourcePath);
                }
                else if (File.Exists(sourcePath))
                {
                    source = new FileInfo(sourcePath);
                    
                    // Check if this is a large file and should show a warning
                    if (source is FileInfo fileInfo && 
                        fileInfo.Length > Settings.Instance.LargeFileSizeWarningThresholdMB * 1024 * 1024)
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
                            return new ImportResult(
                                null,
                                false,
                                new OperationCanceledException("User cancelled large file import"),
                                0,
                                false);
                        }
                    }
                }
                else
                {
                    throw new FileNotFoundException($"Source path not found: {sourcePath}");
                }
                
                // Create temp directory for this import
                string importId = Guid.NewGuid().ToString();
                string tempDirPath = Path.Combine(
                    Settings.Instance.EnableAssetCaching ? _cachePath : Path.GetTempPath(),
                    "EmuLibrary_Assets", 
                    importId);
                
                Directory.CreateDirectory(tempDirPath);
                _activeImports[sourceKey] = tempDirPath;
                
                try
                {
                    DirectoryInfo destination = new DirectoryInfo(tempDirPath);
                    string resultPath = null;
                    
                    // Implement retry logic for network operations
                    int maxRetries = Settings.Instance.NetworkRetryAttempts;
                    int retryCount = 0;
                    bool success = false;
                    Exception lastException = null;
                    
                    while (retryCount <= maxRetries && !success)
                    {
                        try
                        {
                            if (retryCount > 0)
                            {
                                _logger.Info($"Retry {retryCount}/{maxRetries} for {sourcePath}");
                                // Small delay before retry to allow transient issues to clear
                                await Task.Delay(500 * retryCount, cancellationToken);
                            }
                            
                            // Use Windows File Copier to show progress dialog if requested
                            IFileCopier copier = showProgress 
                                ? new WindowsFileCopier(source, destination)
                                : new SimpleFileCopier(source, destination);
                            
                            // Track progress for the copy operation
                            var progressTracker = new Progress<FileCopyProgress>(progress => 
                            {
                                OnImportProgress(new ImportProgressEventArgs(
                                    progress.ProgressPercentage / 100.0,
                                    progress.BytesTransferred,
                                    progress.TotalBytes,
                                    progress.BytesPerSecond,
                                    progress.SecondsRemaining));
                            });
                            
                            // Perform the copy operation with progress tracking
                            await copier.CopyWithProgressAsync(cancellationToken, progressTracker);
                            
                            // Determine the result path
                            if (source is FileInfo)
                            {
                                resultPath = Path.Combine(tempDirPath, Path.GetFileName(sourcePath));
                            }
                            else
                            {
                                resultPath = tempDirPath;
                            }
                            
                            // Verify the imported asset if required
                            if (Settings.Instance.VerifyImportedAssets && source is FileInfo)
                            {
                                _logger.Info($"Verifying imported asset: {resultPath}");
                                
                                // Verify the file size
                                var sourceInfo = new FileInfo(sourcePath);
                                var destInfo = new FileInfo(resultPath);
                                
                                if (sourceInfo.Length != destInfo.Length)
                                {
                                    throw new IOException($"Verification failed: File sizes don't match. " +
                                        $"Source: {sourceInfo.Length}, Destination: {destInfo.Length}");
                                }
                                
                                // For extra verification, compute checksums for smaller files
                                if (sourceInfo.Length < 100 * 1024 * 1024) // Only verify files smaller than 100MB
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
                            throw; // Don't retry if user explicitly cancelled
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
                            
                            // Clean up the destination directory for retry
                            try
                            {
                                if (Directory.Exists(tempDirPath))
                                {
                                    Directory.Delete(tempDirPath, true);
                                    Directory.CreateDirectory(tempDirPath);
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
                    
                    // If caching is enabled, add to cache registry
                    if (Settings.Instance.EnableAssetCaching)
                    {
                        await UpdateCacheRegistryAsync(sourceKey, tempDirPath);
                    }
                    
                    _logger.Info($"Successfully imported asset to {resultPath}");
                    
                    // Return the result
                    return new ImportResult(
                        resultPath,
                        true,
                        null,
                        File.Exists(resultPath) 
                            ? new FileInfo(resultPath).Length 
                            : GetDirectorySize(new DirectoryInfo(resultPath)),
                        false);
                }
                catch (Exception)
                {
                    _activeImports.TryRemove(sourceKey, out _);
                    throw;
                }
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
        /// Cleans up temporary files associated with an import
        /// </summary>
        /// <param name="importPath">Path to the imported asset</param>
        /// <returns>True if cleanup was successful</returns>
        public bool CleanupImport(string importPath)
        {
            if (string.IsNullOrEmpty(importPath))
            {
                return false;
            }
            
            try
            {
                // Don't delete cached assets
                if (Settings.Instance.EnableAssetCaching && 
                    importPath.StartsWith(_cachePath, StringComparison.OrdinalIgnoreCase))
                {
                    // This is a cached asset, don't delete it
                    _logger.Info($"Not deleting cached asset: {importPath}");
                    return true;
                }
                
                if (File.Exists(importPath))
                {
                    // Get the parent directory
                    string parentDir = Path.GetDirectoryName(importPath);
                    if (parentDir.Contains("EmuLibrary_Assets"))
                    {
                        // This is one of our temp directories, delete the whole directory
                        Directory.Delete(parentDir, true);
                        _logger.Info($"Cleaned up temp directory: {parentDir}");
                    }
                    else
                    {
                        // Just delete the file
                        File.Delete(importPath);
                        _logger.Info($"Cleaned up temp file: {importPath}");
                    }
                }
                else if (Directory.Exists(importPath))
                {
                    Directory.Delete(importPath, true);
                    _logger.Info($"Cleaned up temp directory: {importPath}");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to clean up import {importPath}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Cleans up all cached assets
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
        /// Gets info about the current cache state
        /// </summary>
        /// <returns>Cache information</returns>
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
        
        #region Private Methods
        
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
                
                await File.WriteAllTextAsync(registryPath, json);
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
        
        private long GetDirectorySize(DirectoryInfo directory)
        {
            long size = 0;
            
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
            
            return size;
        }
        
        private void OnImportProgress(ImportProgressEventArgs args)
        {
            ImportProgress?.Invoke(this, args);
        }
        
        #endregion
        
        public void Dispose()
        {
            _cacheLock.Dispose();
            _instance = null;
        }
    }
    
    // The following classes were moved to AssetImporter.cs:
    // - ImportProgressEventArgs
    // - ImportResult
    // - CacheInfo
}