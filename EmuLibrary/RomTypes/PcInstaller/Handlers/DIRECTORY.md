# Handlers Directory

This directory contains specialized handlers for different archive formats used by the PcInstaller system. These handlers are responsible for extracting and managing archives containing game installers.

## Key Files

| File | Description |
|------|-------------|
| `IArchiveHandler.cs` | Interface defining the API for all archive handlers, including methods for checking file compatibility, listing contents, and extracting archives. |
| `ArchiveHandlerFactory.cs` | Factory class that creates and manages the appropriate handlers for different file types. |
| `IsoHandler.cs` | Implementation of `IArchiveHandler` for ISO files, using 7z.exe for extraction. |
| `MultiRarHandler.cs` | Implementation of `IArchiveHandler` for RAR archives (including multi-part RARs), using UnRAR.exe. |

## IArchiveHandler Interface

The `IArchiveHandler` interface defines the following key methods:

| Method | Description |
|--------|-------------|
| `CanHandle(string filePath)` | Returns true if the handler can process the specified file. |
| `ListContents(string archivePath)` | Returns a list of files contained in the archive. |
| `ExtractAsync(string archivePath, string destinationPath, CancellationToken cancellationToken)` | Extracts the entire archive to the specified destination. |
| `ExtractFileAsync(string archivePath, string fileToExtract, string destinationPath, CancellationToken cancellationToken)` | Extracts a specific file from the archive. |
| `GetArchiveDisplayName(string archivePath)` | Returns a user-friendly name for the archive. |
| `GetExpectedInstallSize(string archivePath)` | Estimates the size of the extracted content for space requirements. |

## External Tool Dependencies

The archive handlers depend on external tools that must be available in the Tools directory:

- **7z.exe**: Used by `IsoHandler` to extract ISO files
- **UnRAR.exe**: Used by `MultiRarHandler` to extract RAR archives

These handlers implement robust fallback mechanisms to locate these tools in various directories.

## Extending Archive Support

To add support for a new archive format:

1. Create a new class implementing the `IArchiveHandler` interface
2. Implement all required methods
3. Register your handler in the `ArchiveHandlerFactory` constructor
4. Ensure any required external tools are available and properly located by your handler

This modular architecture allows for easy extension of archive format support.