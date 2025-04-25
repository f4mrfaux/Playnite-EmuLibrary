# ISO Scanner Fix Notes

This document explains the changes made to fix issues with the ISO scanner. There were two distinct issues:

1. First issue (already fixed): The scanner was not finding ISO files properly due to extension handling problems
2. Second issue (fixed now): ISO games were found correctly but not appearing in the Playnite UI due to platform assignment issues

## Key Changes

### 1. Improved Extension Handling

We've enhanced the ISO detection to properly handle extensions case-insensitively and to handle null values safely:

```csharp
// Added upper case variations
var discExtensions = new List<string> { 
    "iso", "bin", "img", "cue", "nrg", "mds", "mdf",
    "ISO", "BIN", "IMG", "CUE", "NRG", "MDS", "MDF" 
};
```

### 2. Added Direct File Search Diagnostic

Added a diagnostic method that uses `Directory.GetFiles()` to directly scan for ISO files, which provides an independent verification of file existence:

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

### 3. Improved File Check Algorithm

Changed from iterating through each extension to a more efficient single check:

```csharp
// Check the file extension directly instead of iterating through each extension
string fileExtension = file.Extension?.TrimStart('.')?.ToLowerInvariant();

_emuLibrary.Logger.Debug($"Checking if file {file.Name} has extension '{fileExtension}'");

// Check if this file has a supported extension
if (!string.IsNullOrEmpty(fileExtension) && discExtensions.Contains(fileExtension.ToLowerInvariant()))
{
    _emuLibrary.Logger.Info($"Found ISO file: {file.FullName} (matches extension '{fileExtension}')");
    // Processing continues...
}
```

### 4. Enhanced Diagnostics When No Files Found

Added comprehensive diagnostic reports when no games are found:

```csharp
// Additional diagnostic info to help with troubleshooting
try {
    // Print all files in the directory (top level only) to see what's actually there
    var allFiles = Directory.GetFiles(srcPath, "*.*", SearchOption.TopDirectoryOnly);
    _emuLibrary.Logger.Info($"Found {allFiles.Length} files at top level in {srcPath}");
    
    if (allFiles.Length > 0) {
        _emuLibrary.Logger.Info($"Examples: {string.Join(", ", allFiles.Take(10).Select(Path.GetFileName))}");
    }
    
    // Check subdirectories too
    var allDirs = Directory.GetDirectories(srcPath, "*", SearchOption.TopDirectoryOnly);
    _emuLibrary.Logger.Info($"Found {allDirs.Length} subdirectories at top level in {srcPath}");
    
    if (allDirs.Length > 0) {
        _emuLibrary.Logger.Info($"Examples: {string.Join(", ", allDirs.Take(10).Select(Path.GetFileName))}");
        
        // Pick first directory and see what's inside
        if (allDirs.Length > 0) {
            var firstDir = allDirs[0];
            var filesInFirstDir = Directory.GetFiles(firstDir, "*.*", SearchOption.TopDirectoryOnly);
            _emuLibrary.Logger.Info($"First directory '{Path.GetFileName(firstDir)}' contains {filesInFirstDir.Length} files");
            
            if (filesInFirstDir.Length > 0) {
                _emuLibrary.Logger.Info($"Examples: {string.Join(", ", filesInFirstDir.Take(10).Select(Path.GetFileName))}");
            }
        }
    }
}
catch (Exception ex) {
    _emuLibrary.Logger.Error($"Error in diagnostic check: {ex.Message}");
}
```

### 5. Improved Root Folder Detection

Enhanced the code that identifies game and update folders:

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

### 6. Better Null Handling in Extension Detection

Improved the `HasMatchingExtension` method to better handle null values:

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

## Testing Instructions

Since we can't build the project on Linux, you'll need to test these changes on a Windows system:

1. Build the solution on Windows using:
   ```
   msbuild EmuLibrary.sln /p:Configuration=Debug
   ```

2. Run Playnite with the updated extension and enable debug logging

3. Configure an ISOInstaller mapping and check if ISO files are detected

4. The logs should now show detailed diagnostic information about the disk image detection process, including a direct file search result and a list of any files found

5. If problems persist, review the enhanced diagnostic logs to identify why files aren't being detected

## Potential Issues to Check

If ISO scanning still doesn't work after these changes, check these potential issues:

1. Verify file permissions - make sure Playnite has permission to read the files
2. Check if antivirus software is blocking access to the ISO files
3. Ensure the ISO files have proper file extensions (not renamed or missing extensions)
4. Verify the folder structure matches what the scanner expects
5. Check for special characters in filenames or paths that might cause problems

## Notes on Case Sensitivity

The scanner now explicitly handles both lowercase and uppercase extensions. Extensions are normalized to lowercase for comparison, but both variations are included in the lists for clarity and compatibility with different file systems.

## Platform Assignment Fix (New)

We've identified and fixed an issue where ISO games were being detected but not appearing in the Playnite UI. The problem was related to platform assignment.

### Key Changes for Platform Assignment

1. **Consistent Platform Handling in ISOInstallerScanner.cs**

```csharp
// EXACTLY like PCInstallerScanner - use the platform from the mapping
string platformName = mapping.Platform?.Name;

if (string.IsNullOrEmpty(platformName))
{
    platformName = "PC"; // Default fallback
    _emuLibrary.Logger.Info($"[ISO SCANNER] No platform in mapping, using default: {platformName}");
}
else
{
    _emuLibrary.Logger.Info($"[ISO SCANNER] Using platform from mapping: {platformName}");
}
```

2. **Enhanced Platform Lookup in EmuLibrary.cs**

```csharp
// Try to find PC platform
var pcPlatform = Playnite.Database.Platforms
    .FirstOrDefault(p => p.Name == "PC" || p.Name == "Windows" || p.Name == "PC (Windows)");
    
if (pcPlatform != null)
{
    // Always update to the latest platform ID
    mapping.PlatformId = pcPlatform.SpecificationId ?? pcPlatform.Id.ToString();
    // Don't set Platform property directly, PlatformId is used to resolve it
    Logger.Info($"Set platform to {pcPlatform.Name} (ID: {mapping.PlatformId})");
}
```

3. **Fixed Plugin ID Assignment**

```csharp
// Critical - ensure PluginId is set to match our plugin
if (game.PluginId != Id)
{
    Logger.Warn($"PluginId was not correctly set during import. Setting it manually. Original: {game.PluginId}, Expected: {Id}");
    game.PluginId = Id;
    // Update the game in the database
    PlayniteApi.Database.Games.Update(game);
}
```

4. **Added Diagnostic Test Methods**

- New menu item "Debug: Test direct ISO import..." allows testing the entire process
- Creates a GameMetadata object with the proper properties
- Imports directly into Playnite's database
- Shows detailed error messages if something fails

### Testing the Platform Assignment Fix

1. **Use the Debug Menu**:
   - From Playnite, go to the main menu → EmuLibrary → "Debug: Test direct ISO import..."
   - Select an ISO file
   - The game should be imported correctly and appear in your library

2. **Check Regular Scan**:
   - Configure an ISO Installer mapping in EmuLibrary settings
   - Set the platform to "PC (Windows)"
   - Run a library scan
   - Games should appear in the library with the correct platform