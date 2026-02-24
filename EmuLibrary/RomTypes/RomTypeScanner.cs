using EmuLibrary.Settings;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;

namespace EmuLibrary.RomTypes
{
    public abstract class RomTypeScanner
    {
        protected readonly IEmuLibrary _emuLibrary;

        public RomTypeScanner(IEmuLibrary emuLibrary)
        {
            _emuLibrary = emuLibrary;
        }

        public abstract Guid LegacyPluginId { get; }

        public abstract RomType RomType { get; }

        public abstract bool TryGetGameInfoBaseFromLegacyGameId(Game game, EmulatorMapping mapping, out ELGameInfo gameInfo);
        public virtual LegacySettingsMigrationResult MigrateLegacyPluginSettings(Plugin plugin, out EmulatorMapping mapping)
        {
            mapping = null;
            return LegacySettingsMigrationResult.Unnecessary;
        }
        public abstract IEnumerable<GameMetadata> GetGames(EmulatorMapping mapping, LibraryGetGamesArgs args);
        public abstract IEnumerable<Game> GetUninstalledGamesMissingSourceFiles(CancellationToken ct);
        
        protected static bool HasMatchingExtension(FileSystemInfoBase file, string extension)
        {
            // Handle null cases safely
            if (file == null)
                return false;

            if (file.Extension == null)
                return extension == "<none>";

            // Normalize extensions for comparison
            string fileExt = file.Extension.TrimStart('.').ToLowerInvariant();
            string compareExt = extension.ToLowerInvariant();

            // Compare extensions case-insensitively
            return fileExt == compareExt || (file.Extension == "" && extension == "<none>");
        }

        private static readonly string[] _extractedContentPatterns =
            { "setup.exe", "install.exe", "launcher.exe", "game.exe", "bin", "data", "redist" };

        private static readonly HashSet<string> _systemFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "system", "windows", "program files", "users", "games" };

        /// <summary>
        /// Checks if a folder appears to contain extracted game content (many files, subdirectories,
        /// or common installer patterns) rather than being a simple folder of disc images/archives.
        /// Results are cached per-scan to avoid redundant NAS directory reads.
        /// </summary>
        protected bool IsExtractedContentFolder(string folderPath, Dictionary<string, bool> cache)
        {
            if (cache.TryGetValue(folderPath, out var cached))
                return cached;

            bool isExtracted = false;
            try
            {
                var files = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly);
                var dirs = Directory.GetDirectories(folderPath, "*", SearchOption.TopDirectoryOnly);

                if (files.Length > 15 || dirs.Length > 5)
                {
                    isExtracted = true;
                }
                else
                {
                    var lowerFileNames = files.Select(f => Path.GetFileName(f).ToLowerInvariant()).ToArray();
                    isExtracted = _extractedContentPatterns.Any(p => lowerFileNames.Any(f => f.Contains(p)));
                }

                if (!isExtracted)
                {
                    isExtracted = _systemFolderNames.Contains(Path.GetFileName(folderPath));
                }
            }
            catch
            {
                // On error (e.g., access denied on NAS), assume not extracted
            }

            cache[folderPath] = isExtracted;
            return isExtracted;
        }
    }
}
