# EmuLibrary PC Manager - Complete User Guide

This guide provides detailed instructions for setting up and using EmuLibrary PC Manager with Playnite.

## Table of Contents

1. [Installation](#installation)
2. [Basic Configuration](#basic-configuration)
3. [Working with Game Installers](#working-with-game-installers)
4. [Advanced Configuration](#advanced-configuration)
5. [Customization Options](#customization-options)
6. [Troubleshooting](#troubleshooting)
7. [Getting Help](#getting-help)

## Installation

### From Playnite Extensions Browser

1. Open Playnite
2. Navigate to Extensions → Browse
3. Search for "EmuLibrary PC Game Manager"
4. Click Install
5. Restart Playnite when prompted

### Manual Installation

1. Download the latest .pext file from the releases page
2. Open Playnite and navigate to Extensions → Install manually...
3. Select the downloaded .pext file
4. Restart Playnite when prompted

## Basic Configuration

### First-Time Setup

1. Open Playnite Settings (F4)
2. Go to Extensions → EmuLibrary PC Manager
3. You'll see a helpful guide panel on the left with setup information

### Adding Your First Source

1. In the settings panel, click "Add New Mapping" on the right
2. Configure the following fields:
   - **Emulator**: Select any (e.g., "Windows")
   - **Profile**: Select any (e.g., "Default")
   - **Platform**: Select "PC" (or create it if needed)
   - **ROM Type**: Choose "PcInstaller" (unified handler for all installer types)
   - **Source Path**: Browse to your game installer folder (local or network)
   - **Destination Path**: Where you want games installed locally
   - **Enabled**: Check this box
3. Click "Save" and restart Playnite

### Configuring External Tools

For archive support (ISO and RAR files):

1. Download these utilities from their official websites:
   - [7-Zip Command Line](https://www.7-zip.org/download.html) (7z.exe)
   - [UnRAR Command Line](https://www.rarlab.com/rar_add.htm) (UnRAR.exe)
2. Set up the Tools directory:
   - Press F12 → "Open application directory"
   - Navigate to Extensions → EmuLibrary [ID]
   - Create a folder named "Tools" if it doesn't exist
   - Place 7z.exe and UnRAR.exe in this folder
3. Restart Playnite

## Working with Game Installers

### Supported File Types

- **PC Installers**: setup.exe, install.exe, etc.
- **GOG Installers**: setup_*_gog.exe, etc. (automatically detected)
- **ISO Files**: Game installation disc images
- **RAR Archives**: Single or multi-part (game.part1.rar, etc.)
- **Nested Archives**: RAR files containing ISO files with installers

### GOG Integration (New in v0.8.3)

GOG installers are now handled by the unified PcInstaller system:

- Automatic detection based on filename patterns
- GOG-specific silent parameters
- Galaxy integration support
- Migration from previous version GOG games happens automatically

### Installing a Game

1. Find a game in your Playnite library that shows as "Not Installed"
2. Click the "Play" button
3. Confirm installation when prompted
4. The plugin will:
   - Copy files from your source location
   - Extract archives if necessary
   - Run the installer with appropriate parameters
   - Detect the game executable
   - Update Playnite with installation info

### Playing and Managing Games

- **Launch**: Click Play on an installed game
- **Uninstall**: Right-click → Uninstall
- **Custom Executable**: If the wrong executable is detected:
  1. Right-click → EmuLibrary → Select Custom Executable
  2. Browse to the correct .exe file
  3. Your selection will be remembered

## Advanced Configuration

### Multiple Source Directories

You can add multiple mappings for different source folders:

1. Go to Settings → EmuLibrary PC Manager
2. Add a new mapping for each source
3. Use different platforms to organize games (e.g., PC, GOG, etc.)
4. Consider different destination paths for each source

### Optimizing Game Detection

- **Create folders** for each game (plugin uses folder names for metadata)
- **Name folders meaningfully** (e.g., "Game Name [Year]")
- **Group related files** together (base game, DLCs, patches)

### Silent Installation Parameters

The plugin automatically selects parameters for these installer types:

- **InnoSetup**: `/VERYSILENT /SP- /SUPPRESSMSGBOXES /DIR="..." /NOICONS /NORESTART`
- **NSIS**: `/S /D=...`
- **InstallShield**: `/s /v"/qn INSTALLDIR=\"...\"`
- **MSI**: `/quiet /qn TARGETDIR="..."`

## Customization Options

### Game Information

Edit game details in Playnite:

1. Right-click game → Edit
2. Update title, cover image, platform, etc.
3. Changes persist through reinstallation

### Folder Organization

For best results, use this folder structure:

```
/Games/
  ├── Action/
  │   ├── Game Title 1/
  │   │   └── setup_game.exe
  │   └── Game Title 2/
  │       └── game_installer.exe
  ├── RPG/
  │   └── Game Title 3/
  │       ├── game.part1.rar
  │       ├── game.part2.rar
  │       └── game.part3.rar
```

## Troubleshooting

### Common Issues

| Problem | Solution |
|---------|----------|
| **No games detected** | • Check Source Path is correct and accessible<br>• Verify mapping is enabled<br>• Restart Playnite<br>• Make sure files match supported formats |
| **Installation fails** | • Check network share accessibility<br>• Verify sufficient disk space<br>• Check for antivirus interference<br>• View logs for detailed error |
| **Archive extraction fails** | • Verify 7z.exe and UnRAR.exe are in Tools folder<br>• Check for file corruption<br>• Ensure archives are complete |
| **Wrong executable detected** | • Use "Select Custom Executable" option<br>• Check installation path for correct files |
| **Text contrast issues** | • Update to v0.8.3+<br>• Report specific UI elements with issues |

### Diagnosing Problems with Logs

1. Press F12 in Playnite
2. Click "Open application directory"
3. Navigate to Extensions/EmuLibrary
4. Look for error messages in playnite.log
5. Include these details when reporting issues

## Getting Help

If you need assistance:

1. Check README.md for quick solutions
2. See MIGRATION.md for version upgrade issues
3. Join [Playnite Discord](https://playnite.link/) (#extension-support)
4. Open an issue on GitHub with:
   - Your version number
   - Detailed problem description
   - Steps to reproduce
   - Log file contents if relevant