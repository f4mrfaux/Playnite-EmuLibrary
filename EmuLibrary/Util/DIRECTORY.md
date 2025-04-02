# Util Directory

This directory contains utility classes that provide common functionality used throughout the plugin.

## Key Files

| File | Description |
|------|-------------|
| `FileNameUtils.cs` | Utilities for handling file names, including sanitization and metadata extraction. |

## FileCopier Subdirectory

The `FileCopier/` subdirectory contains implementations for copying files with different methods:

| File | Description |
|------|-------------|
| `IFileCopier.cs` | Interface defining the API for all file copier implementations. |
| `BaseFileCopier.cs` | Abstract base class implementing common functionality for file copiers. |
| `SimpleFileCopier.cs` | Basic implementation that uses standard .NET file operations. |
| `WindowsFileCopier.cs` | Windows-specific implementation that shows the Windows copy dialog for visual feedback. |

## FileCopier Architecture

The FileCopier system is designed with the following architecture:

- `IFileCopier` defines the interface for all copiers
- `BaseFileCopier` provides common functionality
- Concrete implementations handle different copying methods
- The appropriate copier is selected based on user settings and environment

This allows for flexibility in how files are copied, enabling features like progress reporting, cancellation support, and platform-specific optimizations.