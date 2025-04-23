using EmuLibrary.Settings;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Threading;

namespace EmuLibrary.RomTypes
{
    internal abstract class RomTypeScanner
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public RomTypeScanner(IEmuLibrary emuLibrary) { }
#pragma warning restore IDE0060 // Remove unused parameter
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
            bool basicMatch = fileExt == compareExt || (file.Extension == "" && extension == "<none>");
            
            // If basic match fails, try backup method with string ending check
            if (!basicMatch && file.Name != null) {
                // Check if the file name ends with the extension pattern
                bool fallbackMatch = file.Name.EndsWith("." + compareExt, StringComparison.OrdinalIgnoreCase);
                return fallbackMatch;
            }
            
            return basicMatch;
        }
    }
}
