using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using EmuLibrary.Settings;

namespace EmuLibrary.RomTypes.GogInstaller
{
    // Define delegate for progress updates
    internal delegate void RomScanProgressUpdate(string status, int current, int total);

    internal class GogInstallerScanner : RomTypeScanner
    {
        // Patterns to identify GOG installers
        private readonly string[] _gogPatterns = new[]
        {
            @"setup_.*_gog",
            @"gog.*setup",
            @"setup_.*_(\d+\.\d+\.\d+)",
            @"installer_.*"
        };

        private readonly ILogger _logger;
        private readonly IEmuLibrary _emuLibrary;

        public override Guid LegacyPluginId => Guid.Parse("e4ac81a0-1025-4415-9c0e-5df6a4d53f68");
        public override RomType RomType => RomType.GogInstaller;

        public GogInstallerScanner(IEmuLibrary emuLibrary) : base(emuLibrary)
        {
            _emuLibrary = emuLibrary;
            _logger = emuLibrary.Logger;
        }

        private List<string> GetSupportedExtensions()
        {
            return new List<string> { ".exe", ".msi" };
        }

        public List<ELGameInfo> ScanSource(string sourceDir, RomScanProgressUpdate progressCallback)
        {
            _logger.Info($"Scanning for GOG installers in {sourceDir}");
            var results = new List<ELGameInfo>();

            try
            {
                if (!Directory.Exists(sourceDir))
                {
                    _logger.Error($"Source directory does not exist: {sourceDir}");
                    return results;
                }

                // Get all exe and msi files in the source directory
                var extensions = GetSupportedExtensions();
                var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();

                _logger.Info($"Found {files.Count} potential installer files");
                int current = 0;

                foreach (var file in files)
                {
                    progressCallback?.Invoke($"Scanning {Path.GetFileName(file)}", current++, files.Count);
                    
                    if (IsGogInstaller(file))
                    {
                        string name = ExtractGameName(file);
                        
                        var gameInfo = new GogInstallerGameInfo(name, file);
                        
                        results.Add(gameInfo);
                        _logger.Info($"Added GOG installer: {name} from {file}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error scanning source directory: {ex.Message}");
            }

            return results;
        }

        public override IEnumerable<GameMetadata> GetGames(EmulatorMapping mapping, LibraryGetGamesArgs args)
        {
            if (string.IsNullOrEmpty(mapping?.SourcePath) || !Directory.Exists(mapping.SourcePath))
            {
                yield break;
            }

            var gameInfoList = ScanSource(mapping.SourcePath, null);
            foreach (var gameInfo in gameInfoList)
            {
                var gogGameInfo = gameInfo as GogInstallerGameInfo;
                if (gogGameInfo != null)
                {
                    gogGameInfo.MappingId = mapping.MappingId;
                    
                    yield return new GameMetadata
                    {
                        Name = gogGameInfo.Name,
                        GameId = gogGameInfo.AsGameId(),
                        Source = EmuLibrary.SourceName,
                        Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(mapping.Platform.Name) }
                    };
                }
            }
        }

        public override bool TryGetGameInfoBaseFromLegacyGameId(Game game, EmulatorMapping mapping, out ELGameInfo gameInfo)
        {
            gameInfo = null;
            return false; // No legacy support for GOG installers
        }

        public override IEnumerable<Game> GetUninstalledGamesMissingSourceFiles(CancellationToken ct)
        {
            yield break; // Not implemented for GOG installers
        }

        /// <summary>
        /// Determines if a file is a GOG installer
        /// </summary>
        private bool IsGogInstaller(string path)
        {
            string filename = Path.GetFileName(path).ToLower();
            
            // Check against known patterns
            foreach (var pattern in _gogPatterns)
            {
                if (Regex.IsMatch(filename, pattern, RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
            
            // Check file properties if not matched by pattern
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(path);
                if (versionInfo.CompanyName?.Contains("GOG") == true ||
                    versionInfo.FileDescription?.Contains("GOG") == true ||
                    versionInfo.ProductName?.Contains("GOG") == true)
                {
                    return true;
                }
            }
            catch
            {
                // Ignore errors in file property reading
            }
            
            return false;
        }

        /// <summary>
        /// Extracts a game name from the installer filename
        /// </summary>
        private string ExtractGameName(string path)
        {
            string filename = Path.GetFileNameWithoutExtension(path);
            
            // Remove common prefixes
            string[] prefixes = { "setup_", "gog_", "installer_" };
            foreach (var prefix in prefixes)
            {
                if (filename.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    filename = filename.Substring(prefix.Length);
                }
            }
            
            // Remove version numbers, GOG suffix
            filename = Regex.Replace(filename, @"_v?\d+\.\d+\.\d+.*$", "");
            filename = Regex.Replace(filename, @"_gog$", "");
            
            // Replace underscores with spaces
            filename = filename.Replace('_', ' ');
            
            // Title case
            var textInfo = new System.Globalization.CultureInfo("en-US", false).TextInfo;
            filename = textInfo.ToTitleCase(filename);
            
            return filename;
        }
    }
}
