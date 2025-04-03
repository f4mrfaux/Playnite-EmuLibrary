# Common Workflows for EmuLibrary PC Manager

Quick reference guide for common tasks when using EmuLibrary PC Manager.

## Initial Setup

### Adding a New Source Library

```
Settings → EmuLibrary PC Manager → Add New Mapping
↓
Configure mapping (Source/Destination paths)
↓
Save and restart Playnite
↓
Games appear in library
```

### Setting Up Archive Support

```
Download 7z.exe and UnRAR.exe
↓
Place in EmuLibrary/Tools folder
(F12 → Open application directory → Extensions → EmuLibrary → Tools)
↓
Restart Playnite
```

## Day-to-Day Operations

### Installing a Game

```
Select uninstalled game in library
↓
Click "Play"
↓
Confirm installation
↓
Wait for automatic installation
↓
Game is ready to play
```

### Setting Custom Executable

```
Right-click game → EmuLibrary → Select Custom Executable
↓
Browse to correct .exe file
↓
Selection is remembered for future launches
```

### Uninstalling a Game

```
Right-click game → Uninstall
↓
Confirm uninstallation
↓
Game remains in library for future reinstallation
```

## Managing Multiple Sources

### Creating Platform-Specific Libraries

```
Add separate mappings for each game collection:
- PC Games: Source=/Games/PC, Platform=PC
- GOG Games: Source=/Games/GOG, Platform=GOG
- Indie Games: Source=/Games/Indie, Platform=Indie
```

### Disabling a Source Temporarily

```
Settings → EmuLibrary PC Manager
↓
Uncheck "Enabled" for specific mapping
↓
Save and restart Playnite
```

### Updating a Source Path

```
Settings → EmuLibrary PC Manager
↓
Edit Source Path for specific mapping
↓
Save and restart Playnite
```

## Organization Tips

### Folder Structure for Best Results

```
/GameCollection/
  ├── Game1/
  │   └── setup_game1.exe
  ├── Game2/
  │   ├── game2.part1.rar
  │   ├── game2.part2.rar
  │   └── game2.part3.rar
  └── Game3/
      └── game3.iso
```

### Fixing Wrong Game Names

```
Right-click game → Edit
↓
Update game title and metadata
↓
Save changes
```

## Troubleshooting

### When Games Don't Appear

```
Verify Source Path is accessible
↓
Check mapping is enabled
↓
Restart Playnite
↓
Check log files (F12 → Open application directory → Extensions → EmuLibrary)
```

### When Installation Fails

```
Check source files accessibility
↓
Verify disk space at destination
↓
Ensure 7z.exe/UnRAR.exe available (for archives)
↓
Check logs for specific errors
```

### For v0.8.3 Migration Issues

```
Update to v0.8.3+
↓
Restart Playnite (automatic migration occurs)
↓
Update ROM Type in mappings from "GogInstaller" to "PcInstaller"
↓
Save and restart Playnite
```