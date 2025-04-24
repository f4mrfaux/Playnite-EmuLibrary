# ISO Scanner Fix Summary

This document summarizes the changes made to fix issues with ISO games not appearing in the Playnite UI despite being correctly detected by the scanner.

## Root Causes

1. **Plugin ID Issue**: GameMetadata objects didn't have their PluginId set properly after import
2. **Game ID Conflicts**: Multiple games could have the same GameId, causing conflicts
3. **Platform Assignment**: ISO games weren't consistently assigned to the PC platform
4. **Duplicate Handling**: Games with the same name were not properly cleared before adding new ones
5. **GameID Format**: The GameId format needed to include a timestamp to ensure uniqueness

## Key Fixes

### 1. Improved GameId Generation (`ELGameInfo.cs`)

Added timestamp to ensure uniqueness of GameIds:

```csharp
// Generate the Game ID with a timestamp to ensure uniqueness
return string.Format("!0{0}_{1}", Convert.ToBase64String(ms.ToArray()), DateTime.Now.Ticks);
```

Also updated the corresponding deserialization code:

```csharp
// Strip off any timestamp suffix if present (format: base64data_timestamp)
string base64Part = gameId.Substring(2);
int underscorePos = base64Part.LastIndexOf('_');
if (underscorePos > 0)
{
    base64Part = base64Part.Substring(0, underscorePos);
}
```

### 2. Platform Handling in `ISOInstallerScanner.cs`

Changed platform assignment to match PCInstallerScanner's approach:

```csharp
// EXACTLY like PCInstallerScanner - use the platform from mapping
string platformName = mapping.Platform?.Name;

if (string.IsNullOrEmpty(platformName))
{
    platformName = "PC"; // Default fallback
}
```

### 3. Added PluginId Fix in `EmuLibrary.cs`

Created utility method to ensure PluginId is set correctly:

```csharp
private void EnsurePluginId(Game game)
{
    if (game == null)
    {
        Logger.Error("Cannot set PluginId on null game object");
        return;
    }
    
    if (game.PluginId != Id)
    {
        Logger.Info($"Setting PluginId for game '{game.Name}' to {Id} (was: {game.PluginId})");
        game.PluginId = Id;
        
        // Update the game in the database to save the PluginId
        try
        {
            PlayniteApi.Database.Games.Update(game);
            Logger.Info($"Successfully updated PluginId for game '{game.Name}' in database");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to update PluginId for game '{game.Name}': {ex.Message}");
        }
    }
}
```

### 4. Special ISO Game Import Flow

Modified GetGames method to handle ISO games differently:

```csharp
// For ISO games, process import directly for better control
if (mapping.RomType == RomType.ISOInstaller)
{
    // Check if we already have a game with this name to avoid duplicates
    var existingGames = PlayniteApi.Database.Games
        .Where(existing => existing.Name == g.Name && existing.PluginId == Id)
        .ToList();
        
    if (existingGames.Any())
    {
        // Remove existing games with the same name to avoid duplicates
        Logger.Warn($"Removing {existingGames.Count} existing games with name '{g.Name}' to avoid duplicates");
        PlayniteApi.Database.Games.Remove(existingGames);
    }
    
    // Import the game metadata to get a Game object
    var game = PlayniteApi.Database.ImportGame(g);
    
    // Use utility method to ensure PluginId is set correctly
    EnsurePluginId(game);
    
    // Only return if import failed to prevent duplicates
    if (!importSuccess)
    {
        yield return g;
    }
}
else
{
    // For non-ISO games, return normally and let Playnite handle the import
    yield return g;
}
```

### 5. Added Method to Fix Existing Games

Created FixISOGamesPluginId() method to scan and fix existing ISO games on application startup:

```csharp
private void FixISOGamesPluginId()
{
    try
    {
        Logger.Info("Scanning for ISO games with incorrect PluginId...");
        
        // Get all games that might be ISO installer games based on their GameId
        var potentialISOGames = PlayniteApi.Database.Games
            .Where(g => g.GameId != null && g.GameId.Contains("ISOInstaller"))
            .ToList();
            
        Logger.Info($"Found {potentialISOGames.Count} potential ISO games based on GameId");
        
        int fixedGames = 0;
        
        using (PlayniteApi.Database.BufferedUpdate())
        {
            foreach (var game in potentialISOGames)
            {
                if (game.PluginId != Id)
                {
                    // Game has the wrong PluginId, fix it
                    Logger.Warn($"Fixing PluginId for ISO game '{game.Name}' from {game.PluginId} to {Id}");
                    game.PluginId = Id;
                    PlayniteApi.Database.Games.Update(game);
                    fixedGames++;
                }
            }
        }
        
        if (fixedGames > 0)
        {
            Logger.Info($"Fixed PluginId for {fixedGames} ISO games");
            Playnite.Notifications.Add(
                "EmuLibrary-ISO-FixedPluginIds",
                $"Fixed {fixedGames} ISO games that had incorrect plugin IDs. They should now appear in your library.",
                NotificationType.Info);
        }
    }
    catch (Exception ex)
    {
        Logger.Error($"Error fixing ISO games PluginId: {ex.Message}");
    }
}
```

### 6. Added Game Name Cleaning

Added better game name cleaning to remove version numbers and clean up titles:

```csharp
private string CleanGameNameRemoveVersions(string gameName)
{
    if (string.IsNullOrEmpty(gameName))
        return gameName;
    
    // Remove version numbers with v prefix (v1.0, v1.2.3, etc.)
    gameName = System.Text.RegularExpressions.Regex.Replace(gameName, @"v\d+(\.\d+)*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    
    // Remove version numbers in parentheses ((1.0), (1.2.3), etc.)
    gameName = System.Text.RegularExpressions.Regex.Replace(gameName, @"\(\d+(\.\d+)*\)", "");
    
    // Remove version numbers with dots (1.0, 1.2.3, etc.)
    gameName = System.Text.RegularExpressions.Regex.Replace(gameName, @"\s+\d+\.\d+(\.\d+)*", "");
    
    // Remove "Ultimate Edition", "Definitive Edition", etc.
    string[] editionSuffixes = new[] { 
        "Ultimate Edition", "Definitive Edition", "Game of the Year Edition", 
        "Complete Edition", "Deluxe Edition", "Digital Edition",
        "Remastered", "Special Edition"
    };
    
    foreach (var suffix in editionSuffixes)
    {
        if (gameName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            gameName = gameName.Substring(0, gameName.Length - suffix.Length).Trim();
        }
    }
    
    // Clean up spaces and other special characters
    gameName = gameName.Trim();
    
    // Remove trailing dots, dashes, and underscores
    gameName = gameName.TrimEnd('.', '-', '_', ' ');
    
    return gameName;
}
```

### 7. Enhanced Folder Name Detection

Added more intelligent handling of folder names:

```csharp
// Try to find the best folder name to use
string selectedFolderName = parentFolder;

// If parent folder is problematic, try to find a better folder name by going up levels
if (problematicFolders.Contains(parentFolder) || parentFolder.StartsWith("setup.part"))
{
    _emuLibrary.Logger.Info($"[ISO SCANNER] Parent folder '{parentFolder}' is problematic, looking for better name");
    
    // Try to find first non-problematic folder going up the tree
    var currentPath = parentFolderPath;
    while (currentPath != null && currentPath.Length > srcPath.Length)
    {
        var currentDir = Path.GetFileName(currentPath);
        _emuLibrary.Logger.Info($"[ISO SCANNER] Checking folder: {currentDir}");
        
        if (!problematicFolders.Contains(currentDir) && !currentDir.StartsWith("setup.part"))
        {
            selectedFolderName = currentDir;
            _emuLibrary.Logger.Info($"[ISO SCANNER] Found suitable folder name: {selectedFolderName}");
            break;
        }
        
        currentPath = Path.GetDirectoryName(currentPath);
    }
}
```

### 8. Added Notifications for Visibility Debugging

Added notifications to help verify that games are being properly imported:

```csharp
// Notify user of ISO game addition to verify visibility
Playnite.Notifications.Add(
    $"EmuLibrary-ISO-GameAdded-{Guid.NewGuid()}",
    $"Added ISO game: {g.Name}",
    NotificationType.Info);
```

## Results

These changes should ensure that:

1. All ISO games have unique GameIds
2. All ISO games have the correct PluginId after import
3. Existing ISO games with incorrect PluginIds are fixed on startup
4. No duplicate games are created when the same game is detected multiple times
5. Games are properly assigned to the PC platform

With these fixes, ISO games should now appear in the Playnite UI just like PC installer games.