# SingleFile Directory

This directory contains implementations for handling single-file ROMs, which is the simplest and most traditional use case for emulator libraries.

## Key Files

| File | Description |
|------|-------------|
| `SingleFileGameInfo.cs` | Stores metadata about single-file ROMs, including the file path. |
| `SingleFileScanner.cs` | Scans directories for individual ROM files based on file extensions. |
| `SingleFileInstallController.cs` | Handles the installation process for single-file ROMs, which is typically a simple file copy. |
| `SingleFileUninstallController.cs` | Handles the uninstallation process for single-file ROMs, typically just deleting the file. |

## Functionality Overview

The SingleFile implementation:

1. **Scans** directories for individual ROM files based on extensions
2. **Copies** ROM files from the source (e.g., network share) to the destination
3. **Records** the file location for launching the emulator
4. **Removes** the file during uninstallation if requested

This is the original and simplest ROM type implementation in the plugin, handling the traditional emulator use case of individual ROM files that are loaded directly by an emulator.