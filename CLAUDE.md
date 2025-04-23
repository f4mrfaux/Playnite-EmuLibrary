# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> **IMPORTANT NOTE**: This project is developed on Arch Linux but built and deployed on Windows. Do not attempt to build the project on Linux as it will fail due to Windows-specific dependencies.

## Project Overview
EmuLibrary is a library extension for Playnite, an open source video game library manager, focused on emulator ROM management. It allows users to "install" games from one or more folders of ROMs/Disc images to local storage, and helps manage large collections of emulated games.

## Build Commands
- Build solution: `msbuild EmuLibrary.sln /p:Configuration=Release`
- Pack extension: Execute post-build event - `toolbox\toolbox.exe pack $(TargetDir) $(SolutionDir)`
- The packing is also automatically executed as a post-build event in the project

## Development Environment
- Required SDK: Playnite SDK 6.4.0 (included in toolbox directory)
- Target Framework: .NET Framework 4.6.2

## Critical Dependency Management
- NEVER change dependency versions without careful consideration 
- NEVER upgrade Newtonsoft.Json above version 10.0.1 - Playnite requires exactly version 10.0.1
- NEVER enable binding redirects - this will break compatibility with Playnite
- NEVER add dependencies that conflict with Playnite's dependencies
- ALWAYS test on Windows after changing dependencies

## Dependencies (EXACT VERSIONS REQUIRED)
  - ini-parser 2.5.2
  - LibHac 0.7.0
  - Newtonsoft.Json 10.0.1 (CRITICAL: Must be EXACTLY 10.0.1 to match Playnite)
  - protobuf-net 3.1.25
  - System.IO.Abstractions 2.1.0.227
  - ZstdSharp.Port 0.6.5

## CRITICAL DEPENDENCY MANAGEMENT
- NEVER CHANGE OR UPDATE ANY DEPENDENCY VERSIONS - they must match exactly what's shown above
- NEVER ADD NEW NUGET PACKAGES OR REFERENCES even if you encounter compilation errors
- DO NOT USE SYSTEM.NET.HTTP or other modern .NET namespaces that aren't compatible with .NET Framework 4.6.2
- AVOID USING ASYNC/AWAIT features or other C# language features not supported in .NET Framework 4.6.2
- Work around missing features by implementing alternative solutions that work with the existing dependencies
- If new functionality requires libraries not available, implement the functionality using basic .NET Framework classes

## Debugging
- Log files for troubleshooting (relative to Playnite data directory):
  - playnite.log
  - extensions.log
  - library\emulators.db
  - library\platforms.db
  - ExtensionsData\41e49490-0583-4148-94d2-940c7c74f1d9\config.json

## Extension Information
- Extension ID: ROMInstaller_41e49490-0583-4148-94d2-940c7c74f1d9
- Extension Type: GameLibrary
- Required API Version: 6.4.0
- Version numbering: Use semantic versioning (major.minor.patch)

## Coding Guidelines
- Use PascalCase for classes, methods, properties, and public members
- Use camelCase with underscore prefix for private fields (_fieldName)
- Use 4 spaces for indentation; braces on new lines for classes/methods
- Organize imports: System namespaces first, then Playnite SDK, then project-specific
- Use strong typing throughout with proper interfaces and generics
- Use LINQ for collection operations where appropriate
- Implement defensive coding with null checks and proper exception handling
- Log errors using the Playnite Logger class (Logger.Error, Logger.Warn)
- Follow existing patterns for new code (see similar files for examples)
- Target .NET Framework 4.6.2
- Use WPF for UI components

## Playnite SDK API Key Interfaces

### IPlayniteAPI
- Key entry point for all Playnite functionality
- Main interfaces:
  - **Database**: Access to game library via IGameDatabaseAPI (Games, Platforms, Emulators collections)
  - **Dialogs**: User interaction dialogs and prompts
  - **Notifications**: System for displaying notifications to users
  - **Paths**: Access to important file system paths
  - **Emulation**: Emulator-specific functionality (includes access to Platforms and EmulatedPlatforms collections)
  - **MainView**: UI view manipulation
  - **ApplicationInfo**: Information about Playnite's current state and mode
  - **ApplicationSettings**: Access to Playnite configuration

### Game Database Operations
- Use PlayniteAPI.Database.Games collection for CRUD operations
- Important patterns:
  - Get games: `PlayniteApi.Database.Games` collection
  - Add games: `PlayniteApi.Database.Games.Add(game)`
  - Update games: `PlayniteApi.Database.Games.Update(game)`
  - Remove games: `PlayniteApi.Database.Games.Remove(gameId)`
  - Use `BufferedUpdate()` for bulk operations: 
    ```csharp
    using (PlayniteApi.Database.BufferedUpdate())
    {
        // Multiple database operations here
    }
    ```

### Game Files and Resources
- Image handling:
  - `GetFullFilePath(fileId)`: Get full path to a file stored in the database
  - `AddFile(path, gameId)`: Add a file to the database
  - `RemoveFile(fileId)`: Remove a file from the database

## Extension Development Patterns

### Code Modification Guidelines
- Only modify files directly related to the PCInstaller and ISOInstaller functionality when extending these features
- Do not modify the core Yuzu, SingleFile, or MultiFile scanner classes unless absolutely necessary
- Focus changes on the specific RomType implementation being enhanced or fixed
- If a core component needs modification to support new features, discuss it first

### LibraryPlugin Implementation
- Must override mandatory members:
  - `Id`: Unique plugin GUID
  - `Name`: Library name displayed to users
  - `GetGames()`: Core method returning available games

### Game Handling Best Practices
- Game objects require the following fields for proper integration:
  - `GameId`: Unique identifier for each game within the plugin
  - `PluginId`: Must match the plugin's ID
  - `PlayAction`: Action configuration used to start the game (when installed)
  - `InstallDirectory`: Path to installed game files (when installed)

### Game State Management
- Reference fields must be updated through their own collections
- Game objects must be explicitly updated via `Games.Update()` to persist changes
- Listen to collection change events via `ItemCollectionChanged` or `ItemUpdated`
- For library updates, implement the `OnLibraryUpdated` event

### Game Processing Performance
- Use buffered updates to reduce events and improve performance:
  ```csharp
  using (PlayniteApi.Database.BufferedUpdate())
  {
      // Multiple database operations here
  }
  ```
- Consider cancellation tokens to properly handle user cancellation:
  ```csharp
  if (args.CancelToken.IsCancellationRequested)
      yield break;
  ```

## Library Plugin Specifics

### Key Lifecycle Methods
- `GetGames(LibraryGetGamesArgs args)`: Main method to return available games
  - Should handle cancellation via `args.CancelToken`
  - Can check application mode via `Playnite.ApplicationInfo.Mode`
- `GetInstallActions(GetInstallActionsArgs args)`: Return controllers for game installation
- `GetUninstallActions(GetUninstallActionsArgs args)`: Return controllers for game uninstallation
- `OnGameInstalled(OnGameInstalledEventArgs args)`: Hook for post-installation actions
- `GetMainMenuItems(GetMainMenuItemsArgs args)`: Add items to Playnite's main menu
- `GetGameMenuItems(GetGameMenuItemsArgs args)`: Add contextual menu items for games
- `OnApplicationStarted(OnApplicationStartedEventArgs args)`: Initialization after Playnite starts

### Game Installation Framework
- Return specialized controllers from `GetInstallActions` and `GetUninstallActions`
- Controllers handle the actual installation/uninstallation process
- Track installation state via `Game.IsInstalled` and `Game.IsInstalling` properties
- Use notification system to inform users of completed operations:
  ```csharp
  Playnite.Notifications.Add(id, message, NotificationType.Info)
  ```

### Rom Types Support
- **SingleFile**: For single ROM files (.nes, .sfc, etc.)
- **MultiFile**: For games with multiple files in subfolders (multi-disc games)
- **Yuzu**: For Nintendo Switch games (special handling for NAND and updates)
- **PCInstaller**: For PC game installers (.exe files)
  - Special platform handling in EmulatorMapping.cs for PC games
  - Shows PC platforms regardless of emulator profile selection
  - Only supports .exe files as specified in EmulatorMapping.cs
  - Bypasses some emulator-specific validations
- **ISOInstaller**: For disc images requiring mounting and installation
  - Similar to PCInstaller but handles disc image formats
  - Supports common ISO formats like .iso, .bin/.cue, .mdf/.mds
  - Mounts disc images before executing installers
- **ArchiveInstaller**:
  - Handles archives containing ISOs with installers
  - Supports common archive formats (.zip, .rar, .7z)
  - Handles multi-part archives (especially split RAR files)
  - Requires 7-Zip executable to be in PATH
  - Imports all assets locally before any operations (extraction, mounting, installation)
  - No direct network operations for extraction/mounting
  - Multi-step workflow:
    1. Import archive to local temp storage
    2. Extract archive locally using 7-Zip
    3. Find and select ISO files from extracted content
    4. Mount ISO file
    5. Run installer from mounted ISO
    6. Clean up temp files after installation
  - Special handling for multi-part archives requiring parent directory import
  - Password support for protected archives

### Game Metadata Structure
- Create specialized GameInfo classes for different game types
- Implement proper serialization/deserialization of game information
- Use extension methods on Game objects for type-specific operations

## Extension Configuration

### Manifest Requirements (extension.yaml)
- Required fields:
  - `Id`: Unique string identifier (must be consistent with plugin's GUID)
  - `Name`: Extension name displayed to users
  - `Author`: Extension creator's name
  - `Version`: Must follow .NET version format (major.minor.patch)
  - `Module`: DLL filename (e.g., "EmuLibrary.dll")
  - `Type`: Must be "GameLibrary" for library plugins
- Optional fields:
  - `Icon`: Path to icon file relative to extension directory
  - `Links`: List of URLs for documentation, source code, etc.

### Settings Implementation
- Create a settings class that implements `ISettings`
- Override `GetSettings()` to return the settings instance
- Create a WPF UserControl for the settings UI
- Override `GetSettingsView()` to return the settings UI control
- Use data binding between settings model and view
- Consider supporting validation of user-provided settings

### Extension Capabilities
- Implement `Properties` to declare additional capabilities:
  - `CanShutdownClient`: Indicates the library can shut down its client
  - `HasCustomizedGameImport`: Specifies custom import control
  - `HasSettings`: Indicates the plugin has configurable settings

## Testing and Debugging

### Logging
- Use the plugin's Logger for detailed diagnostics:
  ```csharp
  Logger.Info("Informational message");
  Logger.Warn("Warning message");
  Logger.Error("Error message");
  ```
- Log files location (relative to Playnite data directory):
  - playnite.log
  - extensions.log
  - library\emulators.db
  - library\platforms.db
  - ExtensionsData\41e49490-0583-4148-94d2-940c7c74f1d9\config.json

### Application Arguments
- `--start <gameId>`: Starts the game with the specified library ID
- `--nolibupdate`: Skips library update on startup
- `--startdesktop`: Forces the application to start in Desktop mode
- `--startfullscreen`: Forces the application to start in Fullscreen mode
- `--forcesoftrender`: Forces the application to use software render
- `--forcedefaulttheme`: Forces the application to use the default theme
- `--hidesplashscreen`: Won't show startup splash screen
- `--clearwebcache`: Clears web (CEF) cache on startup
- `--shutdown`: Shuts down any existing instances of Playnite
- `--safestartup`: Start Playnite in Safe Mode
- `--backup`: Backup Playnite data
- `--restorebackup`: Restore Playnite data from backup

### URI Commands
- `playnite://playnite/start/<gameId>`: Start a game
- `playnite://playnite/showgame/<gameId>`: Show game details
- `playnite://playnite/search/<term>`: Open global search
- Extensions can register custom commands via Playnite SDK

### Game Variables
The following variables can be used in game links, emulator configuration fields, and game action fields by encapsulating them with curly brackets:

- `{InstallDir}`: Game installation directory
- `{InstallDirName}`: Name of installation folder
- `{ImagePath}`: Game ISO/ROM path if set
- `{ImageName}`: Game ISO/ROM file name
- `{ImageNameNoExt}`: Game ISO/ROM file name without extension
- `{PlayniteDir}`: Playnite's installation directory
- `{Name}`: Game name
- `{Platform}`: Game's platform
- `{GameId}`: Game's ID
- `{DatabaseId}`: Game's database ID
- `{PluginId}`: Game's library plugin ID
- `{Version}`: Game version
- `{EmulatorDir}`: Emulator's installation directory

Example usage: `some string {GameId} test`

### Custom Directory Open Command
By default, Playnite uses Explorer to navigate to file system directories. This behavior can be overwritten via the "Folder open command" option in the Advanced section of settings.

The option takes a shell command to be executed when a directory open action is invoked. The actual directory is passed to the command string via the `{Dir}` variable.

Example (Total Commander): `"c:\Programs\totalcmd\TOTALCMD64.EXE" /L="{Dir}" /O /T`

### Development Workflow
- Build and test in debug mode for detailed error information
- Use the post-build event to automatically pack the extension
- Test in both Desktop and Fullscreen modes
- Validate plugin behavior with different Playnite API versions

### Debugging UI and Settings
- Create debug menu options to inspect internal state
- Add verbose logging during critical operations
- Consider adding diagnostic tools as plugin menu items:
  ```csharp
  yield return new GameMenuItem()
  {
      Action = (args) => { /* Show debug info */ },
      Description = "Show Debug Info...",
      MenuSection = "EmuLibrary"
  };
  ```

### Error Handling
- Implement proper exception handling around file operations
- Validate input paths and configurations before use
- Use defensive programming when accessing external resources
- Provide clear error messages to users when operations fail
- Consider rolling back partial operations when a process fails

### Network and File Operation Best Practices
- Always import installation assets to local temp storage before operations
- Never attempt to extract archives directly over network connections
- Never attempt to mount disc images directly from network locations
- Never execute installers directly from network locations
- Implement proper progress reporting for large file operations
- Handle network disconnections gracefully during file operations
- Ensure proper cleanup of temp files after operations complete
- Implement timeout handling for long-running operations

### Asset Import System
- `AssetImporter` class handles importing assets to local temp storage
  - Always use AssetImporter for network file operations
  - Provides caching for better performance
  - Supports progress reporting via events
  - Handles network retry logic automatically
  - Implements asset verification
  - Example usage:
    ```csharp
    // Get or create AssetImporter instance
    var assetImporter = AssetImporter.Instance ?? 
        new AssetImporter(_logger, _playnite);
        
    // Register for progress updates
    assetImporter.ImportProgress += (sender, e) => {
        UpdateProgress($"Importing: {e.BytesTransferred / (1024 * 1024)} MB / {e.TotalBytes / (1024 * 1024)} MB", 
            (int)(e.Progress * 100));
    };
    
    // Import the asset (with or without progress dialog)
    var importResult = await assetImporter.ImportAsync(
        sourcePath, 
        showWindowsDialog, 
        cancellationToken);
        
    if (importResult.Success) {
        // Use the local asset path: importResult.Path
        // Check if from cache: importResult.FromCache
    }
    
    // Clean up when done (if not cached)
    assetImporter.CleanupTempDirectory(importResult.Path);
    ```

### File Copy System
- `FileCopier` classes handle file copy operations with progress reporting
  - `BaseFileCopier`: Abstract base class with common functionality
  - `SimpleFileCopier`: Implementation for standard file copy
  - `WindowsFileCopier`: Shows Windows copy dialog
  - Supports cancellation via CancellationToken
  - Reports progress via IProgress<FileCopyProgress>
  - Example usage:
    ```csharp
    // Choose copy implementation based on user preferences
    IFileCopier copier = showDialog 
        ? new WindowsFileCopier(sourceInfo, destinationDir)
        : new SimpleFileCopier(sourceInfo, destinationDir);
        
    // Copy with progress reporting
    var progress = new Progress<FileCopyProgress>(p => {
        UpdateProgress($"{p.ProgressPercentage}% complete", 
            (int)p.ProgressPercentage);
    });
    
    // Execute copy operation with progress reporting
    await copier.CopyWithProgressAsync(cancellationToken, progress);
    ```

## Recent Changes (ISO Scanner Fix)

We've made several improvements to the ISOInstallerScanner to fix issues with detecting ISO files:

1. Added case variations for supported disc image extensions:
   ```csharp
   var discExtensions = new List<string> { "iso", "bin", "img", "cue", "nrg", "mds", "mdf", "ISO", "BIN", "IMG", "CUE", "NRG", "MDS", "MDF" };
   ```

2. Added a direct file search diagnostic to verify file presence:
   ```csharp
   // Do a direct folder scan to see if any such files exist
   try {
       var directSearch = Directory.GetFiles(srcPath, "*.*", SearchOption.AllDirectories)
           .Where(f => discExtensions.Contains(Path.GetExtension(f).TrimStart('.').ToLowerInvariant()))
           .ToList();
       
       _emuLibrary.Logger.Info($"Direct search found {directSearch.Count} disc image files in {srcPath}");
       
       if (directSearch.Count > 0) {
           _emuLibrary.Logger.Info($"Examples: {string.Join(", ", directSearch.Take(5).Select(Path.GetFileName))}");
       }
   }
   catch (Exception ex) {
       _emuLibrary.Logger.Error($"Error in direct file search: {ex.Message}");
   }
   ```

3. Improved file extension checking to be more robust:
   ```csharp
   // Check the file extension directly instead of iterating through each extension
   string fileExtension = file.Extension?.TrimStart('.')?.ToLowerInvariant();
   
   _emuLibrary.Logger.Debug($"Checking if file {file.Name} has extension '{fileExtension}'");
   
   // Check if this file has a supported extension
   if (!string.IsNullOrEmpty(fileExtension) && discExtensions.Contains(fileExtension.ToLowerInvariant()))
   ```

4. Enhanced the HasMatchingExtension method in RomTypeScanner for better null handling:
   ```csharp
   protected static bool HasMatchingExtension(FileSystemInfoBase file, string extension)
   {
       // Handle null cases safely
       if (file == null)
           return false;
           
       if (file.Extension == null)
           return extension == "<none>";
           
       // Normalize extensions for comparison
       string fileExt = file.Extension.TrimStart('.').ToLowerInvariant();
       string compareExt = extension.ToLowerInvariant();
       
       // Compare extensions case-insensitively
       return fileExt == compareExt || (file.Extension == "" && extension == "<none>");
   }
   ```

5. Added extensive diagnostic output when no files are found:
   - Logs all top-level files and directories
   - Checks what's inside the first directory 
   - Shows examples of detected files

6. Improved root folder checking to better handle update detection:
   ```csharp
   // Find all valid files in the root game folder to detect base game
   var rootGameFolderFiles = rootGameFolder.GetFiles("*.*", SearchOption.TopDirectoryOnly)
       .Where(f => {
           string ext = Path.GetExtension(f.Name)?.TrimStart('.')?.ToLowerInvariant();
           return !string.IsNullOrEmpty(ext) && discExtensions.Contains(ext);
       })
       .ToList();
       
   _emuLibrary.Logger.Debug($"Root folder check: Found {rootGameFolderFiles.Count} valid files in root folder {rootGameFolder.Name}");
   if (rootGameFolderFiles.Count > 0) {
       _emuLibrary.Logger.Debug($"Examples: {string.Join(", ", rootGameFolderFiles.Take(3).Select(f => f.Name))}");
   }
   ```

7. Enhanced the update file detection to provide better diagnostics:
   ```csharp
   var files = subfolder.GetFiles("*.*", SearchOption.AllDirectories)
       .Where(f => {
           string ext = Path.GetExtension(f.Name)?.TrimStart('.')?.ToLowerInvariant();
           return !string.IsNullOrEmpty(ext) && updateFileExtensions.Contains(ext);
       }).ToList();
       
   _emuLibrary.Logger.Debug($"Update folder check: Found {files.Count} valid files in update folder {subfolder.Name}");
   if (files.Count > 0) {
       _emuLibrary.Logger.Debug($"Examples: {string.Join(", ", files.Take(3).Select(f => f.Name))}");
   }
   ```

These changes should help identify and fix the issue with the ISOInstallerScanner not finding ISO files. The direct file search will show if any ISO files exist in the specified directories, and the enhanced debugging information will help track down where the process is failing.

Note: Since the project must be built on Windows, these changes will need to be transferred to a Windows environment for proper testing and validation.