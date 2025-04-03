# Maintenance Guide for EmuLibrary PC Manager

This document provides guidelines for maintaining and extending this Playnite extension.

## Development Setup

1. **Prerequisites**:
   - Visual Studio 2019 or newer (or Visual Studio Code with C# extension)
   - .NET Framework 4.6.2 SDK
   - PowerShell or Bash for running build scripts

2. **Building**:
   - Windows: `dotnet build -c Release` or `.\build-clean.ps1`
   - Linux/Mac: `./build.sh` or `./build-clean.sh`

3. **Testing Syntax** (Linux only):
   - `./check-syntax.sh` - Performs a fast C# syntax validation without full build

## Architecture Overview

The extension is designed around the concept of ROM types:

1. **Core Components**:
   - `EmuLibrary.cs` - Plugin entry point and main class
   - `Settings/` - Configuration system

2. **ROM Type System**:
   - Each ROM type supports different installation methods
   - Key types include PcInstaller (also handles GOG installers)
   - Each type implements a GameInfo, Scanner, InstallController, and UninstallController

3. **PcInstaller Handlers**:
   - Support for different archive and installer types
   - IsoHandler - Handles ISO files containing game installers
   - MultiRarHandler - Handles multi-part RAR archives that may contain games or ISOs

## Common Maintenance Tasks

### Adding Support for a New Archive/Installer Type

1. Create a new handler class that implements `IArchiveHandler`
2. Add detection logic to `PcInstallerScanner.cs`
3. Register the handler in `ArchiveHandlerFactory.cs`

### Updating UI Elements

1. The main UI is in `Settings/SettingsView.xaml`
2. Ensure all text elements have proper contrast (use Foreground="#FF333333")
3. Test with both light and dark Playnite themes

### Debugging Tips

1. Use `Logger.Info()`, `Logger.Warn()`, and `Logger.Error()` for logging
2. Check logs in Playnite by pressing F12 → "Open application directory" → check log files
3. Common errors include missing permissions and invalid file paths

## Release Process

1. Update version number in `extension.yaml`
2. Update `CHANGELOG.md` with new changes
3. Build using `build-clean.ps1` or `build-clean.sh`
4. Test the .pext file in a clean Playnite installation
5. Create a release tag in Git

## Dependencies

- External tools (7z.exe, UnRAR.exe) are required for archive support
- These should be placed in the Tools directory by users
- LibHac.dll is required for Yuzu ROM scanning