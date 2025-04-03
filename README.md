# EmuLibrary PC Game Manager

EmuLibrary PC Manager helps you manage PC game installers within [Playnite](https://www.playnite.link). Store your game installers on a network share and install them only when you want to play, saving local disk space.

> **Documentation Index**
> - **README.md**: Overview, features, quick setup (you are here)
> - **USER_GUIDE.md**: Detailed usage instructions
> - **WORKFLOW.md**: Common user workflows
> - **MIGRATION.md**: Upgrading from previous versions
> - **ARCHITECTURE.md**: Technical design (for developers)
> - **BUILD.md**: Build instructions (for developers)
> - **MAINTENANCE.md**: Development guidelines (for developers)

## Key Features

- **PC Game Installer Management**: Detect and manage various game installers (.exe, .msi)
- **GOG Integration**: Special handling for GOG installers with silent parameters
- **Advanced Archive Support**: Handle ISO files, RAR archives (including multi-part), and nested archives
- **Smart Executable Detection**: Automatically find the game executable after installation
- **Network Storage Integration**: Works with SMB shares and other network storage
- **UI Enhancements**: Improved contrast for better readability in all themes

## How It Works

1. **Store your installers** on a network share or local folder
2. **Configure the plugin** to scan your installer location
3. **Browse games** in Playnite (they appear as "not installed")
4. **Click "Play"** on a game to install it on demand
5. The plugin:
   - Copies/extracts the installer
   - Runs it silently with appropriate parameters
   - Finds the game executable
   - Updates Playnite with installation info
6. **Launch and play** directly from Playnite

## Quick Start (5-Minute Setup)

1. **Install the plugin** in Playnite:
   - Extensions → Browse → EmuLibrary PC Game Manager
   - Click Install and restart Playnite

2. **Add your first mapping**:
   - Settings → EmuLibrary PC Manager
   - **Emulator**: Select any (e.g., "Windows")
   - **Profile**: Select any (e.g., "Default")
   - **Platform**: Select "PC"
   - **ROM Type**: "PcInstaller" (handles all installers including GOG)
   - **Source Path**: Your network/local folder with game installers
   - **Destination Path**: Where you want games installed
   - **Enabled**: Check this box
   - Click Save and restart Playnite

3. **Your games will appear** in your library ready to install!

> 💡 See **USER_GUIDE.md** for detailed instructions and **WORKFLOW.md** for common tasks.

## Folder Organization

For best results, use this structure:

```
/Games/
  ├── PC/
  │   ├── The Witcher 3/
  │   │   └── setup_the_witcher_3_goty_2.0.0.47.exe
  ├── GOG/
  │   ├── Disco Elysium/
  │   │   └── setup_disco_elysium_the_final_cut_2.0.0.13.exe
```

The plugin uses folder names for better game identification!

## Supported Formats

- **Installer Types**: .exe, .msi, GOG installers
- **Archives**: ISO, RAR (single/multi-part), nested archives
- **Installation Methods**:
  - Silent installation with appropriate parameters
  - Archive extraction with installer detection
  - Timeout protection for installations
  - Proper cancellation handling

## Development Status

| Version | Status | Features |
|---------|--------|----------|
| **0.8.3** | ✅ Current | GOG/PC installer consolidation, UI contrast improvements |
| **0.9.0** | 🔄 In Progress | Progress reporting, metadata integration |
| **1.0.0** | 📅 Planned | Multi-language support, additional archive formats |

See the roadmap in **MAINTENANCE.md** for detailed development plans.

## Archive Support Setup

For ISO and RAR archives:

1. Download 7z.exe and UnRAR.exe from their official websites
2. Place them in the Tools directory inside the plugin folder
3. Restart Playnite

## Common Issues

| Issue | Solution |
|-------|----------|
| **No games appear** | • Verify Source Path<br>• Check mapping is enabled<br>• Restart Playnite |
| **Installation fails** | • Check disk space<br>• Verify network access<br>• See detailed log |
| **Archive errors** | • Add 7z.exe/UnRAR.exe to Tools folder |
| **Wrong executable** | • Right-click → EmuLibrary → Select Custom Executable |
| **Text readability** | • Updated in v0.8.3 with contrast improvements |
| **"Unknown profile type"** | • Fixed in v0.8.3<br>• See MIGRATION.md for details |

For more troubleshooting help, see the complete list in **USER_GUIDE.md**.

## Finding Logs

1. Open Playnite and press F12
2. Click "Open application directory"
3. Check logs in Extensions/EmuLibrary folder

## What's New in v0.8.3

• Consolidated GOG and PC installer functionality
• Fixed UI text contrast issues
• Improved error handling for nested archives
• Added migration for existing GOG game entries
• See CHANGELOG.md for details

## Credits

- Original EmuLibrary by [psychonic](https://github.com/psychonic)
- Enhanced by [Claude AI](https://claude.ai/code)

## Support

Get help in the #extension-support channel on [Playnite Discord](https://playnite.link/) or open an issue on GitHub.