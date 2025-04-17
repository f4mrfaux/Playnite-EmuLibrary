# EmuLibrary

EmuLibrary is a library extension for [Playnite](https://www.playnite.link), an open source video game library manager, originally focused on emulator ROM management and now extended to support PC game installers as well.

While Playnite has had built-in support for scanning paths for installed ROMs and adding them to the library since version 9, EmuLibrary provides alternate functionality.

EmuLibrary treats one or more folders of ROMs/Disc images or PC game installers as a library from which you can "install" games. It can be useful if you have a large collection of emulated games and PC installers with limited storage where you play them versus where you store them (HTPC vs. NAS, for example). It also is useful for keeping the list of games up to date, and for being able to filter via installed/uninstalled.

Disclaimer: This extension was originally created for personal usage, and that is still the primary focus. Because of this, many parts of it are still tailored to specific needs and usage patterns. Despite that, it's being shared with others in case it is useful to them. It is still in the process of being adapted for more general use.

## Setup

To set it up, you create mappings to combine one of each of the following:

* Emulator - either a built-in emulator or a custom emulator manually added (optional for PCInstaller, ISOInstaller, and ArchiveInstaller types)
* Emulator Profile - either a built-in emulator profile or a custom one, out of those supported by the chosen emulator (optional for PCInstaller, ISOInstaller, and ArchiveInstaller types)
* Platform - the ROM platform/console, out of those that the emulator profile supports (PCInstaller, ISOInstaller, and ArchiveInstaller types show PC platforms regardless of emulator selection, and can work without a platform selected)
* RomType - See [Rom Types](#rom-types) below

## Paths

For source and destination, only valid Windows file paths are currently supported. The intended use case is for having the source be an SMB file share (either via UNC path or mapped drive), and the destination be a local path. However, any valid file path should work for either. This means that you can get creative with the source if you have a way to mount alternate remote storage at a Windows file path.

Additionally, for destination paths, relativity to the Playnite folder is preserved if you are using a portable installation of Playnite and your destination is below that folder hierarchically. This means that, for example, if your portable installation is at D:\playnite, and you choose `D:\playnite\rominstall` as your destination, it will be saved internally as `{PlayniteDir}\rominstall`.

## Rom Types

### SingleFile

SingleFile is the simplest type of ROM supported. This is for source folders in which each ROM is fully contained in a single file. It's commonly used for older, non-disc-based systems where the whole ROM consists of a single file. (Ex. .nes, .sfc, .md, etc.). Archive formats are supported as well if the emulator supports them directly. (Ex. .zip)

### MultiFile

With the MultiFile type, each subfolder directly within the source folder is scanned as a potential "ROM". This is for games that have multiple loose files. (Ex. one or more .bin/.cue, with optional .m3u). When installing a MultiFile game, the whole folder is copied. 

To determine which file is used as the one to tell the emulator to load, all files matching the configured emulator profile's supported extensions are considered. Precedence is by configured image extension list order, and then by alphabetical order. For example, if file names are the same except for `(Disc 1)` versus `(Disc 2)`, the first disc takes precedence. Similarly, if you have `.cue` in the extension list before `.m3u` (as some of the built-in profiles have at the time of writing), `.cue` would be chosen over `.m3u`, which may not be desired for multi-disc games.

### ISOInstaller

The ISOInstaller type supports PC game installations from disc images (ISO, BIN/CUE, etc). Like PCInstaller, this type doesn't require an emulator or platform selection. When no platform is selected, it defaults to "PC". This type allows you to:

* Scan folders containing disc images (.iso, .bin/.cue, .img, etc.)
* Mount the disc images for installation
* Install games to a specified location
* Manage disc-based PC games alongside your other games
* Handle DLC, updates, and expansions from disc images

ISOInstaller has similar features to PCInstaller, including special handling for GOG disc images and store detection, as well as complete support for DLC, updates, and expansions with parent-child relationships.

### PCInstaller

The PCInstaller type supports PC game installer executables (.exe files). This type is designed for managing native PC games that don't require emulation. It doesn't require an emulator or platform selection - when no platform is selected, it defaults to "PC". It allows you to:

* Scan folders containing PC game installers (.exe files)
* Install these games to a specified location
* Manage PC games alongside your emulated games collection
* Handle DLC, updates, and expansions with parent-child relationships

When using the PCInstaller type, you'll be able to select from PC-related platforms (Windows, Steam, GOG, etc.) regardless of which emulator or profile you've selected. This makes it suitable for organizing PC game installers from various sources.

#### GOG Integration

PCInstaller has special handling for GOG games:

* Automatically detects GOG installers based on file naming patterns
* Attempts to extract GOG game IDs from installer filenames
* Sets the correct GOG platform for proper categorization in Playnite
* Adds metadata tags for better categorization and filtering
* Stores store-specific IDs for future metadata integration

The PCInstaller type preserves the store information and properly categorizes PC games based on their origin (GOG, Steam, etc.), helping with organization and filtering in Playnite.

#### DLC, Updates, and Expansions Support

PCInstaller provides comprehensive support for additional content types:

* **Automatic Content Detection**: Intelligently identifies updates, DLC, and expansions by analyzing file and directory names
* **Content Type Classification**:
  - **Base Games**: The main game installation
  - **Updates/Patches**: Game updates and patches with version tracking
  - **DLC**: Downloadable content packages
  - **Expansions**: Larger content additions (treated as a special type of DLC)

* **Parent-Child Relationships**: 
  - Automatically links DLC and updates to their parent games
  - Shows relationships in the Playnite UI via dependencies
  - Tracks installed addons for each base game

* **Version Tracking**:
  - Detects and extracts version information from filenames
  - Displays version information in game metadata
  - Helps organize multiple versions of the same game

* **Smart Naming**:
  - Enhances displayed names with content type and version information
  - Maintains consistent naming conventions across different content types
  - Makes related content easily identifiable in your library

### ArchiveInstaller

The ArchiveInstaller type extends the functionality of ISOInstaller by handling archives (ZIP, RAR, 7z) that contain disc images with installers. This type is designed for PC game installations from archived disc images, which is common for large collections. This type allows you to:

* Scan folders containing archives (.zip, .rar, .7z) that have ISOs or other disc images inside
* Handle multi-part archives (especially split RAR files)
* Extract the archives to temporary storage
* Mount the contained disc images
* Install games to a specified location
* Manage archived disc-based PC games alongside your other games
* Handle DLC, updates, and expansions from archived disc images

Just like PCInstaller and ISOInstaller, the ArchiveInstaller type fully supports content type detection and relationship management for DLC, updates, and expansions - all with the added benefit of archive handling.

#### Requirements
* 7-Zip executable must be installed and available in your PATH
* For multi-part archives (like rar, r00, r01, etc.), all parts need to be in the same directory
* Password-protected archives are supported (you'll need to provide the password in the game properties)

#### Features
* Smart multi-disc detection for game collections
* Automatic handling of multi-part archives
* Parallel extraction for large archives
* Automatic retry mechanism for corrupted archives
* Detailed progress reporting during extraction
* Intelligent ISO selection for multi-disc games
* Support for nested archive structures
* Proper cleanup of temporary files

#### Installation Workflow
The ArchiveInstaller implements a multi-step installation workflow:
1. Import archive to local temp storage
2. Extract archive locally using 7-Zip
3. Find and select ISO files from extracted content
4. Mount ISO file
5. Run installer from mounted ISO
6. Clean up temp files after installation

All operations are performed on locally imported copies of files to ensure reliability, especially over network connections.

#### Troubleshooting ArchiveInstaller

**Common Issues and Solutions:**

1. **7-Zip Not Found Error**
   - Ensure 7-Zip is installed on your system
   - Make sure the 7-Zip executable (7z.exe) is in your system PATH
   - The extension searches in common installation locations, but may not find custom installations

2. **Archive Extraction Fails**
   - Check if the archive is password-protected (provide password in game properties)
   - Verify the archive isn't corrupted by testing with 7-Zip directly
   - For multi-part archives, ensure all parts are available in the same directory
   - The extension will automatically retry extraction up to 3 times for potentially recoverable errors

3. **No ISO Found After Extraction**
   - Make sure the archive actually contains a supported disc image (.iso, .bin/.cue, etc.)
   - Some archives may have complex directory structures - the extension scans all subdirectories
   - Check if the archive uses an unusual or unsupported disc image format

4. **ISO Mounting Fails**
   - Requires administrator rights to mount disc images
   - Ensures the extracted ISO isn't corrupted
   - Windows may prevent mounting certain disc images due to security restrictions

5. **Performance Considerations**
   - Large archives (>500MB) will use parallel extraction if available
   - Network imports can be slow - consider using the asset caching feature
   - Extraction can be CPU and disk intensive - ensure adequate system resources

For persistent issues, check the extension log files located in the Playnite data directory.

### Yuzu (Beta)

The Yuzu type currently has a beta level quality of support. Some of it is still being reworked. As named, it is very hardcoded to Yuzu specifically, although Ryujinx support reusing most of the same logic will likely come in the future.

To add a functional mapping, make sure that the selected emulator is Yuzu. (It does not need to be the built-in emulator listing for Yuzu. Custom ones, including ones that point to Yuzu EA, etc. will also work). In the source path, loose XCI/NSP/XCZ/NSZ files in the root of the path are considered.

NSP/NSZ files can also be updates and DLC, rather than just games. Unlike with Tinfoil shares, files are not required to include the title id in the filename. Additionally, while destination path must point to a folder that exists, the setting is ignored. Games install into the NAND directory configured in the selected Yuzu emulator profile.

When a game is installed, the latest update and any DLC from the source will also be installed to the Yuzu NAND, in that order (Game, Update if available, each available DLC). Games already installed will be imported, whether or not they exist in the source folder, and will display as installed. As expected, uninstalling a game will remove the game from Yuzu's NAND. (While Yuzu does not support XCZ or NSZ files for launching or installing to NAND, this plugin installs directly to Yuzu's NAND, without relying on the emulator's built-in install functionality)

#### Known Issues

* If the connection to the source folder's storage is unstable, Playnite may crash when when updating the library. This is unlikely to be able to be completely fixed until Playnite uses a newer .NET version (currently being targeted for Playnite 11). Some some mitigations are planned in the meantime, but are not yet implemented.
* If the mapping is disabled or if EmuLibrary update is cancelled before the scan for the mapping completes, game installation for the mapping's games may result in an error message. This will be fixed in a later version of this addon.
* For PCInstaller, ISOInstaller and ArchiveInstaller mappings, you must select an emulator even though these types don't technically require one. If you leave the emulator field empty, you'll see warnings like "Emulator 00000000-0000-0000-0000-000000000000 not found, skipping" and games won't be imported. Create a dummy emulator in Playnite called "EmuLib-PC" and use it for these mappings.

## Usage Workflow

### Example: Adding a Repository of PC Games (e.g., GOG games)

Here's a step-by-step workflow for adding a repository of PC game installers using the PCInstaller type:

1. Launch Playnite and go to the main menu.

2. Open EmuLibrary settings (Add-ons → Extensions → EmuLibrary → Configure).

3. In the settings window, click "Add Mapping" to create a new mapping.

4. Configure your mapping:
   - Set "Rom Type" to "PCInstaller"
   - Set "Source Path" to your game repository (e.g., "N:\games\GOG\")
   - Set "Destination Path" to where you want to install the games
   - Select a PC platform (Windows, PC, etc.) which should now be available - Optional for PCInstaller (defaults to "PC")
   - IMPORTANT: For PCInstaller, ISOInstaller, and ArchiveInstaller types, you MUST select an emulator even though one isn't technically needed. Create a dummy emulator called "EmuLib-PC" in Playnite's emulator configuration (Library → Configure Emulators) and select it here. Leaving the emulator field empty will cause scanning issues.

5. Click "Save" to save your mapping configuration.

6. In Playnite, update your library (F5) to scan for games.

7. Your games should now appear in your library as "uninstalled" games.

8. To install a game, right-click on it and select "Install". This will copy the installer from your source to the destination and handle the setup.

9. After installation, the game will be marked as "installed" and you can launch it directly from Playnite.

This workflow allows you to maintain a central repository of game installers while only keeping installed games on your local machine.

### Example: Adding a Repository of Archived ISO Games

Here's how to set up a mapping for archived ISO games:

1. Launch Playnite and go to the main menu.

2. Open EmuLibrary settings (Add-ons → Extensions → EmuLibrary → Configure).

3. In the settings window, click "Add Mapping" to create a new mapping.

4. Configure your mapping:
   - Set "Rom Type" to "ArchiveInstaller"
   - Set "Source Path" to your archive repository (e.g., "N:\games\ArchivedISOs\")
   - Set "Destination Path" to where you want to install the games
   - Select a PC platform (Windows, PC, etc.) or leave empty (defaults to "PC")
   - Select a dummy emulator as explained above

5. Click "Save" to save your mapping configuration.

6. Make sure 7-Zip is installed and available in your PATH.

7. In Playnite, update your library (F5) to scan for games.

8. Your archived games will appear in your library as "uninstalled" games.

9. To install a game, right-click on it and select "Install". The system will:
   - Import the archive to local temp storage
   - Extract the archive using 7-Zip
   - Find and mount the ISO file
   - Run the installer
   - Clean up temp files after installation

10. After installation, the game will be marked as "installed" and you can launch it directly from Playnite.

This workflow is particularly useful for large collections of archived disc images, especially when stored on network storage.

### Advanced Usage: Multi-disc Archive Processing

For multi-disc games stored in archives:

1. Organize your archives using standard disc naming conventions:
   - Use patterns like "Game Name (Disc 1).iso", "Game Name (Disc 2).iso"
   - Alternative formats like "Game Name - Disc 1.iso" are also recognized
   - Consistent naming helps the system identify related discs

2. When installing multi-disc games:
   - The system will automatically detect related discs
   - The first disc will be selected by default for installation
   - You can manually select a different disc if needed
   - After installation, all discs will be accessible from the installed game

3. For multi-part RAR archives:
   - Ensure all parts (.rar, .r00, .r01, etc.) are in the same directory
   - The scanner will automatically group them together
   - The entire set will be imported together to local storage before processing
   - Processing is done as a single unit for reliable extraction

4. For nested archives (archives inside archives):
   - The system will extract the outer archive first
   - Then scan for and extract any inner archives
   - Finally, it will locate ISO files in the fully extracted content

This functionality is particularly useful for complex game collections that use advanced archiving techniques.

### Example: Managing DLC, Updates, and Expansions

EmuLibrary provides comprehensive support for managing game content relationships across all PC game types:

1. **Content Organization Recommendations**:
   - Organize files with consistent naming conventions for reliable detection
   - Use common patterns like "Game Name + DLC Name", "Game Name - Update v1.2", etc.
   - Group related content in the same directory when possible

2. **Content Detection and Relationships**:
   - The system automatically detects content types by analyzing file and folder names
   - It recognizes common indicators like "DLC", "Expansion", "Update", "Patch", "v1.2", etc.
   - Base games are automatically linked with their related content

3. **Installation Workflow**:
   - Installing a base game first makes it easier to relate DLC and updates
   - When installing DLC or updates, the system will automatically link to the parent game
   - You can view relationship information in game properties and via dependencies in the UI

4. **Managing Version Updates**:
   - Install updates to keep your games current
   - The system tracks version information when available
   - Base games show information about all installed add-ons

5. **Display and Organization**:
   - Content is displayed with appropriate type indicators
   - Updates show version information when available
   - DLC and expansions show descriptive names
   - Related content is grouped logically in the UI via dependencies

This system provides a comprehensive way to manage complex game libraries with multiple content types across all PC installation methods.

## Support

To get help, check out the #extension-support channel on the Playnite Discord, linked at the top of https://playnite.link/

The following files are generally useful for troubleshooting, relative to the folder where Playnite data is stored. For a portable installation, this is the same folder that Playnite is installed to. For non-portable installations, it is in AppData.

* playnite.log
* extensions.log
* library\emulators.db
* library\platforms.db
* ExtensionsData\41e49490-0583-4148-94d2-940c7c74f1d9\config.json