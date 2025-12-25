# Archive Workflow Documentation

## Expected Workflow

### 1. Archive Detection & Extraction
- **All files** (RAR, ZIP, 7Z, ISO, EXE) are copied/extracted to a **temp directory** first
- Archives are extracted using 7-Zip CLI
- Extraction happens before any installation logic

### 2. Content Detection & Routing

#### If Archive Contains ISO Files:
- **ISOInstaller mapping** → ✅ Handled by ISO handler
  - Extracts archive to temp
  - Finds ISO files in extracted content
  - Uses first ISO found
  - Mounts ISO and runs installer
  - **This is the correct workflow**

- **PCInstaller mapping** → ⚠️ Will detect ISO and warn user
  - Extracts archive to temp
  - Detects ISO files
  - Shows warning: "Archive contains ISO file. Please use ISOInstaller type"
  - Exits installation
  - User should re-scan with ISOInstaller mapping

#### If Archive Contains EXE Files:
- **PCInstaller mapping** → ✅ Handled by PC installer handler
  - Extracts archive to temp
  - Finds EXE files in extracted content
  - Uses first installer EXE found (setup.exe, install.exe, etc.)
  - Runs installer
  - **This is the correct workflow**

- **ISOInstaller mapping** → ❌ Will error
  - Extracts archive to temp
  - Looks for ISO files
  - Finds none → Error: "Extracted archive does not contain any ISO files"
  - User should use PCInstaller mapping instead

### 3. Direct Files (Not Archives)

#### ISO Files:
- **ISOInstaller mapping** → ✅ Direct mounting
  - No extraction needed
  - Mounts ISO directly
  - Runs installer

#### EXE Files:
- **PCInstaller mapping** → ✅ Direct execution
  - Copies to temp
  - Runs installer directly

## Workflow Summary

```
User clicks "Install"
    ↓
Is it an archive? (RAR/ZIP/7Z)
    ↓ YES
Extract to temp directory
    ↓
Detect content type
    ↓
Contains ISO? → Route to ISO handler (if ISOInstaller mapping)
Contains EXE? → Route to PC installer handler (if PCInstaller mapping)
    ↓ NO (not archive)
Is it ISO? → Mount and install (if ISOInstaller mapping)
Is it EXE? → Run installer (if PCInstaller mapping)
```

## Important Notes

1. **Mapping Type Matters**: The user's RomType selection (PCInstaller vs ISOInstaller) determines which scanner detects the file and which handler processes it.

2. **Content-Based Routing**: After extraction, the content type (ISO vs EXE) is detected, but the handler is already determined by the mapping type.

3. **Best Practice**: 
   - Use **ISOInstaller** mapping for archives that contain ISO files
   - Use **PCInstaller** mapping for archives that contain EXE files
   - Both scanners now detect archives, so either can find them, but the handler must match the content

4. **Multi-Part RAR**: Automatically handled by 7-Zip - just specify the first part (.part1.rar or .rar)

## Current Implementation Status

✅ **Working:**
- Archive extraction to temp
- Content detection (ISO vs EXE)
- ISO handler for archives containing ISO (when ISOInstaller mapping used)
- PC installer handler for archives containing EXE (when PCInstaller mapping used)
- Multi-part RAR support

⚠️ **Limitation:**
- If user selects wrong mapping type, they'll get an error message
- No automatic handler switching (would require changing game's RomType dynamically)

## Example Scenarios

### Scenario 1: RAR containing ISO, ISOInstaller mapping
1. User sets up mapping: RomType = ISOInstaller
2. Scanner finds: `game.part1.rar`
3. User clicks Install
4. ✅ Extracts RAR to temp
5. ✅ Finds `game.iso` inside
6. ✅ Mounts ISO
7. ✅ Runs installer from mounted disc

### Scenario 2: RAR containing ISO, PCInstaller mapping (WRONG)
1. User sets up mapping: RomType = PCInstaller
2. Scanner finds: `game.part1.rar`
3. User clicks Install
4. ✅ Extracts RAR to temp
5. ⚠️ Finds ISO files, not EXE
6. ⚠️ Shows warning: "Please use ISOInstaller type"
7. ❌ Installation stops

### Scenario 3: ZIP containing EXE, PCInstaller mapping
1. User sets up mapping: RomType = PCInstaller
2. Scanner finds: `installer.zip`
3. User clicks Install
4. ✅ Extracts ZIP to temp
5. ✅ Finds `setup.exe` inside
6. ✅ Runs installer
7. ✅ Prompts for install directory
8. ✅ Completes installation

