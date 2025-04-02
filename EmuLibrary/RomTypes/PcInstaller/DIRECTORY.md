# PcInstaller Directory

This directory contains the implementation for handling PC game installers, a key feature of this plugin that allows managing PC game installers stored on network shares or local folders.

## Key Files

| File | Description |
|------|-------------|
| `PcInstallerGameInfo.cs` | Stores metadata about PC game installers including source path and executable path. |
| `PcInstallerScanner.cs` | Scans directories for PC game installers, detects various installer types, and creates game metadata. |
| `PcInstallerInstallController.cs` | Handles the installation process from network to local machine, including archive extraction and installer execution. |
| `PcInstallerUninstallController.cs` | Handles the uninstallation process, either using the game's uninstaller or through manual directory deletion. |

## Functionality Overview

The PcInstaller system:

1. **Detects** PC game installers through file extensions, naming patterns, and file properties
2. **Handles** various installer formats (.exe, .msi) and archives (.iso, .rar)
3. **Extracts** archives when needed using specialized handlers
4. **Installs** games with appropriate silent parameters based on installer type
5. **Detects** the game executable after installation
6. **Updates** Playnite with the installation information

## Handlers Subdirectory

The `Handlers/` subdirectory contains specialized classes for handling different archive formats:

| File | Description |
|------|-------------|
| `IArchiveHandler.cs` | Interface for archive handlers that defines common capabilities. |
| `ArchiveHandlerFactory.cs` | Factory class for creating appropriate archive handlers based on file type. |
| `IsoHandler.cs` | Specialized handler for ISO files, using 7z.exe for extraction. |
| `MultiRarHandler.cs` | Specialized handler for RAR archives (including multi-part RARs), using UnRAR.exe. |

## Adding New Archive Handlers

To add support for a new archive format:

1. Create a new class implementing the `IArchiveHandler` interface
2. Implement all required methods for your archive type
3. Register your handler in the `ArchiveHandlerFactory` constructor

This modular design allows for easy extension of archive format support.