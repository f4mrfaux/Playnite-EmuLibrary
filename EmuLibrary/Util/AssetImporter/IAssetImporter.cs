using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EmuLibrary.Util.AssetImporter
{
    public interface IAssetImporter
    {
        /// <summary>
        /// The source file or directory to import
        /// </summary>
        FileSystemInfo Source { get; }
        
        /// <summary>
        /// The local temporary directory where the asset will be imported
        /// </summary>
        DirectoryInfo TempDirectory { get; }
        
        /// <summary>
        /// The expected size of the source file(s) in bytes
        /// </summary>
        long ExpectedSize { get; }
        
        /// <summary>
        /// The current progress of the import operation (0.0 to 1.0)
        /// </summary>
        double Progress { get; }
        
        /// <summary>
        /// Event raised when import progress changes
        /// </summary>
        event EventHandler<ImportProgressEventArgs> ProgressChanged;
        
        /// <summary>
        /// Imports the asset to the local temporary directory
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation</param>
        /// <returns>Information about the imported asset</returns>
        Task<ImportedAssetInfo> ImportAsync(CancellationToken cancellationToken);
        
        /// <summary>
        /// Cleans up temporary files associated with this import
        /// </summary>
        /// <returns>True if cleanup was successful</returns>
        bool Cleanup();
    }
    
    public class ImportProgressEventArgs : EventArgs
    {
        /// <summary>
        /// Progress value from 0.0 to 1.0
        /// </summary>
        public double Progress { get; }
        
        /// <summary>
        /// Bytes transferred so far
        /// </summary>
        public long BytesTransferred { get; }
        
        /// <summary>
        /// Total bytes to transfer
        /// </summary>
        public long TotalBytes { get; }
        
        /// <summary>
        /// Current transfer rate in bytes per second
        /// </summary>
        public long BytesPerSecond { get; }
        
        /// <summary>
        /// Estimated time remaining in seconds
        /// </summary>
        public double EstimatedSecondsRemaining { get; }
        
        public ImportProgressEventArgs(double progress, long bytesTransferred, long totalBytes, long bytesPerSecond, double estimatedSecondsRemaining)
        {
            Progress = progress;
            BytesTransferred = bytesTransferred;
            TotalBytes = totalBytes;
            BytesPerSecond = bytesPerSecond;
            EstimatedSecondsRemaining = estimatedSecondsRemaining;
        }
    }
    
    public class ImportedAssetInfo
    {
        /// <summary>
        /// The local path where the asset has been imported
        /// </summary>
        public string LocalPath { get; }
        
        /// <summary>
        /// The actual size of the imported asset in bytes
        /// </summary>
        public long ActualSize { get; }
        
        /// <summary>
        /// Whether the import was successful
        /// </summary>
        public bool Success { get; }
        
        /// <summary>
        /// Any error that occurred during import
        /// </summary>
        public Exception Error { get; }
        
        /// <summary>
        /// The unique ID for this import session
        /// </summary>
        public Guid ImportId { get; }
        
        /// <summary>
        /// The type of the imported asset
        /// </summary>
        public AssetType AssetType { get; }
        
        public ImportedAssetInfo(string localPath, long actualSize, bool success, Exception error, Guid importId, AssetType assetType)
        {
            LocalPath = localPath;
            ActualSize = actualSize;
            Success = success;
            Error = error;
            ImportId = importId;
            AssetType = assetType;
        }
    }
    
    public enum AssetType
    {
        Unknown,
        Executable,
        DiscImage,
        Archive,
        MultipartArchive
    }
}