# Developer's Guide to EmuLibrary

This guide provides a quick overview of the project structure and key concepts for developers who want to understand, modify, or extend the EmuLibrary plugin.

## Project Architecture

EmuLibrary is designed around the concept of ROM types, which are different ways of handling game files:

1. **Core Components**:
   - `EmuLibrary.cs`: Plugin entry point
   - `IEmuLibrary.cs`: Core interface
   - `Settings/`: Configuration system

2. **ROM Type System**:
   - Each type (SingleFile, MultiFile, GogInstaller, PcInstaller, etc.) follows a similar pattern
   - Each implements:
     - `XXXGameInfo.cs`: Metadata storage
     - `XXXScanner.cs`: File detection
     - `XXXInstallController.cs`: Installation process
     - `XXXUninstallController.cs`: Uninstallation process

3. **Utility Systems**:
   - `Util/FileCopier/`: File operations
   - `PlayniteCommon/`: Shared utilities

## Key Workflows

### Game Detection Workflow

1. User adds a mapping in settings
2. `EmuLibrary.GetGames()` is called by Playnite during library refresh
3. `RomTypeScanner.GetGames()` is called for each ROM type
4. Scanner detects valid files and creates `GameMetadata` objects
5. Playnite adds the games to the library

### Installation Workflow

1. User clicks "Play" on an uninstalled game
2. `EmuLibrary.GetInstallActions()` returns the appropriate controller
3. `XXXInstallController.Install()` is called
4. Files are copied or installers are executed
5. Game status is updated in Playnite

### Uninstallation Workflow

1. User right-clicks and selects "Uninstall"
2. `EmuLibrary.GetUninstallActions()` returns the appropriate controller
3. `XXXUninstallController.Uninstall()` is called
4. Files are removed or uninstallers are executed
5. Game status is updated in Playnite

## Adding a New ROM Type

1. Create a new directory under `RomTypes/`
2. Create your implementation classes:
   - `XXXGameInfo.cs`: Extending `ELGameInfo`
   - `XXXScanner.cs`: Extending `RomTypeScanner`
   - `XXXInstallController.cs`: Extending `BaseInstallController`
   - `XXXUninstallController.cs`: Extending `BaseUninstallController`
3. Add your type to the `RomType` enum in `RomType.cs`
4. Add a `RomTypeInfoAttribute` with your GameInfo and Scanner types

## Testing

When testing the plugin:

1. Build the project
2. Copy the output DLL and dependencies to:
   - `%AppData%\Playnite\Extensions\EmuLibrary\` (for installed Playnite)
   - Or the appropriate extensions directory if running Playnite portable
3. Use DEBUG compilation for detailed logging
4. Check Playnite logs for errors (`F12` → `Open application directory` → check log files)

## Building on Linux

If you're developing on a Linux machine, you can still build and syntax-check the project using Mono:

1. Install the required packages:
   ```bash
   # For Arch Linux
   sudo pacman -S mono mono-msbuild mono-addins nuget
   
   # For Ubuntu/Debian
   sudo apt-get install mono-complete nuget msbuild
   ```

2. Use the provided build scripts:
   - `./build.sh` - Full build using Mono's MSBuild
   - `./check-syntax.sh` - Fast syntax validation of C# files without full build

3. For VS Code integration, open the provided workspace file:
   ```bash
   code EmuLibrary.code-workspace
   ```
   
   Install the C# extension to get intellisense and syntax highlighting.

Note: The Windows-specific post-build steps will be skipped when building on Linux.

## Building on Linux

If you're developing on a Linux machine, you can still build and syntax-check the project using Mono:

1. Install the required packages:
   ```bash
   # For Arch Linux
   sudo pacman -S mono mono-msbuild mono-addins nuget
   
   # For Ubuntu/Debian
   sudo apt-get install mono-complete nuget msbuild
   ```

2. Use the provided build scripts:
   - `./build.sh` - Full build using Mono's MSBuild
   - `./check-syntax.sh` - Fast syntax validation of C# files without full build

3. For VS Code integration, open the provided workspace file:
   ```bash
   code EmuLibrary.code-workspace
   ```
   
   Install the C# extension to get intellisense and syntax highlighting.

Note: The Windows-specific post-build steps will be skipped when building on Linux.

## Directory Documentation

Each directory contains a `DIRECTORY.md` file with specific information about the files and their purposes. Refer to these files for detailed documentation about each component.