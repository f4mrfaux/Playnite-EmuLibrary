# Release Notes - EmuLibrary PC Manager

## Version 0.8.3

### UI Improvements
- Fixed text contrast issues in the Settings panel
- Improved readability of text in info boxes with colored backgrounds
- Ensured consistent text color in all UI components
- Better visibility for checkbox labels and DataGrid content

### Architectural Improvements
- Consolidated GOG and PC installer functionality
- Improved detection of GOG installers within the PC installer framework
- Added automated migration for existing GOG game entries
- Simplified codebase maintenance by removing duplicate code

### Bug Fixes
- Fixed "Unknown emulator profile type" error
- Resolved issues with GOG installer scanner initialization
- Improved handling of nested archives (ISO inside RAR)
- More robust executable detection after installation

## Earlier Versions

### Version 0.8.2
- Fixed protobuf-net dependency issue
- Simplified LibHac dependency handling for automatic inclusion
- Added LibHac download support and improved build scripts

### Version 0.8.1
- Added detailed installation troubleshooting
- Improved error handling during installation
- Better detection of installed games
- Fixed issues with executable path detection