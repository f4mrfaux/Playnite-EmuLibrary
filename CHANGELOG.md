## [Unreleased]

## [0.8.1] - 2025-04-02
### Changed
- Renamed plugin to EmuLibrary PC Manager to better reflect its purpose

### Fixed
- Fixed build errors in GogInstaller and PcInstaller classes
- Fixed class accessibility inconsistencies between implementation and base classes
- Implemented missing abstract methods in GogInstaller classes
- Added proper delegate definition for progress updates
- Fixed duplicate logger field declarations with 'new' keyword
- Added missing namespace imports for System types
- Implemented missing GetPluginUserDataPath method in EmuLibrary class
- Fixed missing catch block in PcInstallerInstallController.cs
- Fixed Game.GameImagePath usage in GogInstallerInstallController to properly create GameInstallationData
- Fixed misspelled parameter name 'arags' to 'args' in action delegates
- Improved thread safety in notification system across all controllers
- Enhanced error handling in installer execution process

## [0.8.0] - 2025-04-02
### Major Improvements
- Fixed critical build errors preventing compilation
- Resolved compatibility issues with Playnite plugin architecture
- Enhanced threading model and error handling
- Updated codebase to follow Playnite SDK best practices

### Improvements
- Enhanced user interface with informative panels and tooltips for better guidance
- Added detailed plugin description in extension.yaml for better discoverability
- Improved notifications during installation process with more helpful messages
- Performance optimizations with caching for installer detection and game name extraction
- Added comprehensive troubleshooting guide in README.md
- Improved documentation with a Quick Start guide and folder structure recommendations
- Added ability to select custom executables for games with launchers or when auto-detection selects the wrong file
- Added option to prompt for installer selection when multiple options are found in archives
- Added option to prompt for installation location when executable detection fails
- Improved archive handling with game-specific temporary directories
- Enhanced threading model with proper UI thread handling and exception management
- Added cross-platform support for file browsing operations
- Fixed resource cleanup to ensure proper disposal of temporary resources
### Security
- Updated Newtonsoft.Json from 10.0.1 to 13.0.3 to address high severity vulnerability (GHSA-5crp-9r3c-p9vr)
- Reverted to the original LibHac version 0.7.0 which is compatible with .NET Framework 4.6.2
- Downgraded protobuf-net from 3.1.25 to 2.4.6 for better .NET Framework 4.6.2 compatibility

### Fixed
- Fixed compatibility issues with .NET Framework 4.6.2 in archive handling code
- Addressed thread safety issues by properly using UIDispatcher for UI updates
- Added missing BaseUninstallController implementation
- Fixed plugin ID format in extension.yaml to follow Playnite conventions
- Improved error handling in file extraction and installation process
- Added fallback methods for finding 7z.exe and UnRAR.exe in various locations including PATH
- Enhanced installer detection for better success rates with different archive types
- Added timeout protection for installer processes that might hang
- Improved robustness of directory copying with better error handling
- Strengthened LINQ usage patterns for better .NET Framework 4.6.2 compatibility
- Fixed potential issues with FirstOrDefault() by using safer collection access patterns
- Enhanced cancellation handling in long-running processes
### Added
- Added generic PC game installer support to allow management of PC game installers
- New PcInstaller ROM type that can detect and handle various installer types (.exe, .msi)
- Added GOG installer support to allow management of GOG game installers
- New GogInstaller ROM type that can detect and handle GOG-specific installers
- Support for silent installation and uninstallation of PC games
- Automatic game executable detection after installation
- Support for network shares via SMB as sources for game installers
- Settings for customizing PC installer behavior and installation locations
- Added metadata integration with Playnite to enable automatic metadata downloads
- Enhanced game title extraction for better metadata matching with Steam/GOG databases
- Added option to use folder names instead of installer filenames for improved metadata matching

### Notes
- The PC Installer functionality allows managing a collection of PC game installers stored on network shares
- Games can be installed on-demand when you want to play them, saving local disk space
- The plugin will automatically detect the correct executable to launch after installation
- Supported installer types include InnoSetup, NSIS, InstallShield, and MSI installers
- Added support for ISO files containing installers
- Added support for multi-part RAR archives containing ISO files with game installers

### How to Use
1. Add a new mapping in settings with "PcInstaller" as the Rom Type
2. Set the source path to a network or local folder containing your game installers
3. Set the destination path to where you want games to be installed
4. Games will appear in your library and can be installed by clicking "Play"
5. After installation, the game will launch using the detected executable
6. Enable "Metadata Settings" options in the plugin settings to automatically download metadata for your games
7. Use the "Use folder names for metadata matching" option for better metadata matches when your folder structure contains clean game titles

### Advanced Archive Support
- ISO files: The plugin can extract ISO files and run installers found inside them
- Multi-part RAR archives: The plugin can handle multi-part RAR archives (e.g., game.part1.rar, game.part2.rar)
- Nested archives: The plugin can handle RAR archives containing ISO files with installers
- Required tools: Place 7z.exe and UnRAR.exe in the Tools directory of the plugin or other detected locations
- The plugin will automatically detect tools from multiple possible locations:
  - The Tools directory inside the plugin folder (recommended)
  - The plugin's root directory
  - The parent directory's Tools folder
  - Your system PATH
- Improved error handling ensures successful operation even in challenging scenarios
