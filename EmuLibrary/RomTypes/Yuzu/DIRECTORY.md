# Yuzu Directory

This directory contains specialized implementations for handling Nintendo Switch games for the Yuzu emulator, which has unique requirements and structures.

## Key Files

| File | Description |
|------|-------------|
| `Yuzu.cs` | Helper class with Yuzu-specific constants and utility functions. |
| `YuzuGameInfo.cs` | Stores metadata specific to Yuzu games, including title ID and update information. |
| `YuzuGameInfoExtensions.cs` | Extension methods for the YuzuGameInfo class. |
| `YuzuScanner.cs` | Specialized scanner for detecting Yuzu-compatible games with proper metadata. |
| `YuzuInstallController.cs` | Handles the installation of Yuzu games with proper directory structure. |
| `YuzuUninstallController.cs` | Handles the uninstallation of Yuzu games and cleanup. |
| `YuzuLegacySettings.cs` | Support for legacy settings migration. |
| `SourceDirCache.cs` | Cache system for optimizing scans of Yuzu game directories. |

## Yuzu-Specific Features

The Yuzu implementation includes specialized features:

1. **Title ID Detection**: Extracts Nintendo Switch title IDs for proper emulator configuration
2. **Update/DLC Handling**: Special handling for game updates and DLC content
3. **Directory Structure**: Maintains the correct directory structure required by Yuzu
4. **Caching**: Optimized scanning with caching for better performance with large libraries

This implementation demonstrates how the plugin architecture can be extended to support specialized emulator requirements beyond simple file copying.