## [Unreleased]
### Security
- Updated Newtonsoft.Json from 10.0.1 to 13.0.3 to address high severity vulnerability (GHSA-5crp-9r3c-p9vr)
- Updated LibHac to version 0.16.0 for better compatibility with .NET Framework 4.6.2

### Fixed
- Fixed compatibility issues with .NET Framework 4.6.2 in archive handling code
- Improved error handling in file extraction and installation process
- Added fallback methods for finding 7z.exe and UnRAR.exe in various locations
- Enhanced installer detection for better success rates with different archive types
- Added timeout protection for installer processes that might hang
- Improved robustness of directory copying with better error handling
### Added
- Added generic PC game installer support to allow management of PC game installers
- New PcInstaller ROM type that can detect and handle various installer types (.exe, .msi)
- Added GOG installer support to allow management of GOG game installers
- New GogInstaller ROM type that can detect and handle GOG-specific installers
- Support for silent installation and uninstallation of PC games
- Automatic game executable detection after installation
- Support for network shares via SMB as sources for game installers
- Settings for customizing PC installer behavior and installation locations

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
