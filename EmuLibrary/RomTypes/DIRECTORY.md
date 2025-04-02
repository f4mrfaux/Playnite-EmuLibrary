# RomTypes Directory

This directory contains implementations of different ROM/installer types supported by the plugin. Each ROM type has its own set of classes for handling specific types of game files.

## Key Files

| File | Description |
|------|-------------|
| `BaseInstallController.cs` | Base class for all install controllers providing common functionality. |
| `BaseUninstallController.cs` | Base class for all uninstall controllers providing common functionality. |
| `ELGameInfo.cs` | Base class for all game info classes, defining common properties and behaviors. |
| `ELGameInfoBaseExtensions.cs` | Extension methods for the `ELGameInfo` class. |
| `RomType.cs` | Enum defining the available ROM types (SingleFile, MultiFile, Yuzu, GogInstaller, PcInstaller). |
| `RomTypeInfoAttribute.cs` | Attribute for associating metadata with ROM types. |
| `RomTypeScanner.cs` | Base class for ROM type scanners that search for games. |

## Subdirectories

Each ROM type has its own directory with specialized implementations:

| Directory | Description |
|-----------|-------------|
| `SingleFile/` | Implementation for handling single ROM files (traditional use case). |
| `MultiFile/` | Implementation for handling ROMs that consist of multiple files. |
| `Yuzu/` | Specialized implementation for handling Yuzu emulator files. |
| `GogInstaller/` | Specialized implementation for GOG game installers. |
| `PcInstaller/` | Implementation for generic PC game installers, including advanced archive handling. |

## Adding New ROM Types

To add a new ROM type:

1. Create a new subdirectory for your ROM type
2. Implement the following classes:
   - `[YourType]GameInfo.cs`: Extends `ELGameInfo` to store metadata specific to your type
   - `[YourType]Scanner.cs`: Extends `RomTypeScanner` to detect games of your type
   - `[YourType]InstallController.cs`: Extends `BaseInstallController` to handle installation
   - `[YourType]UninstallController.cs`: Extends `BaseUninstallController` to handle uninstallation
3. Add your type to the `RomType` enum in `RomType.cs` with a `RomTypeInfoAttribute`
4. Make sure the attribute references your GameInfo and Scanner types

This architecture allows for easy extension of the plugin to support new types of game files.