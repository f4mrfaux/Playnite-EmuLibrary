# Migration Guide - EmuLibrary PC Manager

This document explains how to migrate from previous versions of EmuLibrary PC Manager to version 0.8.3 and newer.

## Migrating from v0.8.2 to v0.8.3

### Changes in v0.8.3

Version 0.8.3 includes the following key changes:

1. **Consolidated Installer Types**: 
   - GOG installer functionality is now integrated into PC installer
   - Use "PcInstaller" ROM type for all installers, including GOG games

2. **UI Improvements**:
   - Better text contrast in settings panels
   - Improved readability in both light and dark themes
   - Consistent styling across all components

3. **Bug Fixes**:
   - Fixed "Unknown emulator profile type" error
   - Improved GOG installer detection
   - Better handling of nested archives

### Automatic Migration

The extension will **automatically migrate** your existing games when you update:

1. Install version 0.8.3 or newer
2. Restart Playnite
3. The extension will:
   - Detect any existing GOG installer games
   - Convert them to use the new unified PC installer system
   - Preserve all game data and custom settings
   - Update your library accordingly

**No manual action is required** for this migration.

### Updating Your Mappings

While your existing mappings will continue to work, we recommend updating them for clarity:

1. Go to Settings → EmuLibrary PC Manager
2. For any mapping using "GogInstaller" ROM type:
   - Change the ROM Type to "PcInstaller"
   - All other settings can remain the same
3. Click Save and restart Playnite

### Verifying Migration Success

To verify that migration was successful:

1. Check that all your GOG games still appear in your library
2. Confirm that installed GOG games still launch correctly
3. Try installing a previously uninstalled GOG game to ensure it works

## Migrating from Earlier Versions

### From v0.7.x to v0.8.x

If you're migrating from a much earlier version (0.7.x or earlier):

1. **Backup your settings first**:
   - Go to Settings → EmuLibrary PC Manager
   - Take screenshots or notes of your current configuration
   
2. **Clean installation recommended**:
   - Uninstall the old version completely
   - Restart Playnite
   - Install the latest version
   - Reconfigure your mappings based on your backup
   
3. **Additional steps**:
   - You may need to re-scan your sources
   - Custom executable selections might need to be redefined
   - Check if external tools (7z.exe, UnRAR.exe) need updating

## Known Migration Issues

### Common Issues and Solutions

| Issue | Solution |
|-------|----------|
| **Games missing after migration** | - Restart Playnite<br>- Go to Library → Scan all library data<br>- Check if the source path is still accessible |
| **"Unknown emulator profile type" error** | - This should be fixed by v0.8.3<br>- If it persists, try removing and recreating the mapping |
| **Games show as installed but won't launch** | - Right-click → EmuLibrary → Select Custom Executable<br>- Browse to the correct executable |
| **Settings UI appears empty or corrupted** | - Close and reopen the settings panel<br>- If issues persist, try resetting Playnite UI cache |

## Get Help with Migration

If you encounter any issues during migration:

1. Check the Playnite logs for error messages:
   - Press F12
   - Click "Open application directory"
   - Check the log files

2. Report issues on GitHub with:
   - Your previous version number
   - Steps to reproduce the issue
   - Relevant log entries
   - Screenshots if applicable

3. Join the Playnite Discord for real-time assistance in the #extension-support channel