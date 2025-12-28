using System;
using System.IO;

namespace EmuLibrary.Util
{
    /// <summary>
    /// Utility class for validating file system paths against Windows limitations.
    /// </summary>
    internal static class PathValidator
    {
        // Windows MAX_PATH is 260, including null terminator (259 usable characters)
        private const int MAX_PATH = 260;

        // For directories, Windows recommends MAX_PATH - 12 to allow for file names (248 characters)
        private const int MAX_DIRECTORY_PATH = 248;

        /// <summary>
        /// Validates that a file path is within Windows MAX_PATH limits.
        /// </summary>
        /// <param name="path">The file path to validate</param>
        /// <param name="errorMessage">Output parameter containing error message if validation fails</param>
        /// <returns>True if path is valid, false if it exceeds limits</returns>
        public static bool ValidateFilePath(string path, out string errorMessage)
        {
            if (string.IsNullOrEmpty(path))
            {
                errorMessage = "Path cannot be null or empty";
                return false;
            }

            // Get absolute path to check actual length
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                errorMessage = $"Invalid path format: {ex.Message}";
                return false;
            }

            if (fullPath.Length >= MAX_PATH)
            {
                errorMessage = $"File path exceeds Windows MAX_PATH limit of {MAX_PATH} characters (path length: {fullPath.Length})";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// Validates that a directory path is within Windows limits.
        /// Uses a more conservative limit (248 chars) to allow space for file names.
        /// </summary>
        /// <param name="path">The directory path to validate</param>
        /// <param name="errorMessage">Output parameter containing error message if validation fails</param>
        /// <returns>True if path is valid, false if it exceeds limits</returns>
        public static bool ValidateDirectoryPath(string path, out string errorMessage)
        {
            if (string.IsNullOrEmpty(path))
            {
                errorMessage = "Path cannot be null or empty";
                return false;
            }

            // Get absolute path to check actual length
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                errorMessage = $"Invalid path format: {ex.Message}";
                return false;
            }

            if (fullPath.Length >= MAX_DIRECTORY_PATH)
            {
                errorMessage = $"Directory path exceeds recommended limit of {MAX_DIRECTORY_PATH} characters (allows space for file names). Path length: {fullPath.Length}";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// Truncates a game name to ensure the resulting install path stays within limits.
        /// </summary>
        /// <param name="basePath">The base installation directory</param>
        /// <param name="gameName">The game name to use as subfolder</param>
        /// <param name="maxExtraLength">Maximum length to reserve for additional subfolders/files (default: 50)</param>
        /// <returns>Truncated game name that will keep total path within limits</returns>
        public static string TruncateGameNameForPath(string basePath, string gameName, int maxExtraLength = 50)
        {
            if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(gameName))
            {
                return gameName;
            }

            try
            {
                var basePathFull = Path.GetFullPath(basePath);
                var availableLength = MAX_DIRECTORY_PATH - basePathFull.Length - maxExtraLength - 1; // -1 for path separator

                if (gameName.Length <= availableLength)
                {
                    return gameName;
                }

                // Truncate and add ellipsis
                if (availableLength > 3)
                {
                    return gameName.Substring(0, availableLength - 3) + "...";
                }

                // If available length is very short, just truncate
                return gameName.Substring(0, Math.Max(1, availableLength));
            }
            catch
            {
                // If anything fails, return original game name
                return gameName;
            }
        }
    }
}
