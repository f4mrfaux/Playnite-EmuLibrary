# PCInstaller Unified Handler - Testing Checklist

## Overview
PCInstaller now handles all PC game formats in a unified way:
- Direct EXE files
- Direct ISO files  
- Archives (ZIP/RAR/7Z) containing EXE
- Archives (ZIP/RAR/7Z) containing ISO
- Multi-part RAR files

## Prerequisites
- 7-Zip installed (for archive extraction)
- Test files prepared:
  - Direct EXE installer
  - Direct ISO file
  - ZIP archive containing EXE
  - ZIP archive containing ISO
  - RAR archive (single or multi-part) containing EXE
  - RAR archive (single or multi-part) containing ISO

## Test Scenarios

### 1. Direct EXE File Installation
**Setup:**
- Create mapping: RomType = PCInstaller
- Source path: Folder containing .exe file
- Scan library

**Expected:**
- ✅ Game appears in library with "Install" action
- ✅ Click Install → Runs installer
- ✅ Prompts for installation directory
- ✅ After installation, game shows "Play" action
- ✅ Primary executable detected and set

**Test:**
- [ ] Scan finds EXE file
- [ ] Game metadata correct
- [ ] Installation works
- [ ] Play action works after install

---

### 2. Direct ISO File Installation
**Setup:**
- Create mapping: RomType = PCInstaller
- Source path: Folder containing .iso file
- Scan library

**Expected:**
- ✅ Game appears in library with "Install" action
- ✅ Click Install → Mounts ISO
- ✅ Finds installer in mounted disc
- ✅ Prompts for installation directory
- ✅ Runs installer from mounted disc
- ✅ Unmounts ISO after installation
- ✅ Primary executable detected and set

**Test:**
- [ ] Scan finds ISO file
- [ ] Game metadata correct
- [ ] ISO mounts successfully
- [ ] Installer found and runs
- [ ] ISO unmounts after install
- [ ] Play action works after install

---

### 3. Archive (ZIP) Containing EXE
**Setup:**
- Create mapping: RomType = PCInstaller
- Source path: Folder containing .zip file with .exe inside
- Scan library

**Expected:**
- ✅ Game appears in library with "Install" action
- ✅ Click Install → Extracts ZIP to temp
- ✅ Detects EXE in extracted content
- ✅ Runs installer
- ✅ Prompts for installation directory
- ✅ Cleans up temp directory
- ✅ Primary executable detected and set

**Test:**
- [ ] Scan finds ZIP file
- [ ] Game metadata correct
- [ ] Archive extracts successfully
- [ ] EXE detected in extracted content
- [ ] Installation works
- [ ] Temp directory cleaned up
- [ ] Play action works after install

---

### 4. Archive (ZIP) Containing ISO
**Setup:**
- Create mapping: RomType = PCInstaller
- Source path: Folder containing .zip file with .iso inside
- Scan library

**Expected:**
- ✅ Game appears in library with "Install" action
- ✅ Click Install → Extracts ZIP to temp
- ✅ Detects ISO in extracted content
- ✅ Mounts ISO
- ✅ Finds installer in mounted disc
- ✅ Prompts for installation directory
- ✅ Runs installer
- ✅ Unmounts ISO
- ✅ Cleans up temp directory
- ✅ Primary executable detected and set

**Test:**
- [ ] Scan finds ZIP file
- [ ] Game metadata correct
- [ ] Archive extracts successfully
- [ ] ISO detected in extracted content
- [ ] ISO mounts successfully
- [ ] Installer found and runs
- [ ] ISO unmounts
- [ ] Temp directory cleaned up
- [ ] Play action works after install

---

### 5. Multi-Part RAR Containing EXE
**Setup:**
- Create mapping: RomType = PCInstaller
- Source path: Folder containing game.part1.rar, game.part2.rar, etc.
- Scan library

**Expected:**
- ✅ Game appears in library with "Install" action
- ✅ Click Install → Extracts all RAR parts
- ✅ Detects EXE in extracted content
- ✅ Runs installer
- ✅ Installation completes successfully

**Test:**
- [ ] Scan finds first RAR part
- [ ] Game metadata correct
- [ ] All RAR parts extracted
- [ ] EXE detected
- [ ] Installation works

---

### 6. Multi-Part RAR Containing ISO
**Setup:**
- Create mapping: RomType = PCInstaller
- Source path: Folder containing game.part1.rar with ISO inside
- Scan library

**Expected:**
- ✅ Game appears in library with "Install" action
- ✅ Click Install → Extracts all RAR parts
- ✅ Detects ISO in extracted content
- ✅ Mounts ISO and installs
- ✅ Installation completes successfully

**Test:**
- [ ] Scan finds first RAR part
- [ ] Game metadata correct
- [ ] All RAR parts extracted
- [ ] ISO detected and mounted
- [ ] Installation works

---

### 7. Cancellation During Installation
**Test Scenarios:**
- [ ] Cancel during archive extraction
- [ ] Cancel during ISO mounting
- [ ] Cancel during installer execution
- [ ] Cancel during file copy

**Expected:**
- ✅ Process stops cleanly
- ✅ Temp directories cleaned up
- ✅ ISO unmounted if mounted
- ✅ Game state reset (IsInstalling = false)
- ✅ User notification shown

---

### 8. Error Handling
**Test Scenarios:**
- [ ] Missing 7-Zip (archive extraction fails)
- [ ] Corrupted archive
- [ ] ISO mount failure
- [ ] Installer not found in ISO
- [ ] User cancels folder selection

**Expected:**
- ✅ Clear error messages
- ✅ User notifications
- ✅ Proper cleanup
- ✅ Game state reset

---

### 9. Edge Cases
- [ ] Archive containing both ISO and EXE (should prefer ISO)
- [ ] Multiple ISO files in archive (should use first)
- [ ] Multiple EXE files in archive (should find installer)
- [ ] Very large archives (>10GB)
- [ ] Network path sources (slow)

---

## Verification Points

### After Each Installation:
1. ✅ Game shows as "Installed" in Playnite
2. ✅ Play action is available and works
3. ✅ Install directory is set correctly
4. ✅ Primary executable is detected
5. ✅ No temp directories left behind
6. ✅ ISO unmounted (if used)
7. ✅ Game can be played

### Log Files to Check:
- `playnite.log` - General Playnite logs
- `extensions.log` - Extension-specific logs
- Look for:
  - Archive extraction messages
  - ISO mount/unmount messages
  - Installation progress
  - Any errors or warnings

## Known Limitations

1. **Password-Protected Archives**: Not supported yet - will fail with 7-Zip error
2. **Nested Archives**: Not automatically handled - archive in archive won't extract
3. **Progress Reporting**: Limited during extraction (7-Zip output not parsed)

## Success Criteria

✅ All test scenarios pass
✅ No crashes or hangs
✅ Proper cleanup in all cases
✅ Clear error messages when things fail
✅ User experience is smooth and intuitive

