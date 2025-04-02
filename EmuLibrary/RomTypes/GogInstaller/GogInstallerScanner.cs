using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Playnite.SDK;
using EmuLibrary.RomTypes;

namespace EmuLibrary.RomTypes.GogInstaller
{
    public class GogInstallerScanner : RomTypeScanner
    {
        // Patterns to identify GOG installers
        private readonly string[] _gogPatterns = new[]
        {
            @"setup_.*_gog",
            @"gog.*setup",
            @"setup_.*_(\d+\.\d+\.\d+)",
            @"installer_.*"
        };

        public GogInstallerScanner(ILogger logger) : base(logger)
        {
        }

        public override List<string> GetSupportedExtensions()
        {
            return new List<string> { ".exe", ".msi" };
        }

        public override List<ELGameInfo> ScanSource(string sourceDir, RomScanProgressUpdate progressCallback)
        {
            Logger.Info($"Scanning for GOG installers in {sourceDir}");
            var results = new List<ELGameInfo>();

            try
            {
                if (!Directory.Exists(sourceDir))
                {
                    Logger.Error($"Source directory does not exist: {sourceDir}");
                    return results;
                }

                // Get all exe and msi files in the source directory
                var extensions = GetSupportedExtensions();
                var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();

                Logger.Info($"Found {files.Count} potential installer files");

                foreach (var file in files)
                {
                    if (IsGogInstaller(file))
                    {
                        string name = ExtractGameName(file);
                        
                        var gameInfo = new GogInstallerGameInfo(name, file);
                        
                        results.Add(gameInfo);
                        Logger.Info($"Added GOG installer: {name} from {file}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error scanning source directory: {ex.Message}");
            }

            return results;
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
