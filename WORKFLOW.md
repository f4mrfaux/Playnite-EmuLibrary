# User Workflows for EmuLibrary PC Manager

This guide outlines common workflows for using the EmuLibrary PC Manager extension for Playnite.

## First-Time Setup

1. **Install the Extension**
   - Open Playnite
   - Go to Extensions → Browse
   - Search for "EmuLibrary PC Manager"
   - Click Install
   - Restart Playnite when prompted

2. **Configure Your First Source**
   - Go to Settings → EmuLibrary PC Manager
   - You'll see a helpful guide panel on the left
   - Click "Add New Mapping" on the right
   - Complete the following fields:
     - Emulator: Select any (e.g., "Windows")
     - Profile: Select any (e.g., "Default")
     - Platform: Select "PC" (or create it)
     - ROM Type: Select "PcInstaller" (handles both regular and GOG installers)
     - Source Path: Your network or local folder with game installers
     - Destination Path: Where you want games installed
     - Enabled: Check this box
   - Click Save and restart Playnite

3. **Install External Tools** (for archive support)
   - If you need ISO or RAR support:
     - Download 7z.exe and UnRAR.exe from their official websites
     - Create a folder called "Tools" in the EmuLibrary extension directory
     - Copy the executables into this folder
     - Restart Playnite

## Day-to-Day Usage

### Finding and Installing Games

1. **Browse Your Library**
   - Open Playnite
   - Games from your configured sources will appear in your library
   - Uninstalled games will show a grayed-out "Install" status

2. **Install a Game**
   - Click on a game in your library
   - Click the "Play" button
   - Playnite will ask if you want to install the game
   - Click "Yes" to begin installation
   - The plugin will:
     - Copy the installer from your network share
     - Extract archives if needed (RAR, ISO)
     - Run the installer with the appropriate silent parameters
     - Detect the main game executable
     - Update Playnite with the installation information

3. **Play the Game**
   - Once installation is complete, click "Play" again
   - The game will launch directly

### Managing Your Games

1. **Select a Custom Executable**
   - If the plugin doesn't find the correct executable:
     - Right-click the game in your library
     - Select EmuLibrary → Select Custom Executable
     - Browse to the correct .exe file
     - Click Open
     - This selection will be remembered for future launches

2. **Uninstall a Game**
   - Right-click a game in your library
   - Select Uninstall
   - The plugin will properly uninstall the game
   - The game will remain in your library for future installation

3. **View Game Details**
   - Click a game in your library
   - The Details panel will show information about the game
   - For installed games, you can see the installation path
   - For uninstalled games, you can see the source location

## Managing Multiple Sources

1. **Add Additional Sources**
   - Go to Settings → EmuLibrary PC Manager
   - Add new mappings for each source folder
   - You can configure different destination paths for each
   - Use different platforms to organize games (e.g., "PC", "GOG Games")

2. **Disable a Source Temporarily**
   - Go to Settings → EmuLibrary PC Manager
   - Uncheck the "Enabled" box for any mapping
   - Click Save and restart Playnite
   - Games from this source will no longer appear in your library

3. **Update Source Paths**
   - If your network share location changes:
     - Go to Settings → EmuLibrary PC Manager
     - Update the Source Path for the affected mapping
     - Click Save and restart Playnite
     - The plugin will use the new location

## Troubleshooting

1. **Check the Logs**
   - If you encounter issues:
     - Press F12 in Playnite
     - Click "Open application directory"
     - Check the log files in the "Extensions" subfolder

2. **Common Fixes**
   - For installation issues:
     - Ensure your network share is accessible
     - Check if you have enough disk space
     - Make sure external tools (7z.exe, UnRAR.exe) are available
   - For game detection issues:
     - Organize games in properly named folders
     - Ensure your game files match supported formats

## Tips and Tricks

1. **Organize Your Installers**
   - Create a folder for each game
   - Use descriptive folder names (the plugin uses these for metadata)
   - Keep related files together (installers, patches, DLCs)

2. **Optimize Network Performance**
   - Use wired connections when possible
   - Consider a local cache for frequently accessed games
   - Schedule scanning during low-network-usage periods

3. **Customize the Experience**
   - Use Playnite's filtering to organize your game collection
   - Create custom filters for installed vs. uninstalled games
   - Use the Details panel to see specific installation information

4. **Keep Your Extension Updated**
   - Check for updates regularly in Playnite's Extensions menu
   - Read the CHANGELOG.md file for new features and improvements
   - Report any issues on GitHub to help improve the extension