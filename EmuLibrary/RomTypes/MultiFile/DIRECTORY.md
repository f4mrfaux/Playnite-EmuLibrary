# MultiFile Directory

This directory contains implementations for handling multi-file ROMs, which are games that consist of multiple files that must be kept together (e.g., CD-based games with multiple tracks).

## Key Files

| File | Description |
|------|-------------|
| `MultiFileGameInfo.cs` | Stores metadata about multi-file ROMs, including the primary file and associated files. |
| `MultiFileGameInfoExtensions.cs` | Extension methods for the MultiFileGameInfo class. |
| `MultiFileScanner.cs` | Scans directories for multi-file ROMs based on patterns and relationships. |
| `MultiFileInstallController.cs` | Handles the installation process for multi-file ROMs, copying all related files. |
| `MultiFileUninstallController.cs` | Handles the uninstallation process for multi-file ROMs, removing all related files. |

## Functionality Overview

The MultiFile implementation:

1. **Detects** games that consist of multiple related files
2. **Groups** files that belong to the same game
3. **Copies** all related files during installation
4. **Records** the primary file for launching the emulator
5. **Removes** all files during uninstallation

This implementation is important for handling games like multi-disc titles, CD-based games with multiple tracks, or games with separate data files that must be kept together.