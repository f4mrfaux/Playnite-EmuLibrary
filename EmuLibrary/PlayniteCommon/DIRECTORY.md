# PlayniteCommon Directory

This directory contains common utility classes sourced from or inspired by Playnite's codebase, providing functionality that is useful across the plugin.

## Key Files

| File | Description |
|------|-------------|
| `CloneObject.cs` | Utilities for creating deep copies of objects, used for settings editing. |
| `SafeFileEnumerator.cs` | Robust file enumeration that handles network paths and permissions issues gracefully. |
| `StringExtensions.cs` | String manipulation utilities, particularly for file path handling. |
| `LICENSE.md` | License information for code sourced from Playnite. |

## Functionality Overview

### CloneObject

The `CloneObject` class provides:
- Methods for deep copying objects using serialization
- Supports complex object graphs
- Used primarily for settings editing (to create an editable copy)

### SafeFileEnumerator

The `SafeFileEnumerator` implements:
- IEnumerable for file system entries
- Robust error handling for network shares
- Recovery from transient failures
- Better performance on slow network connections

### StringExtensions

The `StringExtensions` class provides:
- Methods for normalizing game names
- Utilities for path manipulation
- Safe string operations for UI display

## Usage Notes

These utilities are used throughout the plugin to ensure:
- Reliable file operations, especially over networks
- Consistent string handling
- Safe object manipulation

These are particularly important for the plugin's core functionality of scanning network shares and handling file operations.