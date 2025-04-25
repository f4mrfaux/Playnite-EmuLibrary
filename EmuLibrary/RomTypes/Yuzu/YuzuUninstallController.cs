using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace EmuLibrary.RomTypes.Yuzu
{
    class YuzuUninstallController : ELUninstallController
    {
        private readonly SourceDirCache _cache;
        private readonly YuzuGameInfo _gameInfo;

        internal YuzuUninstallController(Game game, IEmuLibrary emuLibrary) : base(game, emuLibrary)
        {
            _gameInfo = game.GetYuzuGameInfo();
            var scanner = _emuLibrary.GetScanner(RomType.Yuzu) as YuzuScanner;
            if (scanner == null)
            {
                _emuLibrary.Logger.Error("YuzuScanner not found. Cannot continue with uninstallation.");
                throw new System.InvalidOperationException("Yuzu scanner not found");
            }
            _cache = scanner.GetCacheForMapping(_gameInfo.MappingId);

            Name = string.Format("Uninstall from {0}", _gameInfo.Mapping.Emulator?.Name ?? "Emulator");
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            var yuzu = new Yuzu(_gameInfo.Mapping.EmulatorBasePathResolved, _emuLibrary.Logger);
            yuzu.UninstallTitleFromNand(Game.GameId);
            _cache.TheCache.InstalledGames.Remove(_gameInfo.TitleId);

            InvokeOnUninstalled(new GameUninstalledEventArgs());
        }
    }
}