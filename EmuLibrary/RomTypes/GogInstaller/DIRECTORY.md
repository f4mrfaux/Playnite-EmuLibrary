# GogInstaller Directory

This directory contains specialized implementations for handling GOG game installers, which have specific formats and behaviors different from generic PC installers.

## Key Files

| File | Description |
|------|-------------|
| `GogInstallerGameInfo.cs` | Stores metadata specific to GOG games, extending the base `ELGameInfo` class. |
| `GogInstallerScanner.cs` | Specialized scanner for detecting GOG installers based on their unique patterns and file properties. |
| `GogInstallerInstallController.cs` | Handles the installation of GOG games, with specific silent parameters and executable detection. |
| `GogInstallerUninstallController.cs` | Handles the uninstallation of GOG games, using either the GOG-specific uninstaller or manual deletion. |

## GOG-Specific Features

The GogInstaller implementation includes specialized features for GOG games:

1. **Detection**: Recognizes GOG-specific installer formats and naming patterns
2. **Silent Installation**: Uses parameters optimized for GOG installers
3. **Galaxy Integration**: Special handling for GOG Galaxy-compatible games
4. **Executable Detection**: Specialized patterns for finding GOG game executables

## Relation to PcInstaller

The GogInstaller implements similar functionality to the PcInstaller but with GOG-specific optimizations. Both use the same base architecture of:

- A GameInfo class for metadata
- A Scanner for detection
- Install and Uninstall controllers for management

This allows for code reuse while providing specialized behavior for GOG games.