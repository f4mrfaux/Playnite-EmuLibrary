# ISO Scanner Fix - Summary of Changes

## Problem Description
ISO games were being detected by the ISOInstallerScanner (with log entries showing files found and game entries created), but the games weren't appearing in the Playnite UI. Logs showed a warning about not being able to find the PC platform in database.

## Root Causes Identified
1. **Platform Assignment**: ISO games needed to consistently use the PC platform ("PC (Windows)" specifically)
2. **Plugin ID Assignment**: The PluginId was not being consistently set on Game objects after import (the GameMetadata class doesn't have this property)
3. **Game Import Process**: Issues with how games were being imported into Playnite and database updates
4. **Platform Metadata Properties**: Inconsistent platform references between settings and game metadata

## Changes Made

### 1. Fixed Platform Handling (Critical Fix)
- Modified the platform assignment in ISOInstallerScanner.cs:
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
- Enhanced platform lookup in EmuLibrary.cs to ensure PC platform is always found:
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

### 2. Fixed Plugin ID Assignment (Most Critical Fix)
- **IMPORTANT INSIGHT:** The GameMetadata class doesn't have a PluginId property - it must be set on the Game object after import.

- **CRITICAL FIX:** Ensure PluginId is set on Game objects after import:
  ```csharp
  var game = PlayniteApi.Database.ImportGame(gameMetadata);
                                            
  // CRITICAL: Explicitly set PluginId after import 
  // This is required for games to appear in Playnite's UI
  game.PluginId = Id;
  
  // Update the game in the database with the corrected PluginId
  PlayniteApi.Database.Games.Update(game);
  ```

- Added detailed logging to verify and troubleshoot the import process:
  ```csharp
  Logger.Info($"Added game: {game.Name} (ID: {game.GameId}) with explicit PluginId: {game.PluginId}");
  ```

- This ensures that the game will be associated with our plugin in Playnite's database, which is required for the game to appear in the UI.

### 3. Added Comprehensive Diagnostics
- Added TestDirectImportISOGames method to directly test game import:
  - Allows selecting an ISO file manually
  - Creates a proper GameMetadata object
  - Imports the game with correct platform and PluginId
  - Verifies the import succeeded
  - Shows detailed errors if something fails

### 4. Enhanced Debug Output
- Added detailed logging of game import process:
  ```csharp
  // Log details before import
  Logger.Info($"TEST IMPORT - Game: {metadata.Name}");
  Logger.Info($"TEST IMPORT - GameId: {metadata.GameId}");
  Logger.Info($"TEST IMPORT - PluginId: {metadata.PluginId}");
  Logger.Info($"TEST IMPORT - Platform: {string.Join(", ", metadata.Platforms)}");
  ```
- Enhanced logging in GetGames method:
  ```csharp
  // Log more details about the game being returned to help with debugging
  Logger.Info($"Found game {gameCount}: {g.Name} (ID: {g.GameId}, Installed: {g.IsInstalled})");
  
  // Ensure the game has the critical GameId property
  if (string.IsNullOrEmpty(g.GameId))
  {
      Logger.Error($"Game {g.Name} has an empty GameId - this will prevent it from appearing in Playnite");
  }
  
  // Check for valid platforms
  if (g.Platforms == null || !g.Platforms.Any())
  {
      Logger.Warn($"Game {g.Name} has no platforms");
  }
  ```

## Testing and Verification

### How to Test the Changes
1. **Via Debug Menu**:
   - Use "Debug: Test direct ISO import..." to manually add an ISO game
   - This directly tests the import process with verbose logging

2. **Regular Library Scan**:
   - Create an ISO mapping in EmuLibrary settings 
   - Set the platform to "PC (Windows)"
   - Point it to a folder containing ISO files
   - Initiate a library scan via Playnite's "Update Library" button

3. **Diagnostic Menu**:
   - Use "Debug: Run ISO Scanner diagnostics..." to perform platform tests

### Expected Results
- ISO games should now be detected and added to Playnite
- Games should correctly use the "PC (Windows)" platform
- The PluginId should be set to the EmuLibrary plugin ID
- Games should appear in the Playnite UI after scanning

## Potential Edge Cases
- If the PC platform doesn't exist in Playnite, the plugin attempts to:
  1. Use "PC" or "Windows" or "PC (Windows)" as a fallback
  2. Create a PC platform if none exists (in diagnostic mode)
  3. Use ANY platform as a last resort
- Very old game entries might need to be deleted and rescanned
- Multiple ISO games with the same folder name might get deduplicated