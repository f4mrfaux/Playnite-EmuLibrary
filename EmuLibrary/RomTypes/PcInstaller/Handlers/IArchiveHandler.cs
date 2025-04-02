using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EmuLibrary.RomTypes.PcInstaller.Handlers
{
    /// <summary>
    /// Interface for classes that handle different archive formats
    /// </summary>
    public interface IArchiveHandler
    {
        /// <summary>
        /// Checks if the specified file is a supported archive format
        /// </summary>
        /// <param name="filePath">Path to the file to check</param>
        /// <returns>True if the format is supported, otherwise false</returns>
        bool CanHandle(string filePath);

        /// <summary>
        /// Lists the contents of the archive
        /// </summary>
        /// <param name="archivePath">Path to the archive</param>
        /// <returns>A list of files in the archive</returns>
        List<string> ListContents(string archivePath);

        /// <summary>
        /// Extracts the archive to a specified location
        /// </summary>
        /// <param name="archivePath">Path to the archive</param>
        /// <param name="destinationPath">Destination path for extraction</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Path to the extracted content</returns>
        Task<string> ExtractAsync(string archivePath, string destinationPath, CancellationToken cancellationToken);

        /// <summary>
        /// Extracts a specific file from the archive
        /// </summary>
        /// <param name="archivePath">Path to the archive</param>
        /// <param name="fileToExtract">Filename to extract</param>
        /// <param name="destinationPath">Destination path for extraction</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Path to the extracted file</returns>
        Task<string> ExtractFileAsync(string archivePath, string fileToExtract, string destinationPath, CancellationToken cancellationToken);

        /// <summary>
        /// Returns a display name for the archive
        /// </summary>
        /// <param name="archivePath">Path to the archive</param>
        /// <returns>A user-friendly name for the archive</returns>
        string GetArchiveDisplayName(string archivePath);

        /// <summary>
        /// Returns the expected installation size for the archive
        /// </summary>
        /// <param name="archivePath">Path to the archive</param>
        /// <returns>Estimated size in bytes, or 0 if unknown</returns>
        ulong GetExpectedInstallSize(string archivePath);
    }
}