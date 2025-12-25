# PCInstaller vs ISOInstaller Comparison

## Critical Differences Found

### ✅ **What PCInstaller Has (Good)**
- Archive extraction support (ZIP/RAR/7Z)
- ISO mounting/unmounting logic
- Content detection (ISO vs EXE)
- Direct ISO file handling
- Direct EXE file handling
- Primary executable detection
- Stable GameId generation
- GOG/Steam detection

### ⚠️ **What ISOInstaller Has That PCInstaller Might Be Missing**

#### 1. **Multi-Disc Support** ⚠️ CRITICAL
**ISOInstaller:**
- Groups ISO files by folder
- Stores list of all ISO files (`ISOFiles` property)
- Handles multi-disc games (Disc 1, Disc 2, etc.)
- Selects primary ISO intelligently (prioritizes "disc 1", .m3u files, smallest file)

**PCInstaller:**
- Only handles single ISO files
- No grouping by folder
- No multi-disc support

**Impact:** Multi-disc games won't work correctly - only first disc will be detected

#### 2. **Folder-Based Scanning** ⚠️ IMPORTANT
**ISOInstaller:**
- Groups files by parent folder
- One game entry per folder (even if multiple ISOs)
- Uses folder name for game name

**PCInstaller:**
- Processes individual files
- One game entry per file
- Uses parent folder name for game name (same approach)

**Impact:** Multi-disc games will create multiple game entries instead of one

#### 3. **Extracted Content Filtering** ⚠️ IMPORTANT
**ISOInstaller:**
- Filters out extracted game content folders
- Detects system folders (Windows, Program Files, etc.)
- Skips folders with too many files/folders (likely extracted)

**PCInstaller:**
- No extracted content filtering
- Will scan extracted game folders as if they're installers

**Impact:** May create duplicate entries for extracted games

#### 4. **InstallSize Calculation** ⚠️ MINOR
**ISOInstaller:**
- Calculates from all ISO files in folder (sum)
- More accurate for multi-disc games

**PCInstaller:**
- Only calculates from single file
- Missing for ISO files (only set for EXE)

**Impact:** Install size may be inaccurate for multi-disc games

#### 5. **SourceFullPath Fallback Logic** ⚠️ MINOR
**ISOInstaller:**
- Checks ISOFiles list if primary path doesn't exist
- Searches directory for any ISO file as last resort
- More robust path resolution

**PCInstaller:**
- Simpler logic, just combines paths
- No fallback searching

**Impact:** May fail if file moved/renamed

#### 6. **UninstallController Differences** ⚠️ MINOR
**ISOInstaller:**
- More comprehensive uninstaller detection (pattern matching)
- Automatically deletes directory if no uninstaller found
- Updates game state properly

**PCInstaller:**
- Simpler uninstaller detection (only 3 specific names)
- Asks user before deleting directory
- Less automatic

**Impact:** Uninstall might be less smooth

#### 7. **Primary ISO Selection Logic** ⚠️ MINOR
**ISOInstaller:**
- Intelligent selection: "disc 1" > .m3u > smallest file
- Handles multi-disc scenarios

**PCInstaller:**
- Just uses first ISO found
- No prioritization

**Impact:** Might select wrong disc for multi-disc games

## Recommendations

### High Priority Fixes

1. **Add Multi-Disc Support to PCInstaller**
   - Group ISO files by folder in scanner
   - Store list of ISO files in GameInfo (or reuse existing structure)
   - Select primary ISO intelligently
   - Handle disc switching during installation if needed

2. **Add Extracted Content Filtering**
   - Port the filtering logic from ISOInstallerScanner
   - Skip system folders and extracted game content
   - Prevent duplicate entries

3. **Fix InstallSize for ISO Files**
   - Calculate from all ISO files if multi-disc
   - Set InstallSize in scanner for ISO files

### Medium Priority

4. **Improve SourceFullPath Fallback**
   - Add search logic if primary path doesn't exist
   - Check for moved/renamed files

5. **Enhance UninstallController**
   - Add pattern-based uninstaller detection
   - Better automatic cleanup

### Low Priority

6. **Primary ISO Selection**
   - Add intelligent selection logic
   - Prioritize "disc 1" and .m3u files

## Current Status Assessment

**PCInstaller is ~85% complete** compared to ISOInstaller functionality:
- ✅ Core ISO handling works
- ✅ Archive support is better (new feature)
- ⚠️ Missing multi-disc support (critical for some games)
- ⚠️ Missing extracted content filtering (may cause duplicates)
- ⚠️ Minor improvements needed in uninstaller and path resolution

## Action Items

1. [ ] Add multi-disc ISO grouping to PCInstallerScanner
2. [ ] Add extracted content filtering to PCInstallerScanner  
3. [ ] Fix InstallSize calculation for ISO files
4. [ ] Improve SourceFullPath fallback logic
5. [ ] Enhance UninstallController with pattern matching

