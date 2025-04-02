# EmuLibrary Directory

This is the main project directory containing the source code for the EmuLibrary plugin.

## Key Files

| File | Description |
|------|-------------|
| `EmuLibrary.cs` | Core plugin class that implements `LibraryPlugin` and `IEmuLibrary`. This is the entry point for the plugin. |
| `EmuLibraryClient.cs` | Client-side implementation of the plugin. |
| `IEmuLibrary.cs` | Interface defining core plugin functionality. |
| `EmuLibrary.csproj` | Project file containing references and build configurations. |
| `extension.yaml` | Plugin metadata and description shown in Playnite. |
| `icon.png` | Plugin icon displayed in Playnite's extension list. |

## Subdirectories

| Directory | Description |
|-----------|-------------|
| `PlayniteCommon/` | Common utility classes used across the plugin, sourced from Playnite. |
| `Properties/` | Assembly information and properties for the plugin. |
| `RomTypes/` | Contains implementations of different ROM/installer types (e.g., SingleFile, MultiFile, GogInstaller, PcInstaller). |
| `Settings/` | Settings-related classes for configuring the plugin. |
| `Util/` | Utility classes used across the plugin. |
| `Tools/` | Directory where external tools like 7z.exe and UnRAR.exe should be placed. |

## Architecture Overview

EmuLibrary is structured around the concept of "ROM types", which are different ways of handling game files. The plugin supports:

1. **SingleFile**: Simple ROM files (traditional use case)
2. **MultiFile**: ROMs that consist of multiple files
3. **Yuzu**: Special handler for Yuzu emulator files
4. **GogInstaller**: Specialized handler for GOG installers
5. **PcInstaller**: Generic PC game installer handler

Each ROM type implements:
- A `GameInfo` class to store metadata
- A `Scanner` to detect games
- `InstallController` to handle installation
- `UninstallController` to handle uninstallation

The main `EmuLibrary` class integrates these components with Playnite.