# Migration Guide - v0.8.3 Update

## Key Changes in v0.8.3

1. **Unified Installer System**
   - GOG and PC installers now use the same backend
   - All installer types handled by "PcInstaller" ROM type
   - GOG installers automatically detected by filename patterns

2. **UI Improvements**
   - Better text contrast for all UI elements
   - Improved readability in both light and dark themes
   - Consistent styling throughout settings panels

3. **Bug Fixes**
   - Fixed "Unknown emulator profile type" error
   - Better handling of nested archives
   - Improved installation parameter detection

## Automatic Migration Process

Your games will migrate automatically with no data loss:

1. Install v0.8.3 and restart Playnite
2. The extension will:
   - Find existing GOG installer games
   - Convert them to use the unified system
   - Preserve all game data and settings
   - Update your library seamlessly

**No manual action is required** - but we recommend updating your settings (see below).

## Recommended Settings Update

For cleaner configuration:

1. Open Settings → EmuLibrary PC Manager
2. For mappings using "GogInstaller" ROM type:
   - Change ROM Type to "PcInstaller"
   - Leave all other settings unchanged
3. Save and restart Playnite

## Verification Checklist

- [ ] All GOG games still appear in library
- [ ] Installed GOG games launch correctly
- [ ] New GOG games install properly
- [ ] UI text is readable in all panels

## Troubleshooting Common Issues

| Problem | Solution |
|---------|----------|
| **Missing games** | • Restart Playnite<br>• Library → Scan all library data |
| **Profile type error** | • Fixed in v0.8.3<br>• If persists, recreate the mapping |
| **Launch issues** | • Right-click → EmuLibrary → Select Custom Executable |
| **UI problems** | • Close/reopen settings<br>• Try resetting UI cache (F12 → Debugging options) |

## Earlier Version Migrations

Coming from v0.7.x or earlier?

1. **Backup your settings** (screenshot/notes)
2. **Clean install recommended**
3. **Reconfigure** mappings from backup

## Getting Help

Having migration issues?

1. Check logs: F12 → Open application directory → Extensions → EmuLibrary
2. Report on GitHub with version details and logs
3. Join [Playnite Discord](https://playnite.link/) (#extension-support) for help