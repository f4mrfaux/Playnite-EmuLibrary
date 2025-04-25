# EmuLibrary

EmuLibrary is a library extension for [Playnite](https://www.playnite.link), an open source video game library manager, focused on emulator ROM management and PC game installation. While Playnite has built-in support for scanning installed ROMs, EmuLibrary provides a different approach by treating ROM folders as libraries from which you can selectively "install" games.

> **Attribution**: This is a fork of [psychonic's EmuLibrary](https://github.com/psychonic/Playnite-EmuLibrary), extended by f4mrfaux to add PC game installer support, ISOInstaller, and other improvements.

## Latest Updates (v1.6.0)

### New in v1.6.0:
- **Fixed ISO File Installation**: Resolved issues with ISO files not being found during installation
- **Enhanced Path Resolution**: Added more robust path detection for ISO files with multiple fallback mechanisms
- **Improved Game Scanning**: Better detection of ISO files with various naming conventions
- **User Interface Additions**: Added "Find Missing ISO" context menu option for manually selecting ISO files
- **Automatic Path Recovery**: Added system to recover and repair ISO paths during startup
- **Diagnostic Improvements**: More detailed logging for easier troubleshooting

## Key Features

- **Multiple ROM Types**: Support for various game formats including single-file ROMs, multi-file game folders, PC installers, ISO disk images, and Yuzu Switch games
- **Flexible Storage Management**: Keep your collection on network/external storage and install games to local drives only when needed
- **Smart Content Management**: Automatic detection of base games, updates, DLC, and expansions with parent-child relationships
- **Platform & Store Detection**: Identifies appropriate platforms for games and recognizes content from various digital stores
- **Metadata Integration**: Works with Playnite's built-in metadata system to fetch game information, covers, and backgrounds

## Quick Start Guide

1. Install the EmuLibrary extension from Playnite's add-on browser
2. Access the extension settings: Add-ons → Extensions → EmuLibrary → Configure
3. Add mappings for your game collections based on their type (see [ROM Types](#rom-types) below)
4. Save settings and refresh your library (F5)
5. Your games appear in Playnite as "uninstalled" titles
6. Install games by right-clicking and selecting "Install"
7. Manage your collection through Playnite's standard interface

## ROM Types and Workflows

EmuLibrary supports several types of games, each with a unique workflow:

### SingleFile ROM Workflow

Best for: NES, SNES, Genesis and other classic console ROMs stored as individual files

1. **Setup**: Create a SingleFile mapping pointing to your ROM folder
2. **Scanning**: EmuLibrary finds all compatible ROM files based on file extensions
3. **Installation**: When you install a ROM, it's copied from the source to your destination folder
4. **Play**: The game launches through your selected emulator using the local ROM copy

### MultiFile ROM Workflow

Best for: Multi-disc games or ROMs with multiple files (CD-based systems like PlayStation)

1. **Setup**: Create a MultiFile mapping pointing to a folder containing game subfolders
2. **Scanning**: Each subfolder is treated as a single game
3. **Installation**: The entire folder is copied to your destination
4. **Play**: The primary file (determined by extension priority) is used to launch the game

### ISOInstaller Workflow

Best for: PC games distributed as disc images (.iso, .bin/.cue, etc.)

1. **Setup**: Create an ISOInstaller mapping using the standard mapping table in settings
2. **Scanning**: EmuLibrary finds all compatible disc images (.iso, .bin, .img, etc.)
3. **Installation Process**:
   - When you click "Install Game" on an ISO game, the system mounts the ISO directly from its source location
   - The ISO is mounted as a virtual drive using Windows' built-in mounting capability
   - You'll see a list of executables found on the ISO and can select which installer to run
   - The installer runs and you complete the normal game installation process
   - After installation completes, you select the directory where you installed the game
   - Next, you choose which executable from that directory is the main game launcher
   - Finally, the ISO is unmounted
   - The game now appears as "installed" in your library
4. **Play**: Clicking "Play" launches the game directly using the executable you selected, without needing the original ISO

### PCInstaller Workflow

Best for: Windows game installers (.exe files)

1. **Setup**: Create a PCInstaller mapping pointing to your installer collection
2. **Scanning**: EmuLibrary finds all executable installers
3. **Installation**: The installer is copied locally and run, then you specify the installation directory
4. **Play**: The game launches directly using the selected game executable

### Yuzu (Nintendo Switch) Workflow

Best for: Nintendo Switch games with Yuzu emulator (Beta feature)

1. **Setup**: Create a Yuzu mapping pointing to your Switch ROM collection
2. **Scanning**: EmuLibrary finds XCI/NSP/XCZ/NSZ files and detects games, updates, and DLC
3. **Installation**: Games are installed directly to Yuzu's NAND directory
4. **Play**: Games launch through the Yuzu emulator

## Detailed Setup

To set up EmuLibrary, you create mappings to combine one of each of the following:

* **Emulator** - either a built-in emulator or a custom emulator manually added (optional for PCInstaller and ISOInstaller types)
* **Emulator Profile** - either a built-in emulator profile or a custom one (optional for PCInstaller and ISOInstaller types)
* **Platform** - the game platform/console (PCInstaller and ISOInstaller default to "PC" if none selected)
* **RomType** - The type of game files you're adding (see [ROM Types](#rom-types) above)
* **Source Path** - Where your original game files are stored
* **Destination Path** - Where installed games will be copied to

## Path Configuration

For source and destination paths, both local and network paths are supported:

- **Source Path**: Can be a local folder or network location (UNC path or mapped drive)
- **Destination Path**: Typically a local folder for better performance
- **Relative Paths**: If using a portable Playnite installation, paths below the Playnite folder are stored relatively

## Advanced Features

### DLC, Updates, and Expansions Management

EmuLibrary intelligently handles additional content:

1. **Automatic Detection**: The system recognizes add-on content by analyzing file and folder names
2. **Content Classification**:
   - Base Games: Main game installations
   - Updates/Patches: Game updates with version tracking
   - DLC: Downloadable content packages
   - Expansions: Larger content additions

3. **Parent-Child Relationships**: 
   - Add-on content is automatically linked to parent games
   - Relationships are visible in Playnite through dependencies
   - You can see all installed add-ons for each base game

4. **Version Tracking**:
   - Version information is extracted from filenames when available
   - Install newer versions to keep games up to date

### Store Integration

For games from digital stores, EmuLibrary provides special handling:

- **GOG Game Detection**: Automatically identifies GOG installers
- **Store ID Preservation**: Maintains store-specific IDs for metadata matching
- **Platform Assignment**: Sets appropriate platform tags (GOG, Steam, etc.)

### Content Organization Recommendations

For best results with your game collection:

- **Consistent Naming**: Use standard naming patterns for games and related content
- **Folder Structure**: Group related content (game + DLC + updates) in the same directory when possible
- **Common Patterns**: Use recognizable patterns like "Game Name + DLC Name", "Game Name - Update v1.2"

### Metadata Integration

EmuLibrary works with Playnite's built-in metadata system:

- **Automatic Metadata**: When enabled, Playnite requests metadata for imported games
- **Manual Download**: You can also download metadata through Playnite's UI
- **Metadata Sources**: Uses existing Playnite metadata extensions like SteamGridDB, IGDB, etc.
- **Available Information**: Fetches covers, backgrounds, descriptions, release dates, genres, etc.

### Multi-Disc Game Handling

For collections with multi-disc games:

- **Disc Detection**: The system recognizes related discs through standard naming patterns
- **Installation**: Select the primary disc for installation; related discs are handled automatically
- **Access**: All discs are accessible from the installed game

## Troubleshooting

### Common Issues

- **Games Not Appearing**: Check file extensions and ensure your source path is accessible
- **Installation Errors**: Verify that destination paths are valid and writeable
- **Emulator Issues**: Make sure emulators are properly configured in Playnite
- **ISO Mounting Problems**: Requires Windows 8 or higher with built-in ISO mounting capabilities
- **ISO Games Not Visible**: Ensure you've set the correct platform (PC) and that the ISO files have proper extensions
- **ISO Files Not Found**: If you see an error about ISO files not being found during installation, right-click the game and use "Find Missing ISO..." to manually select the file
- **Missing Context Menu**: If a newly added feature like "Find Missing ISO..." isn't appearing in the context menu, restart Playnite

### Special Notes for ISO and PC Installers

1. **Mounting Requirements**: ISO mounting requires Windows 8 or newer with built-in mounting capabilities.
2. **File Extensions**: ISO scanning supports various disc image formats including .iso, .bin, .img, .cue, .nrg, .mds, and .mdf.
3. **Emulator Mapping**: For PCInstaller and ISOInstaller mappings, you must select an emulator even though none is needed. Create a dummy emulator called "EmuLib-PC" and use it for these mappings.
4. **Consistent Interface**: Both PCInstaller and ISOInstaller use the same standard mapping table interface and "Install Game" menu option for a consistent experience.
5. **Command-Line Arguments**: You can add command-line arguments to game launches by editing the play action after installation.
6. **Installation Interruptions**: If an installation is interrupted, you might need to manually unmount any ISO files using Windows Explorer.
7. **Missing ISO Files**: If an ISO file can't be found during installation, use the "Find Missing ISO..." context menu option to manually select the ISO file.
8. **Path Flexibility**: The system can now find ISO files even if they've been moved, as long as they remain in one of your configured mapping folders.

### Log Files

For troubleshooting, check these log files (relative to Playnite data directory):

* playnite.log
* extensions.log
* ExtensionsData\41e49490-0583-4148-94d2-940c7c74f1d9\config.json

## Support

To get help, check out the #extension-support channel on the [Playnite Discord](https://playnite.link/)..