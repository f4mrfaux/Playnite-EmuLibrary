# PC Compatibility Backend Review

## Executive Summary

This document reviews the PC compatibility features (PCInstaller and ISOInstaller) for production readiness. The core functionality is solid, but **archive extraction support (RAR, ZIP, 7ZIP) is missing** and needs to be implemented to achieve the "Steam store at home" workflow.

## Current Implementation Status

### ✅ What Works Well

#### PCInstaller (EXE Files)
- **Scanner**: Properly detects `.exe` files recursively in source directories
- **Installation Workflow**:
  1. ✅ Creates temp directory
  2. ✅ Copies EXE to temp
  3. ✅ Runs installer with user interaction
  4. ✅ Prompts user for installation directory
  5. ✅ Auto-detects primary executable
  6. ✅ Updates Playnite library with play action
- **GOG Integration**: Detects GOG installers and extracts game IDs
- **Platform Detection**: Properly categorizes games by store (GOG, Steam, etc.)
- **Error Handling**: Good exception handling and logging
- **Cancellation Support**: Proper cancellation token handling

#### ISOInstaller (ISO Files)
- **Scanner**: Detects `.iso` files, filters out extracted content
- **Installation Workflow**:
  1. ✅ Mounts ISO using PowerShell `Mount-DiskImage`
  2. ✅ Finds installer (setup.exe, install.exe, etc.)
  3. ✅ Runs installer
  4. ✅ Unmounts ISO after installation
  5. ✅ Auto-detects primary executable
  6. ✅ Updates Playnite library
- **Multi-disc Support**: Handles multiple ISO files per game
- **Extracted Content Detection**: Smart filtering to avoid duplicate entries
- **Error Handling**: Proper cleanup on failure (unmounts ISO)

### ❌ Critical Missing Features

#### Archive Extraction Support
**Status**: **NOT IMPLEMENTED**

The following archive formats are **not supported**:
- ❌ ZIP files
- ❌ RAR files (single and multi-part)
- ❌ 7ZIP files
- ❌ Other compressed formats

**Impact**: Users cannot install games that come in compressed archives. The workflow stops at the archive file.

**Required Workflow**:
```
Archive File → Extract to Temp → Detect Content Type → Handle as ISO or EXE
```

## Detailed Workflow Analysis

### Current Workflows

#### PCInstaller Workflow (EXE)
```
1. Scan source directory for .exe files
2. User clicks "Install"
3. Copy .exe to temp directory
4. Run installer (user completes installation)
5. Prompt user for installation directory
6. Scan install directory for .exe files
7. Auto-detect primary executable
8. Update Playnite library with play action
9. Clean up temp directory
```

**Status**: ✅ **Production Ready** (except archive support)

#### ISOInstaller Workflow (ISO)
```
1. Scan source directory for .iso files
2. User clicks "Install"
3. Mount ISO using PowerShell
4. Find installer in mounted disc
5. Run installer (user completes installation)
6. Auto-detect primary executable in install directory
7. Update Playnite library with play action
8. Unmount ISO
```

**Status**: ✅ **Production Ready** (except archive support)

### Missing Workflow: Archive Handling

#### Required Archive Workflow
```
1. Scan source directory for .zip, .rar, .7z, .part1.rar, etc.
2. User clicks "Install"
3. Extract archive to temp directory
   - Handle multi-part RAR (part1.rar, part2.rar, etc.)
   - Handle password-protected archives (prompt user)
4. Detect extracted content:
   - If contains .iso file → Handle as ISOInstaller
   - If contains .exe file → Handle as PCInstaller
   - If contains both → Prompt user or use heuristics
5. Continue with appropriate installer workflow
6. Clean up temp extraction directory
```

**Status**: ❌ **NOT IMPLEMENTED**

## Code Review Findings

### PCInstallerInstallController.cs

**Strengths**:
- Good async/await usage
- Proper cancellation token handling
- Clean temp directory management
- Good error messages

**Issues**:
1. **No archive detection**: Only handles `.exe` files directly
2. **No extraction logic**: No code to extract archives before installation
3. **Temp directory cleanup**: Could fail silently (logged as warning only)

**Recommendations**:
- Add archive detection before file copy
- Implement extraction step
- Improve temp cleanup error handling

### ISOInstallerInstallController.cs

**Strengths**:
- Proper ISO mounting/unmounting
- Good installer detection logic
- Proper cleanup in finally block
- Handles multi-disc scenarios

**Issues**:
1. **No archive detection**: Only handles `.iso` files directly
2. **Mount failure handling**: Could be more user-friendly
3. **No extraction logic**: Cannot extract ISO from archive first

**Recommendations**:
- Add archive detection before mounting
- Extract archives that contain ISO files
- Improve error messages for mount failures

### PCInstallerScanner.cs

**Strengths**:
- Efficient file enumeration
- Good GOG detection logic
- Proper batch processing
- Good error handling

**Issues**:
1. **Only scans for .exe**: No archive file detection
   ```csharp
   var installerExtensions = new List<string> { "exe" };
   ```
2. **No archive handling**: Archives are completely ignored

**Recommendations**:
- Add archive extensions to scanner
- Detect archives and mark them for extraction
- Handle multi-part RAR files

### ISOInstallerScanner.cs

**Strengths**:
- Smart extracted content filtering
- Good multi-ISO handling
- Efficient directory scanning

**Issues**:
1. **Only scans for .iso**: No archive file detection
   ```csharp
   var discExtensions = new List<string> { "iso", "ISO" };
   ```
2. **No archive handling**: Archives containing ISO files are ignored

**Recommendations**:
- Add archive extensions to scanner
- Extract and inspect archives to find ISO files
- Handle nested archives

## Implementation Plan

### Phase 1: Archive Detection (High Priority)

**Tasks**:
1. Add archive extensions to scanners:
   - ZIP: `.zip`
   - RAR: `.rar`, `.part1.rar`, `.part01.rar`, etc.
   - 7ZIP: `.7z`, `.7zip`
   - TAR: `.tar`, `.tar.gz`, `.tar.bz2`

2. Update `PCInstallerScanner.cs`:
   ```csharp
   var installerExtensions = new List<string> { "exe", "zip", "rar", "7z" };
   ```

3. Update `ISOInstallerScanner.cs`:
   - Detect archives that may contain ISO files
   - Mark for extraction and inspection

### Phase 2: Archive Extraction (High Priority)

**Tasks**:
1. Create `ArchiveExtractor` utility class:
   - Use `System.IO.Compression` for ZIP (built-in)
   - Use `SharpCompress` or `7-Zip` library for RAR/7ZIP
   - Handle multi-part RAR files
   - Support password-protected archives

2. Integration points:
   - `PCInstallerInstallController.Install()`: Extract before copying
   - `ISOInstallerInstallController.Install()`: Extract before mounting

3. Extraction workflow:
   ```
   Archive → Temp Extract Dir → Detect Content → Continue Workflow
   ```

### Phase 3: Content Detection (Medium Priority)

**Tasks**:
1. After extraction, detect content type:
   - ISO files → Route to ISOInstaller workflow
   - EXE files → Route to PCInstaller workflow
   - Both → Use heuristics or prompt user

2. Handle edge cases:
   - Archives containing multiple installers
   - Archives containing both ISO and EXE
   - Nested archives

### Phase 4: Error Handling & UX (Medium Priority)

**Tasks**:
1. Password prompt for protected archives
2. Progress reporting during extraction
3. Better error messages
4. Cleanup on cancellation

## Dependencies Required

### For Archive Extraction

1. **ZIP Support**: Built-in (`System.IO.Compression.ZipFile`)
   - ✅ No additional dependency needed

2. **RAR Support**: Need external library
   - Option 1: `SharpCompress` (MIT License, .NET Standard 2.0)
   - Option 2: `7-Zip` via command-line (requires 7z.exe)
   - **Recommendation**: `SharpCompress` for cross-platform support

3. **7ZIP Support**: Same as RAR
   - `SharpCompress` supports 7Z format
   - Or use 7-Zip command-line

### Recommended Library

**SharpCompress** (v0.37.0+)
- Supports: ZIP, RAR, 7Z, TAR, GZIP, BZIP2
- .NET Framework 4.6.2 compatible
- MIT License
- NuGet: `SharpCompress`

## Testing Checklist

### PCInstaller
- [x] EXE file installation works
- [ ] ZIP archive containing EXE
- [ ] RAR archive containing EXE
- [ ] 7ZIP archive containing EXE
- [ ] Multi-part RAR containing EXE
- [ ] Password-protected archive
- [ ] Archive with nested folders

### ISOInstaller
- [x] ISO file mounting and installation works
- [ ] ZIP archive containing ISO
- [ ] RAR archive containing ISO
- [ ] 7ZIP archive containing ISO
- [ ] Multi-part RAR containing ISO
- [ ] Archive containing multiple ISO files (multi-disc)

### Edge Cases
- [ ] Archive containing both ISO and EXE
- [ ] Nested archives (archive in archive)
- [ ] Corrupted archives
- [ ] Very large archives (>10GB)
- [ ] Network path archives (slow extraction)

## Production Readiness Assessment

### Current Status: **75% Ready**

**Ready for Production**:
- ✅ EXE file installation
- ✅ ISO file installation
- ✅ GOG detection and metadata
- ✅ Error handling and logging
- ✅ Cancellation support

**Not Ready for Production**:
- ❌ Archive extraction (critical)
- ❌ Multi-part RAR support
- ❌ Password-protected archives
- ⚠️ Better progress reporting needed
- ⚠️ Improved error messages for mount failures

## Recommendations

### Immediate Actions (Before Production)

1. **Implement archive extraction** (Critical)
   - Add SharpCompress library
   - Create ArchiveExtractor utility
   - Integrate into both install controllers

2. **Update scanners** to detect archives
   - Add archive extensions to file detection
   - Handle multi-part RAR naming

3. **Add extraction step** to installation workflow
   - Extract to temp before processing
   - Detect content type after extraction
   - Route to appropriate installer

### Short-term Improvements

1. Password prompt for protected archives
2. Progress reporting during extraction
3. Better error messages
4. Handle nested archives

### Long-term Enhancements

1. Parallel extraction for multi-part archives
2. Resume interrupted extractions
3. Cache extracted content for faster re-installation
4. Support for more archive formats

## Conclusion

The PC compatibility features are **well-implemented** for direct EXE and ISO files, but **archive extraction is a critical missing feature** that prevents the full "Steam store at home" workflow. 

**Priority**: Implement archive extraction before production release.

**Estimated Effort**: 
- Archive extraction: 2-3 days
- Scanner updates: 1 day
- Testing: 2 days
- **Total: ~1 week**

The codebase is clean and well-structured, making it straightforward to add archive support without major refactoring.

