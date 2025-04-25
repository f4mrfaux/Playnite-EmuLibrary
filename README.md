# EmuLibrary

EmuLibrary is a library extension for [Playnite](https://www.playnite.link), an open source video game library manager, originally focused on emulator ROM management and now extended to support PC game installers as well.

While Playnite has had built-in support for scanning paths for installed ROMs and adding them to the library since version 9, EmuLibrary provides alternate functionality.

EmuLibrary treats one or more folders of ROMs/Disc images or PC game installers as a library from which you can "install" games. It can be useful if you have a large collection of emulated games and PC installers with limited storage where you play them versus where you store them (HTPC vs. NAS, for example). It also is useful for keeping the list of games up to date, and for being able to filter via installed/uninstalled.

## Key Features

* **Manage Multiple ROM Sources**: Connect to multiple ROM repositories or disc image collections
* **Support for Various ROM Types**: Handle single files, multi-file games, disc images (ISOs), and PC installers
* **Space-Efficient**: Install only the games you want to play, keeping your local storage optimized
* **Game State Management**: Easily track installed and uninstalled games
* **Platform Integration**: Special handling for different platforms (including GOG, Steam, and Nintendo Switch)

Disclaimer: This extension was originally created for personal usage, and that is still the primary focus. Because of this, many parts of it are still tailored to specific needs and usage patterns. Despite that, it's being shared with others in case it is useful to them. It is still in the process of being adapted for more general use.

## Setup

To set it up, you create mappings to combine one of each of the following:

* Emulator - either a built-in emulator or a custom emulator manually added
* Emulator Profile - either a built-in emulator profile or a custom one, out of those supported by the chosen emulator
* Platform - the ROM platform/console, out of those that the emulator profile supports (PCInstaller type now shows PC platforms regardless of emulator selection)
* RomType - See [Rom Types](#rom-types) below

## Paths

For source and destination, only valid Windows file paths are currently supported. The intended use case is for having the source be an SMB file share (either via UNC path or mapped drive), and the destination be a local path. However, any valid file path should work for either. This means that you can get creative with the source if you have a way to mount alternate remote storage at a Windows file path.

Additionally, for destination paths, relativity to the Playnite folder is preserved if you are using a portable installation of Playnite and your destination is below that folder hierarchically. This means that, for example, if your portable installation is at D:\playnite, and you choose `D:\playnite\rominstall` as your destination, it will be saved internally as `{PlayniteDir}\rominstall`.

## Rom Types

### SingleFile

SingleFile is the simplest type of ROM supported. This is for source folders in which each ROM is fully contained in a single file. It's commonly used for older, non-disc-based systems where the whole ROM consists of a single file. (Ex. .nes, .sfc, .md, etc.). Archive formats are supported as well if the emulator supports them directly. (Ex. .zip)

### MultiFile

With the MultiFile type, each subfolder directly within the source folder is scanned as a potential "ROM". This is for games that have multiple loose files or those that use disc images (ISOs, BIN/CUE, etc.). When installing a MultiFile game, the whole folder is copied.

#### Working with Disc Images (ISOs and other formats)

The MultiFile type is the recommended ROM type for handling disc-based games with ISO files, BIN/CUE files, or other disc image formats. By organizing your disc images in folders (one folder per game), you can manage these games effectively:

* **Single-disc games**: Place the ISO or BIN/CUE files in a folder named after the game
* **Multi-disc games**: Place all disc images in a single folder, preferably with an M3U playlist file

To determine which file is used as the one to tell the emulator to load, all files matching the configured emulator profile's supported extensions are considered. Precedence is by configured image extension list order, and then by alphabetical order. For example, if file names are the same except for `(Disc 1)` versus `(Disc 2)`, the first disc takes precedence. Similarly, if you have `.cue` in the extension list before `.m3u` (as some of the built-in profiles have at the time of writing), `.cue` would be chosen over `.m3u`, which may not be desired for multi-disc games.

### PCInstaller

The PCInstaller type supports PC game installer executables (.exe files). This type is designed for managing native PC games that don't require emulation. It allows you to:

* Scan folders containing PC game installers (.exe files)
* Install these games to a specified location
* Manage PC games alongside your emulated games collection

When using the PCInstaller type, you'll be able to select from PC-related platforms (Windows, Steam, GOG, etc.) regardless of which emulator or profile you've selected. This makes it suitable for organizing PC game installers from various sources.

#### PCInstaller Features and Capabilities

* **Platform Detection**: Automatically identifies and categorizes games from different platforms (GOG, Steam, etc.)
* **Installer Organization**: Manages your collection of installer files separately from installed games
* **Space Efficiency**: Only install the games you want to play, keeping others in your library as "uninstalled"
* **Cross-Platform Support**: Works with various PC gaming platforms without requiring emulation

#### GOG Integration

PCInstaller has special handling for GOG games:

* Automatically detects GOG installers based on file naming patterns
* Attempts to extract GOG game IDs from installer filenames
* Sets the correct GOG platform for proper categorization in Playnite
* Adds metadata tags for better categorization and filtering
* Stores store-specific IDs for future metadata integration

The PCInstaller type preserves the store information and properly categorizes PC games based on their origin (GOG, Steam, etc.), helping with organization and filtering in Playnite.

### Yuzu (Beta)

The Yuzu type currently has a beta level quality of support. Some of it is still being reworked. As named, it is very hardcoded to Yuzu specifically, although Ryujinx support reusing most of the same logic will likely come in the future.

To add a functional mapping, make sure that the selected emulator is Yuzu. (It does not need to be the built-in emulator listing for Yuzu. Custom ones, including ones that point to Yuzu EA, etc. will also work). In the source path, loose XCI/NSP/XCZ/NSZ files in the root of the path are considered.

NSP/NSZ files can also be updates and DLC, rather than just games. Unlike with Tinfoil shares, files are not required to include the title id in the filename. Additionally, while destination path must point to a folder that exists, the setting is ignored. Games install into the NAND directory configured in the selected Yuzu emulator profile.

When a game is installed, the latest update and any DLC from the source will also be installed to the Yuzu NAND, in that order (Game, Update if available, each available DLC). Games already installed will be imported, whether or not they exist in the source folder, and will display as installed. As expected, uninstalling a game will remove the game from Yuzu's NAND. (While Yuzu does not support XCZ or NSZ files for launching or installing to NAND, this plugin installs directly to Yuzu's NAND, without relying on the emulator's built-in install functionality)

#### Known Issues

* If the connection to the source folder's storage is unstable, Playnite may crash when when updating the library. This is unlikely to be able to be completely fixed until Playnite uses a newer .NET version (currently being targeted for Playnite 11). Some some mitigations are planned in the meantime, but are not yet implemented.
* If the mapping is disabled or if EmuLibrary update is cancelled before the scan for the mapping completes, game installation for the mapping's games may result in an error message. This will be fixed in a later version of this addon.

## Usage Workflows

### Working with Disc Image (ISO) Files

Here's a step-by-step workflow for managing a collection of disc images (ISOs):

1. **Organize your disc images**: Create a folder structure where each game has its own folder:
   ```
   N:\games\PS2\
   ├── Final Fantasy X\
   │   └── Final Fantasy X.iso
   ├── God of War\
   │   └── God of War.iso
   ├── Metal Gear Solid 3\
   │   ├── Metal Gear Solid 3 (Disc 1).iso
   │   └── Metal Gear Solid 3 (Disc 2).iso
   ```

2. **Configure EmuLibrary**:
   - Open Playnite and go to Add-ons → Extensions → EmuLibrary → Configure
   - Click "Add Mapping" to create a new mapping
   - Set "Emulator" to your preferred emulator (e.g., PCSX2 for PS2 games)
   - Select an appropriate emulator profile
   - Set "Rom Type" to "MultiFile"
   - Set "Source Path" to your ISO repository (e.g., "N:\games\PS2\")
   - Set "Destination Path" to where you want to install the games (e.g., "D:\Installed Games\PS2\")
   - Select the appropriate platform (e.g., PlayStation 2)

3. **Scan and Install**:
   - Update your Playnite library (F5)
   - Your games will appear as "uninstalled" in the library
   - To install a game, right-click it and select "Install"
   - The entire game folder will be copied to your destination path
   - The game will be marked as "installed" and ready to play

4. **Multi-disc Game Management**:
   - For multi-disc games, create M3U playlist files in the game folder
   - Name the playlist after the game (e.g., "Metal Gear Solid 3.m3u")
   - List each disc in the playlist file in the correct order
   - Configure your emulator profile to prioritize M3U files if needed

### Example: Adding a Repository of PC Games (e.g., GOG games)

Here's a step-by-step workflow for adding a repository of PC game installers using the PCInstaller type:

1. Launch Playnite and go to the main menu.

2. Open EmuLibrary settings (Add-ons → Extensions → EmuLibrary → Configure).

3. In the settings window, click "Add Mapping" to create a new mapping.

4. Configure your mapping:
   - Set "Emulator" to "GOG" (or any placeholder emulator)
   - Set "Profile" to "Choose on startup" 
   - Set "Rom Type" to "PCInstaller"
   - Set "Source Path" to your game repository (e.g., "N:\games\GOG\")
   - Set "Destination Path" to where you want to install the games
   - Select a PC platform (Windows, PC, etc.) which should now be available

5. Click "Save" to save your mapping configuration.

6. In Playnite, update your library (F5) to scan for games.

7. Your games should now appear in your library as "uninstalled" games.

8. To install a game, right-click on it and select "Install". This will copy the installer from your source to the destination and handle the setup.

9. After installation, the game will be marked as "installed" and you can launch it directly from Playnite.

These workflows allow you to maintain central repositories of games while only keeping installed games on your local machine, saving disk space and better organizing your collection.

## Support

To get help, check out the #extension-support channel on the Playnite Discord, linked at the top of https://playnite.link/

The following files are generally useful for troubleshooting, relative to the folder where Playnite data is stored. For a portable installation, this is the same folder that Playnite is installed to. For non-portable installations, it is in AppData.

* playnite.log
* extensions.log
* library\emulators.db
* library\platforms.db
* ExtensionsData\41e49490-0583-4148-94d2-940c7c74f1d9\config.json