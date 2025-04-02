# Current fork goals:

PC Game Installation Support Implementation Plan
Overview
This plan focuses on extending EmuLibrary to handle PC games in various formats:

GOG installers (typically .exe files)
ISO files containing game installers
Multi-RAR archives that need extraction before installation

The goal is to maintain the same repository paradigm that EmuLibrary uses for ROMs: store the installation files on your NAS, and "install" them to your local machine when you want to play them.
Implementation Approach
1. New Game Types
We'll extend the existing "RomType" concept to include new types for PC games:

GogInstaller: For GOG and other standalone installers (.exe, .msi)
IsoImage: For games distributed as ISO files
ArchiveInstaller: For games distributed as multi-part archives (RAR, ZIP, 7z)

2. Required Libraries/Tools
To handle these formats, we'll need to integrate with:

7-Zip for archive extraction (via 7z.dll or process invocation)
Virtual Drive Tool for ISO mounting (WinCDEmu, ImDisk, or similar)
Process Management for running installers with parameters

Specific Implementations

GogInstallerHandler

Detects GOG and other executable installers
Runs installers with appropriate parameters (silent, destination path)
Tracks installation files and shortcuts


IsoImageHandler

Mounts ISO files to virtual drives
Detects and runs installers within the ISO
Unmounts after installation


ArchiveInstallerHandler

Extracts multi-part archives
Handles various archive formats (RAR, ZIP, 7z)
Detects and processes the extracted content (ISO or installer)



4. Installation Process
For each type, the installation process will follow this pattern:

Preparation

Verify source files
Create temporary working directory if needed
For archives: extract to temp directory
For ISOs: mount to virtual drive


Installation

Detect installer within prepared content
Generate installation parameters
Execute installer with progress monitoring
Capture installation path and created files


Cleanup

Remove temporary files
Unmount ISO if mounted
Update game metadata with installation path


Verification

Verify that executable exists at expected location
Test launch if configured to do so



5. UI Enhancements
Add UI elements to support these new game types:

New mapping configuration

Game type selection (GOG, ISO, Archive)
Installation parameters (silent, custom args)
Post-installation actions


Installation progress UI

Multi-stage progress (extraction, mounting, installation)
Better error reporting and recovery options


Game metadata enhancements

Installation type indicator
Installation size tracking
Install date/time



Implementation Steps
Phase 1: Framework and GOG Handler (2-3 weeks)

Create the base GameInstallerHandler class
Implement GogInstallerHandler

Simple .exe detection and execution
Basic parameter handling
Installation tracking


Update UI to support the new handler type
Test with simple GOG installers

Phase 2: ISO Support (2-3 weeks)

Research and implement ISO mounting solution

Evaluate WinCDEmu, ImDisk, PowerShell virtual mount
Implement selected solution


Create IsoImageHandler

Mount/unmount functionality
Installer detection within ISO
Installation process


Update UI for ISO configuration
Test with various ISO files

Phase 3: Archive Support (3-4 weeks)

Implement 7-Zip integration

Add 7z.dll reference or process invocation
Create extraction utilities


Create ArchiveInstallerHandler

Multi-part RAR detection
Extraction process
Content processing (detect ISO or installer)


Update UI for archive configuration
Test with various archive formats and structures

Phase 4: Integration and Polish (2-3 weeks)

Integrate all handlers into the main plugin
Enhance error handling and recovery
Add detailed logging
Improve UI experience
Comprehensive testing

Testing Strategy
For each implementation phase:

Unit Testing

Test detection and handling of each file type
Test parameter generation
Test progress reporting


Integration Testing

Test complete installation flow for each game type
Test with various real-world examples
Test error conditions and recovery


User Acceptance Testing

Test the full process from the user's perspective
Verify UI clarity and usability
Test on different Windows versions

# EmuLibrary

EmuLibrary is a library extension for [Playnite](https://www.playnite.link), an open source video game library manager, focused on emulator ROM management.

While Playnite has had built-in support for scanning paths for installed ROMs and adding them to the library since version 9, EmuLibrary provides alternate functionality.

EmuLibrary treats one or more folders of ROMs/Disc images as a library from which you can "install" games. It can be useful if you have a large collection of emulated games and limited storage where you play them versus where you store them (HTPC vs. NAS, for example). It also is useful for keeping the list of emulated games up to date, and for being able to filter via installed/uninstalled.

Disclaimer: I created this extension for my own usage, and that is still the primary focus. Because of this, many parts of it are still tailored to my personal needs and usage patterns. Despite that, I wanted to share it with others in case it is useful to them. It is still in the process of being (slowly) adapted for more general use.

## Setup

To set it up, you create mappings to combine one of each of the following:

* Emulator - either a built-in emulator or a custom emulator manually added
* Emulator Profile - either a built-in emulator profile or a custom one, out of those supported by the chosen emulator
* Platform - the ROM platform/console, out of those that the emulator profile supports
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

### Yuzu (Beta)

The Yuzu type currently has a beta level quality of support. Some of it is still being reworked. As named, it is very hardcoded to Yuzu specifically, although Ryujinx support reusing most of the same logic will likely come in the future.

To add a functional mapping, make sure that the selected emulator is Yuzu. (It does not need to be the built-in emulator listing for Yuzu. Custom ones, including ones that point to Yuzu EA, etc. will also work). In the source path, loose XCI/NSP/XCZ/NSZ files in the root of the path are considered.

NSP/NSZ files can also be updates and DLC, rather than just games. Unlike with Tinfoil shares, files are not required to include the title id in the filename. Additionally, while destination path must point to a folder that exists, the setting is ignored. Games install into the NAND directory configured in the selected Yuzu emulator profile.

When a game is installed, the latest update and any DLC from the source will also be installed to the Yuzu NAND, in that order (Game, Update if available, each available DLC). Games already installed will be imported, whether or not they exist in the source folder, and will display as installed. As expected, uninstalling a game will remove the game from Yuzu's NAND. (While Yuzu does not support XCZ or NSZ files for launching or installing to NAND, this plugin installs directly to Yuzu's NAND, without relying on the emulator's built-in install functionality)

#### Known Issues

* If the connection to the source folder's storage is unstable, Playnite may crash when when updating the library. This is unlikely to be able to be completely fixed until Playnite uses a newer .NET version (currently being targeted for Playnite 11). Some some mitigations are planned in the meantime, but are not yet implemented.
* If the mapping is disabled or if EmuLibrary update is cancelled before the scan for the mapping completes, game installation for the mapping's games may result in an error message. This will be fixed in a later version of this addon.

## Support

To get help, check out the #extension-support channel on the Playnite Discord, linked at the top of https://playnite.link/

The following files are generally useful for troubleshooting, relative to the folder where Playnite data is stored. For a portable installation, this is the same folder that Playnite is installed to. For non-portable installations, it is in AppData.

* playnite.log
* extensions.log
* library\emulators.db
* library\platforms.db
* ExtensionsData\41e49490-0583-4148-94d2-940c7c74f1d9\config.json
