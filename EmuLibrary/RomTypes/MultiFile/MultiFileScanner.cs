using EmuLibrary.PlayniteCommon;
using EmuLibrary.Settings;
using EmuLibrary.Util;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace EmuLibrary.RomTypes.MultiFile
{
    internal class MultiFileScanner : RomTypeScanner
    {
        private readonly IPlayniteAPI _playniteAPI;

        // Hack to exclude anything past disc one for games we're not treating as multi-file / m3u but have multiple discs :|
        static private readonly Regex s_discXpattern = new Regex(@"\((?:Disc|Disk) \d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public override RomType RomType => RomType.MultiFile;
        public override Guid LegacyPluginId => EmuLibrary.PluginId;

        public MultiFileScanner(IEmuLibrary emuLibrary) : base(emuLibrary)
        {
            _playniteAPI = emuLibrary.Playnite;
        }

        public override IEnumerable<GameMetadata> GetGames(EmulatorMapping mapping, LibraryGetGamesArgs args)
        {
            if (args.CancelToken.IsCancellationRequested)
                yield break;

            var imageExtensionsLower = mapping.ImageExtensionsLower;
            var srcPath = mapping.SourcePath;
            var dstPath = mapping.DestinationPathResolved;
            SafeFileEnumerator fileEnumerator;

            #region Import "installed" games
            if (Directory.Exists(dstPath))
            {
                // For MultiFile, we still only want the top-level directories as entry points,
                // but we want to scan all subdirectories to find candidate folders
                fileEnumerator = new SafeFileEnumerator(dstPath, "*.*", SearchOption.AllDirectories);
                
                // Keep track of directories we've already processed to avoid duplicates
                var processedDirs = new HashSet<string>();

                foreach (var file in fileEnumerator)
                {
                    if (args.CancelToken.IsCancellationRequested)
                        yield break;

                    // Skip regular files, only process directories
                    if (!file.Attributes.HasFlag(FileAttributes.Directory) || s_discXpattern.IsMatch(file.Name))
                        continue;
                    
                    // Get the full path to the directory
                    var dirPath = file.FullName;
                    
                    // If we already processed this directory or any parent directory, skip it
                    if (processedDirs.Any(dir => dirPath.StartsWith(dir)))
                        continue;
                        
                    var dirEnumerator = new SafeFileEnumerator(dirPath, "*.*", SearchOption.AllDirectories);
                    // First matching rom of first valid extension that has any matches. Ex. for "m3u,cue,bin", make sure we don't grab a bin file when there's an m3u or cue handy
                    var rom = imageExtensionsLower.Select(ext => dirEnumerator.FirstOrDefault(f => HasMatchingExtension(f, ext))).FirstOrDefault(f => f != null);
                    
                    if (rom != null)
                    {
                        // Add this directory to the processed list to avoid duplicates
                        processedDirs.Add(dirPath);
                        
                        // Calculate relative path from the destination root
                        var relativeDirPath = dirPath.Substring(dstPath.Length).TrimStart(Path.DirectorySeparatorChar);
                        var relativeRomPath = rom.FullName.Substring(dirPath.Length).TrimStart(Path.DirectorySeparatorChar);
                        
                        var baseFileName = StringExtensions.GetPathWithoutAllExtensions(Path.GetFileName(file.Name));
                        var gameName = StringExtensions.NormalizeGameName(baseFileName);
                        var info = new MultiFileGameInfo()
                        {
                            MappingId = mapping.MappingId,

                            // Relative to mapping.SourcePath
                            SourceFilePath = Path.Combine(relativeDirPath, relativeRomPath),
                            SourceBaseDir = relativeDirPath,
                        };

                        yield return new GameMetadata()
                        {
                            Source = EmuLibrary.SourceName,
                            Name = gameName,
                            Roms = new List<GameRom>() { new GameRom(gameName, _playniteAPI.Paths.IsPortable ? rom.FullName.Replace(_playniteAPI.Paths.ApplicationPath, Playnite.SDK.ExpandableVariables.PlayniteDirectory) : rom.FullName) },
                            InstallDirectory = _playniteAPI.Paths.IsPortable ? dirPath.Replace(_playniteAPI.Paths.ApplicationPath, Playnite.SDK.ExpandableVariables.PlayniteDirectory) : dirPath,
                            IsInstalled = true,
                            GameId = info.AsGameId(),
                            Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(mapping.Platform.Name) },
                            Regions = FileNameUtils.GuessRegionsFromRomName(baseFileName).Select(r => new MetadataNameProperty(r)).ToHashSet<MetadataProperty>(),
                            InstallSize = (ulong)dirEnumerator.Where(f => !f.Attributes.HasFlag(FileAttributes.Directory)).Select(f => new FileInfo(f.FullName)).Sum(f => f.Length),
                            GameActions = new List<GameAction>() { new GameAction()
                                {
                                    Name = $"Play in {mapping.Emulator.Name}",
                                    Type = GameActionType.Emulator,
                                    EmulatorId = mapping.EmulatorId,
                                    EmulatorProfileId = mapping.EmulatorProfileId,
                                    IsPlayAction = true,
                                } }
                        };
                    }
                }
            }
            #endregion

            #region Import "uninstalled" games
            if (Directory.Exists(srcPath))
            {
                // Use AllDirectories to search recursively
                fileEnumerator = new SafeFileEnumerator(srcPath, "*.*", SearchOption.AllDirectories);
                
                // Keep track of directories we've already processed to avoid duplicates
                var processedDirs = new HashSet<string>();

                foreach (var file in fileEnumerator)
                {
                    if (args.CancelToken.IsCancellationRequested)
                        yield break;

                    // Skip regular files, only process directories
                    if (!file.Attributes.HasFlag(FileAttributes.Directory) || s_discXpattern.IsMatch(file.Name))
                        continue;
                        
                    // Get the full path to the directory
                    var dirPath = file.FullName;
                    
                    // If we already processed this directory or any parent directory, skip it
                    if (processedDirs.Any(dir => dirPath.StartsWith(dir)))
                        continue;

                    var dirEnumerator = new SafeFileEnumerator(dirPath, "*.*", SearchOption.AllDirectories);
                    // First matching rom of first valid extension that has any matches. Ex. for "m3u,cue,bin", make sure we don't grab a bin file when there's an m3u or cue handy
                    var rom = imageExtensionsLower.Select(ext => dirEnumerator.FirstOrDefault(f => HasMatchingExtension(f, ext))).FirstOrDefault(f => f != null);
                    
                    if (rom != null)
                    {
                        // Add this directory to the processed list to avoid duplicates
                        processedDirs.Add(dirPath);
                        
                        // Calculate relative paths from source root
                        var relativeDirPath = dirPath.Substring(srcPath.Length).TrimStart(Path.DirectorySeparatorChar);
                        var relativeRomPath = rom.FullName.Substring(dirPath.Length).TrimStart(Path.DirectorySeparatorChar);
                        
                        // Check if this game is already installed
                        var fileInfo = new FileInfo(rom.FullName);
                        var dirInfo = new DirectoryInfo(dirPath);
                        var equivalentInstalledPath = Path.Combine(dstPath, Path.Combine(relativeDirPath, relativeRomPath));

                        if (File.Exists(equivalentInstalledPath))
                        {
                            continue;
                        }

                        var info = new MultiFileGameInfo()
                        {
                            MappingId = mapping.MappingId,

                            // Relative to mapping.SourcePath
                            SourceFilePath = Path.Combine(relativeDirPath, relativeRomPath),
                            SourceBaseDir = relativeDirPath,
                        };

                        var baseFileName = StringExtensions.GetPathWithoutAllExtensions(Path.GetFileName(file.Name));
                        var gameName = StringExtensions.NormalizeGameName(baseFileName);

                        yield return new GameMetadata()
                        {
                            Source = EmuLibrary.SourceName,
                            Name = gameName,
                            IsInstalled = false,
                            GameId = info.AsGameId(),
                            Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(mapping.Platform.Name) },
                            Regions = FileNameUtils.GuessRegionsFromRomName(baseFileName).Select(r => new MetadataNameProperty(r)).ToHashSet<MetadataProperty>(),
                            InstallSize = (ulong)dirEnumerator.Where(f => !f.Attributes.HasFlag(FileAttributes.Directory)).Select(f => new FileInfo(f.FullName)).Sum(f => f.Length),
                            GameActions = new List<GameAction>() { new GameAction()
                                {
                                    Name = $"Play in {mapping.Emulator.Name}",
                                    Type = GameActionType.Emulator,
                                    EmulatorId = mapping.EmulatorId,
                                    EmulatorProfileId = mapping.EmulatorProfileId,
                                    IsPlayAction = true,
                                } }
                        };
                    }
                }
            }
            #endregion
        }

        public override bool TryGetGameInfoBaseFromLegacyGameId(Game game, EmulatorMapping mapping, out ELGameInfo gameInfo)
        {
            // OLD /////////////////////////////////////////////////
            // GameId format - segments divided by '|'.
            // 0 - Was flag string, with only flag ever being * for multi-file. Now is base game path if multifile
            // 1 - Full Rom file source path
            // If no segments present (no '|'), then entire value is Full Rom file source path (1)

            if (!game.GameId.Contains(".") || !game.GameId.Contains("|"))
            {
                gameInfo = null;
                return false;
            }

            var playAction = game.GameActions.Where(ga => ga.IsPlayAction).First();
            if (mapping.RomType != RomType.MultiFile)
            {
                gameInfo = null;
                return false;
            }

            var parts = game.GameId.Split('|');

            Debug.Assert(parts.Length == 2, $"GameId is not in expected format (expected 2 parts, got {parts.Length})");

            if (string.IsNullOrEmpty(parts[0]))
            {
                gameInfo = null;
                return false;

            }

            gameInfo = new RomTypes.MultiFile.MultiFileGameInfo()
            {
                MappingId = mapping.MappingId,
                SourceFilePath = parts[1].Replace(mapping.SourcePath, "").TrimStart('\\'),
                SourceBaseDir = parts[0] == "*" ? Path.GetDirectoryName(parts[1]) : parts[0].Replace(mapping.SourcePath, "").TrimStart('\\'),
            };

            return true;
        }

        public override IEnumerable<Game> GetUninstalledGamesMissingSourceFiles(CancellationToken ct)
        {
            return _playniteAPI.Database.Games.TakeWhile(g => !ct.IsCancellationRequested)
                .Where(g =>
            {
                if (g.PluginId != EmuLibrary.PluginId || g.IsInstalled)
                    return false;

                var info = g.GetELGameInfo();
                if (info.RomType != RomType.MultiFile)
                    return false;

                return !Directory.Exists((info as MultiFileGameInfo).SourceFullBaseDir);
            });
        }
    }
}
