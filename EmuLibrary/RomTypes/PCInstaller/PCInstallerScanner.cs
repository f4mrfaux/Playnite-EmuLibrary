using EmuLibrary.PlayniteCommon;
using EmuLibrary.Settings;
using EmuLibrary.Util;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace EmuLibrary.RomTypes.PCInstaller
{
    internal class PCInstallerScanner : RomTypeScanner
    {
        private readonly IPlayniteAPI _playniteAPI;
        private readonly IEmuLibrary _emuLibrary;

        public override RomType RomType => RomType.PCInstaller;
        public override Guid LegacyPluginId => EmuLibrary.PluginId;

        public PCInstallerScanner(IEmuLibrary emuLibrary) : base(emuLibrary)
        {
            _playniteAPI = emuLibrary.Playnite;
            _emuLibrary = emuLibrary;
        }

        public override IEnumerable<GameMetadata> GetGames(EmulatorMapping mapping, LibraryGetGamesArgs args)
        {
            if (args.CancelToken.IsCancellationRequested)
                yield break;

            var srcPath = mapping.SourcePath;
            var dstPath = mapping.DestinationPathResolved;

            // Only support EXE files for PC game installers
            var installerExtensions = new List<string> { "exe" };
            
            SafeFileEnumerator fileEnumerator;

            #region Import discovered installers
            if (Directory.Exists(srcPath))
            {
                fileEnumerator = new SafeFileEnumerator(srcPath, "*.*", SearchOption.AllDirectories);

                foreach (var file in fileEnumerator)
                {
                    if (args.CancelToken.IsCancellationRequested)
                        yield break;

                    foreach (var extension in installerExtensions)
                    {
                        if (args.CancelToken.IsCancellationRequested)
                            yield break;

                        if (HasMatchingExtension(file, extension))
                        {
                            // For PC installers, we use the parent folder name as the game name
                            var parentFolder = Directory.GetParent(file.FullName).Name;
                            var gameName = StringExtensions.NormalizeGameName(parentFolder);
                            
                            var relativePath = file.FullName.Substring(srcPath.Length).TrimStart(Path.DirectorySeparatorChar);
                            
                            var info = new PCInstallerGameInfo()
                            {
                                MappingId = mapping.MappingId,
                                SourcePath = relativePath,
                                InstallerFullPath = file.FullName,
                                InstallDirectory = null // Will be set during installation
                            };

                            yield return new GameMetadata()
                            {
                                Source = EmuLibrary.SourceName,
                                Name = gameName,
                                IsInstalled = false, // PC games start as uninstalled
                                GameId = info.AsGameId(),
                                Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(mapping.Platform.Name) },
                                InstallSize = (ulong)new FileInfo(file.FullName).Length,
                                GameActions = new List<GameAction>() 
                                { 
                                    new GameAction()
                                    {
                                        Name = "Install Game",
                                        Type = GameActionType.URL,
                                        Path = "", // Will be updated after installation
                                        IsPlayAction = false
                                    }
                                }
                            };
                        }
                    }
                }
            }
            #endregion
            
            #region Update installed games
            var installedGames = _playniteAPI.Database.Games
                .Where(g => g.PluginId == EmuLibrary.PluginId && g.IsInstalled)
                .Select(g => {
                    try {
                        var info = g.GetELGameInfo();
                        if (info.RomType == RomType.PCInstaller) {
                            return (g, info as PCInstallerGameInfo);
                        }
                    } catch { }
                    return (null, null);
                })
                .Where(pair => pair.Item1 != null);
            
            foreach (var (game, gameInfo) in installedGames)
            {
                if (args.CancelToken.IsCancellationRequested)
                    yield break;
                
                if (!string.IsNullOrEmpty(gameInfo.InstallDirectory) && Directory.Exists(gameInfo.InstallDirectory))
                {
                    // Game is still installed, update it
                    yield return new GameMetadata()
                    {
                        Source = EmuLibrary.SourceName,
                        Name = game.Name,
                        IsInstalled = true,
                        GameId = gameInfo.AsGameId(),
                        InstallDirectory = gameInfo.InstallDirectory,
                        Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(mapping.Platform.Name) },
                        GameActions = new List<GameAction>() 
                        { 
                            new GameAction()
                            {
                                Name = "Play",
                                Type = GameActionType.File,
                                Path = !string.IsNullOrEmpty(gameInfo.PrimaryExecutable) 
                                    ? gameInfo.PrimaryExecutable 
                                    : Playnite.Database.Games.Get(game.Id)?.GameActions?.FirstOrDefault(a => a.IsPlayAction)?.Path 
                                    ?? gameInfo.InstallDirectory,
                                IsPlayAction = true
                            }
                        }
                    };
                }
            }
            #endregion
        }

        public override bool TryGetGameInfoBaseFromLegacyGameId(Game game, EmulatorMapping mapping, out ELGameInfo gameInfo)
        {
            // PC installers are a new type, so there are no legacy game IDs to convert
            gameInfo = null;
            return false;
        }

        public override IEnumerable<Game> GetUninstalledGamesMissingSourceFiles(CancellationToken ct)
        {
            return _playniteAPI.Database.Games.TakeWhile(g => !ct.IsCancellationRequested)
                .Where(g =>
                {
                    if (g.PluginId != EmuLibrary.PluginId || g.IsInstalled)
                        return false;

                    try
                    {
                        var info = g.GetELGameInfo();
                        if (info.RomType != RomType.PCInstaller)
                            return false;

                        return !File.Exists((info as PCInstallerGameInfo).SourceFullPath);
                    }
                    catch
                    {
                        return false;
                    }
                });
        }
    }
}