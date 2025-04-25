# EmuLibrary Release Notes v1.6.0

## ISO Scanner and Installer Fixes

This release focuses on fixing issues with ISO path handling during scanning and installation. These improvements ensure that ISO-based games are properly detected, displayed in the library, and can be successfully installed.

### Key Improvements

#### ISO Installation

- **Fixed ISO Path Resolution**: Resolved issues where the ISO files couldn't be found during installation
- **Enhanced Path Search**: Improved searching for ISO files by game name when paths are incomplete
- **Path Recovery System**: Added ability to recover and repair ISO paths during application startup
- **Manual File Selection**: Added "Find Missing ISO..." context menu option to manually select ISO files when automatic resolution fails

#### Game Detection

- **Better Scanning**: Improved detection of ISO files with various naming patterns
- **Case Sensitivity**: Now handles case variations in file extensions (.ISO vs .iso)
- **Game Name Variations**: Searches for files using multiple variants of game names (with spaces, underscores, hyphens, etc.)
- **Fuzzy Matching**: Added score-based matching to find the most likely ISO file for a game

#### User Interface

- **Path Diagnostics**: Better error messages about ISO path issues
- **Missing ISO Tagging**: Games with missing ISO files are tagged for easier identification
- **Context Menu Integration**: Added context menu option for manually selecting ISO files
- **Notification Improvements**: Added notifications to verify that games are being imported properly

### Technical Improvements

- **Path Information Preservation**: Better preservation of path information during library updates
- **Error Handling**: More robust error handling in all path-related operations
- **Diagnostic Logging**: Enhanced logging for ISO path resolution process
- **PathIds Compatibility**: Fixed issues with TagIds and other read-only collections in Playnite API

### Installation Instructions

1. Close Playnite
2. Install the extension as a .pext file (drag and drop into Playnite)
3. Restart Playnite
4. If you had ISO games that weren't appearing or couldn't be installed, they should now work correctly
5. For any games still having ISO path issues, right-click and use "Find Missing ISO..." to manually select the ISO file

### Known Issues

- In rare cases, you might need to restart Playnite to see the new context menu options
- Windows 8 or higher is required for ISO mounting functionality

### Future Improvements

- Additional automation for ISO file detection
- Better handling of multi-disc games
- Improved organization of game collections with DLC and updates

We hope these improvements make EmuLibrary more reliable and user-friendly for your game collections. As always, feedback and issue reports are welcome through GitHub or the Playnite Discord.