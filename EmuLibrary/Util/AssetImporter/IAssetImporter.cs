using System;
using System.Threading;
using System.Threading.Tasks;

namespace EmuLibrary.Util.AssetImporter
{
    /// <summary>
    /// Interface for asset importers that copy files to local storage before processing
    /// </summary>
    public interface IAssetImporter
    {
        /// <summary>
        /// Imports a source file/directory to local temp storage
        /// </summary>
        /// <param name="sourcePath">Path to the source file/directory</param>
        /// <param name="showProgress">Whether to show progress dialog</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Path to the local temp copy</returns>
        Task<string> ImportToLocalAsync(string sourcePath, bool showProgress, CancellationToken cancellationToken);
        
        /// <summary>
        /// Cleans up the temp directory after processing
        /// </summary>
        /// <param name="tempPath">Path to the temp directory</param>
        void CleanupTempDirectory(string tempPath);
    }
}