# EmuLibrary PC Game Manager

EmuLibrary PC Game Manager is a fork of the [EmuLibrary](https://github.com/psychonic/Playnite-EmuLibrary) plugin for [Playnite](https://www.playnite.link) that has been repurposed to manage PC game installers rather than just emulator ROMs. This plugin brings GameVault-like functionality to Playnite without requiring a dedicated server.

## Project Overview

This project transforms the original EmuLibrary from a ROM management tool into a comprehensive PC game installer management solution. The core concept remains the same: store your game installers on a network share (NAS, SMB, etc.) and only install them locally when you want to play them, saving local disk space.

### Key Features

- **PC Game Installer Support**: Detect and manage various PC game installers (.exe, .msi)
- **GOG Installer Integration**: Specialized support for GOG game installers with silent installation
- **Advanced Archive Handling**:
  - ISO file extraction and installer detection
  - Multi-part RAR archive support (e.g., game.part1.rar, game.part2.rar)
  - Nested archives (RAR archives containing ISO files with installers)
- **Automatic Executable Detection**: Intelligently finds the correct game executable after installation
- **Network Storage Integration**: Works with SMB shares and other network storage solutions
- **Playnite Integration**: Adds games to your Playnite library with proper metadata

## How It Works

1. **Store your installers** on a network share or local folder
2. **Configure the plugin** to scan that location for PC game installers
3. **Browse games in Playnite** that appear as "not installed"
4. **Install on demand** by clicking "Play" on a game you want to play
5. The plugin will:
   - Copy or extract the installer from your network share
   - Handle archives (RAR, ISO) automatically if needed
   - Run the installer silently with the appropriate parameters
   - Detect the installed game executable using intelligent pattern matching
   - Update Playnite with the installation information
6. **Launch the game** directly from Playnite
7. **Error handling** ensures graceful recovery if problems occur

## Setup Guide

### Quick Start (5-Minute Setup)

1. **Install the plugin** in Playnite (Extensions → Browse → EmuLibrary PC Game Manager)
2. **Open the settings**:
   - Go to Settings → EmuLibrary PC Game Manager
   - You'll see a helpful guide panel with setup instructions
3. **Add a new mapping**:
   - **Emulator**: Select any emulator from the dropdown (it doesn't matter which one)
   - **Profile**: Select any profile from the dropdown
   - **Platform**: Select "PC" (or create it if it doesn't exist)
   - **Rom Type**: Choose "PcInstaller" for generic installers or "GogInstaller" for GOG installers
   - **Source Path**: Browse to your network share or folder containing game installers
   - **Destination Path**: Choose where you want games to be installed locally
   - **Enabled**: Make sure this is checked
4. **Click "Save" and restart Playnite**
5. **Your games will appear** in your library as uninstalled games

### Folder Structure Tips

For best results, organize your game installers in folders like this:

```
/Games/
  ├── PC/
  │   ├── The Witcher 3/
  │   │   └── setup_the_witcher_3_goty_2.0.0.47.exe
  │   ├── Cyberpunk 2077/
  │   │   └── setup_cyberpunk_2077_1.63.exe
  │   └── Baldur's Gate 3/
  │       └── setup_baldurs_gate_3_patch_6.exe
  ├── GOG/
  │   ├── Disco Elysium/
  │   │   └── setup_disco_elysium_the_final_cut_2.0.0.13.exe
  │   └── Pathfinder Wrath of the Righteous/
  │       └── setup_pathfinder_wrath_of_the_righteous_2.1.5.exe
```

The plugin will use folder names for better game identification!

### Advanced Features

#### Archive Support

For ISO and RAR archive support:

1. Download 7z.exe and UnRAR.exe from their official sources
2. Place them in one of these locations (checked in this order):
   - The Tools directory inside the plugin folder (recommended)
   - The plugin's root directory
   - The parent directory's Tools folder
   - Your system PATH
3. The plugin will automatically detect and use these tools for archive handling
4. Enhanced error handling ensures graceful fallbacks if extraction encounters problems

#### Installation Options

Configure default installation behavior in Settings:

- **Auto-detect installers**: Automatically detect installer types based on file properties
- **Default installation location**: Where games will be installed by default
- **Create game-specific directories**: Create a folder for each game during installation

## Supported Formats

### Installer Types

- **Executable Installers** (.exe, .msi)
- **GOG Installers** (with special detection for GOG Galaxy games)
- **ISO Files** containing game installers
- **RAR Archives** (single and multi-part)

### Installation Methods

The plugin supports various installation methods:

- **Silent Installation** with appropriate parameters for each installer type:
  - InnoSetup installers: `/VERYSILENT /SP- /SUPPRESSMSGBOXES /DIR="destination" /NOICONS /NORESTART`
  - NSIS installers: `/S /D=destination`
  - InstallShield installers: `/s /v"/qn INSTALLDIR=\"destination\""`
  - MSI installers: `/quiet /qn TARGETDIR="destination"`
- **Archive Extraction** and installer detection:
  - Automatic detection of installation executables within extracted content
  - Advanced filtering to ignore updaters, uninstallers, and utility programs
  - Multiple fallback methods if primary detection fails
- **Process Management**:
  - Timeout protection (10-minute limit) for hung installations
  - Proper cancellation handling when user aborts installation

## Development Progress

The project has successfully implemented:

- ✅ Generic PC installer support and detection
- ✅ GOG installer specialized support
- ✅ Network share integration
- ✅ Silent installation parameter detection
- ✅ Post-installation executable detection
- ✅ Multi-RAR archive handling
- ✅ ISO file support

Current enhancements include:

- ✅ Improved user interface with helpful guides
- ✅ Enhanced error handling and user notifications
- ✅ Performance optimizations with caching
- ✅ Better folder-based game naming

Future plans include:

- Interactive installation progress reporting
- Better error recovery options
- Support for more archive formats
- Additional metadata integration improvements

## Testing Status

| Feature | Tested | Notes |
|---------|--------|-------|
| Basic PC installer detection | ✅ | Tested with various setup.exe files |
| GOG installer detection | ✅ | Works with standard GOG offline installers |
| Silent installation parameters | ✅ | Confirmed working with InnoSetup, NSIS, MSI |
| Network share access | ✅ | Tested with SMB shares |
| Post-installation executable detection | ✅ | Successfully finds main game EXEs |
| Multi-part RAR extraction | ⚠️ | Requires external UnRAR.exe in Tools directory or PATH |
| ISO file handling | ⚠️ | Requires external 7z.exe in Tools directory or PATH |
| Nested archives (RAR → ISO → installer) | ⚠️ | Basic support implemented, needs more testing |
| Installation cancellation | ✅ | Process termination now properly handled |
| Error recovery | ✅ | Improved with graceful fallbacks and logging |
| Large installer support (>4GB) | ⚠️ | Should work but needs more testing |
| Installation to different drives | ✅ | Works with various destination paths |
| .NET Framework 4.6.2 compatibility | ✅ | Fixed compatibility issues |
| Timeout protection | ✅ | Added 10-minute timeout for hung installers |
| External tool detection | ✅ | Multiple fallback paths for finding required tools |

## Credits

- Original EmuLibrary by [psychonic](https://github.com/psychonic)
- This fork includes contributions from [Claude AI](https://claude.ai/code)

## Troubleshooting

### Common Issues and Solutions

| Issue | Solution |
|-------|----------|
| **No games appear in library** | - Verify your Source Path is correct<br>- Check that the mapping is enabled<br>- Restart Playnite<br>- Make sure you have suitable installer files (.exe, .msi, etc.) |
| **Installation fails silently** | - Check if you have enough disk space<br>- Ensure the network share is accessible<br>- Try running the installer manually to see if it has specific requirements |
| **"Archive tools not found" error** | - Download 7z.exe and UnRAR.exe<br>- Place them in the Tools folder of the plugin<br>- Restart Playnite |
| **Cannot detect game executable** | - The plugin will still install the game but may not find the launcher<br>- Right-click the game in Playnite → Properties → Add the path to the executable manually |
| **Slow scanning of network shares** | - Organize games in folders for faster scanning<br>- Use a wired network connection for better performance<br>- Consider setting up a local cache on a faster drive |

### Log Files

If you're experiencing issues, check the Playnite log files:
1. Open Playnite
2. Press F12 to open the diagnostic tools
3. Click "Open application directory"
4. Open the "Extensions" folder and then the EmuLibrary folder
5. Check playnite.log for error messages

## Support

To get help, check out the #extension-support channel on the [Playnite Discord](https://playnite.link/), or open an issue on GitHub.