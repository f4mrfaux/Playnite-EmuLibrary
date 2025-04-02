# FileCopier Directory

This directory contains implementations for copying files with different methods, particularly optimized for transferring large files from network locations to local storage.

## Key Files

| File | Description |
|------|-------------|
| `IFileCopier.cs` | Interface defining the API for all file copier implementations. |
| `BaseFileCopier.cs` | Abstract base class implementing common functionality for file copiers. |
| `SimpleFileCopier.cs` | Basic implementation that uses standard .NET file operations. |
| `WindowsFileCopier.cs` | Windows-specific implementation that shows the Windows copy dialog for visual feedback. |

## IFileCopier Interface

The `IFileCopier` interface defines:

| Method | Description |
|--------|-------------|
| `CopyFile(string source, string destination, bool overwrite)` | Copies a single file from source to destination. |
| `CopyFiles(IEnumerable<CopyOperation> operations)` | Copies multiple files in a batch. |
| `CancelCopy()` | Cancels an ongoing copy operation. |

Additionally, it defines events for progress reporting and completion.

## Implementation Details

### BaseFileCopier

The `BaseFileCopier` implements common functionality:
- Event triggering
- Error handling
- Default implementations of some methods

### SimpleFileCopier

The `SimpleFileCopier` provides:
- Basic file copy operations using File.Copy
- Progress reporting using file size checks
- Minimal overhead for simple operations

### WindowsFileCopier

The `WindowsFileCopier` provides:
- Native Windows copy dialog
- Shell-based file operations
- User-familiar interface with progress bars and cancel buttons
- Special handling for UAC and permissions

## Usage

The appropriate FileCopier is selected based on:
- User preferences in Settings
- Current Playnite mode (desktop vs. fullscreen)
- Platform availability

This system allows for flexible and user-friendly file copying during game installation.