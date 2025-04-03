# EmuLibrary PC Manager - User Guide

## Introduction

EmuLibrary PC Manager helps you manage PC game installers within Playnite. This guide covers how to set up and use the extension effectively.

## Installation

1. Download the latest .pext file from the releases page
2. Open Playnite and navigate to Extensions → Install manually...
3. Select the downloaded .pext file
4. Restart Playnite when prompted

## Basic Configuration

### Setting Up Your First Source

1. Go to Settings → EmuLibrary PC Manager
2. Click "Add New Mapping"
3. Configure the following:
   - **Emulator**: Select any emulator (e.g., "Windows")
   - **Profile**: Select any profile (e.g., "Default")
   - **Platform**: Select "PC" (or create it if needed)
   - **ROM Type**: Select "PcInstaller" (handles both generic and GOG installers)
   - **Source Path**: The folder where your game installers are stored
   - **Destination Path**: Where you want games to be installed
   - **Enabled**: Check this box to activate the mapping
4. Click "Save" and restart Playnite

## Working with Game Installers

### Supported Installer Types

The extension now handles all types of installers through a unified system:

- **Standard PC Installers**: Setup files like setup.exe, install.exe
- **GOG Installers**: Automatically detected and handled with correct parameters
- **ISO Files**: Mounted and installer extracted automatically
- **RAR Archives**: Extracted and processed (including multi-part RARs)
- **Nested Archives**: For example, RAR files containing ISO files

### GOG Games Integration (New in v0.8.3)

Previously, GOG installers required a separate "GogInstaller" ROM type. As of v0.8.3:

- Use the standard "PcInstaller" ROM type for all installers
- GOG installers are automatically detected based on filename patterns:
  - setup_*_gog.exe
  - gog*setup.exe
  - setup_*_[version].exe
  - installer_*.exe
- GOG-specific silent parameters are automatically applied
- GOG Galaxy integration is properly handled

### Installation Process

1. Browse your Playnite library for games from your configured source
2. Click on a game that shows as "Not Installed"
3. Click the "Play" button
4. Confirm that you want to install the game
5. The plugin will:
   - Copy the installer from your source location
   - Handle any archive extraction if needed
   - Run the installer with appropriate silent parameters
   - Detect the main game executable
   - Update Playnite with installation information

## Advanced Features

### Archive Handling

For ISO and RAR support:

1. Download 7z.exe and UnRAR.exe from their official websites
2. Create a "Tools" folder in your extension directory:
   - Find your extension directory by pressing F12 → "Open application directory"
   - Navigate to Extensions → [EmuLibrary Extension ID]
   - Create a folder named "Tools"
3. Place the downloaded executables in the Tools folder
4. Restart Playnite

### Customizing Game Information

If the plugin doesn't detect the correct game information:

1. Right-click the game in your Playnite library
2. Go to Edit
3. Update the title, cover image, or other details
4. Your customizations will persist even if you reinstall the game

### Selecting Custom Executables

If the plugin doesn't automatically detect the correct game executable:

1. Right-click the game in your Playnite library
2. Select EmuLibrary → Select Custom Executable
3. Browse to the correct executable file
4. This selection will be remembered for future launches

## Troubleshooting

### Installation Issues

If games fail to install:

1. Check that your source path is accessible
2. Ensure you have enough disk space at the destination
3. Verify that 7z.exe and UnRAR.exe are available (for archive handling)
4. Check the Playnite logs (F12 → "Open application directory" → check logs)

### UI Readability Issues

As of v0.8.3, we've improved text contrast in the settings UI. If you still have issues:

1. Make sure you've updated to v0.8.3 or newer
2. If text is still difficult to read, please report the specific UI element on GitHub

### Migration from Previous Versions

When updating from versions before 0.8.3:

1. Your existing GOG games will be automatically migrated
2. No action is required on your part
3. The migration preserves all game data and settings
4. If you see any "Unknown emulator profile type" errors, they should be resolved after restart

## Getting Help

If you need assistance:

1. Check the README.md file for common issues and solutions
2. Join the Playnite Discord and ask in #extension-support
3. Open an issue on the GitHub repository
4. Include relevant details from your Playnite logs

## Contributing

Want to help improve EmuLibrary PC Manager?

1. Check the MAINTENANCE.md file for development information
2. Fork the repository on GitHub
3. Make your changes and create a pull request
4. Follow the coding style and documentation practices in ARCHITECTURE.md