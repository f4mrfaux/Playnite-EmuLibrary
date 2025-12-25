# Archive Support Implementation

## Summary

Archive extraction support has been implemented using 7-Zip CLI for reliable multi-part RAR handling. The implementation supports ZIP, RAR, 7Z, and multi-part archives.

## Implementation Details

### 1. ArchiveExtractor Utility (`EmuLibrary/Util/ArchiveExtractor.cs`)

**Features:**
- Uses 7-Zip CLI (`7z.exe`) for extraction
- Supports ZIP, RAR, 7Z, and 7ZIP formats
- Handles multi-part RAR files automatically
- Detects content type after extraction (ISO vs EXE)
- Proper cancellation support
- Error handling and logging

**Key Methods:**
- `IsArchiveFile(string filePath)` - Checks if file is a supported archive
- `IsMultiPartRar(string filePath)` - Detects multi-part RAR files
- `FindMultiPartRarFiles(string firstPartPath)` - Finds all parts of multi-part RAR
- `ExtractArchiveAsync(...)` - Extracts archive using 7-Zip CLI
- `DetectContentType(string extractedDir)` - Detects ISO/EXE files in extracted content

**7-Zip Detection:**
- Searches common installation paths:
  - `C:\Program Files\7-Zip\7z.exe`
  - `C:\Program Files (x86)\7-Zip\7z.exe`
  - PATH environment variable
- Falls back to `7za.exe` if `7z.exe` not found

### 2. Scanner Updates

#### PCInstallerScanner
- **Updated**: Now detects `.zip`, `.rar`, `.7z`, `.7zip` files in addition to `.exe`
- Archives are treated as potential PC installers

#### ISOInstallerScanner
- **Updated**: Now detects archive files that may contain ISO files
- Archives are scanned and can contain ISO files

### 3. Installation Controller Integration

#### PCInstallerInstallController
**Workflow:**
1. Check if source file is an archive
2. If archive:
   - Extract to temp directory
   - Detect content type (ISO vs EXE)
   - If ISO files found → Error (should use ISOInstaller)
   - If EXE files found → Use first installer EXE
   - Continue with normal installation workflow
3. If not archive → Continue with normal EXE installation

**Content Detection:**
- Searches for common installer names: `setup.exe`, `install.exe`, `autorun.exe`, `game.exe`
- Falls back to first EXE found if no common name matches

#### ISOInstallerInstallController
**Workflow:**
1. Check if source file is an archive
2. If archive:
   - Extract to temp directory
   - Detect ISO files in extracted content
   - Use first ISO file found
   - Continue with normal ISO mounting workflow
3. If not archive → Continue with normal ISO mounting

**Multi-ISO Handling:**
- If multiple ISO files found, uses first one
- Logs warning about multiple ISO files

### 4. Multi-Part RAR Support

**Detection Patterns:**
- `game.part1.rar`, `game.part2.rar`, etc.
- `game.part01.rar`, `game.part02.rar`, etc.
- `game.part001.rar`, `game.part002.rar`, etc.
- `game.rar`, `game.r00`, `game.r01`, etc. (standard multi-part)

**Handling:**
- Automatically finds all parts in the same directory
- 7-Zip CLI automatically handles multi-part extraction
- Only the first part needs to be specified

## Requirements

### 7-Zip Installation
Users must have 7-Zip installed on their system. The extractor will:
1. Search common installation paths
2. Check PATH environment variable
3. Show clear error message if not found

**Download**: https://www.7-zip.org/

## Error Handling

### Archive Extraction Failures
- Clear error messages logged
- User notifications for failures
- Proper cleanup of temp directories

### Missing 7-Zip
- Error message: "7-Zip executable not found. Please install 7-Zip."
- Installation cannot proceed without 7-Zip

### Content Detection Failures
- If archive contains no ISO/EXE files → Error
- If archive contains ISO but processed as PCInstaller → Error with suggestion
- If archive contains both → Uses heuristics (ISO for ISOInstaller, EXE for PCInstaller)

## Cancellation Support

- Full cancellation token support throughout extraction
- Process cleanup on cancellation
- Temp directory cleanup in finally blocks

## Testing Checklist

### PCInstaller with Archives
- [ ] ZIP archive containing EXE
- [ ] RAR archive containing EXE
- [ ] 7Z archive containing EXE
- [ ] Multi-part RAR containing EXE
- [ ] Archive with nested folders
- [ ] Archive with multiple EXE files (should use first installer)

### ISOInstaller with Archives
- [ ] ZIP archive containing ISO
- [ ] RAR archive containing ISO
- [ ] 7Z archive containing ISO
- [ ] Multi-part RAR containing ISO
- [ ] Archive containing multiple ISO files

### Edge Cases
- [ ] Archive containing both ISO and EXE (should route correctly)
- [ ] Password-protected archive (not yet supported - will fail gracefully)
- [ ] Corrupted archive (should show clear error)
- [ ] Very large archives (>10GB)
- [ ] Network path archives (slow extraction)

## Known Limitations

1. **Password-Protected Archives**: Not yet supported. Will fail with 7-Zip error.
   - Future enhancement: Prompt user for password

2. **Nested Archives**: Not automatically handled.
   - If archive contains another archive, user must extract manually first

3. **Progress Reporting**: Limited during extraction.
   - 7-Zip output is captured but not parsed for progress

## Future Enhancements

1. **Password Support**: Prompt user for password when needed
2. **Progress Reporting**: Parse 7-Zip output for extraction progress
3. **Nested Archive Handling**: Automatically extract nested archives
4. **Archive Validation**: Check archive integrity before extraction
5. **Resume Support**: Resume interrupted extractions

## Code Quality

- ✅ Proper error handling
- ✅ Resource cleanup (temp directories)
- ✅ Cancellation support
- ✅ Comprehensive logging
- ✅ Clear error messages
- ✅ Follows existing code patterns

## Files Modified

1. `EmuLibrary/Util/ArchiveExtractor.cs` - **NEW** - Archive extraction utility
2. `EmuLibrary/RomTypes/PCInstaller/PCInstallerScanner.cs` - Added archive extensions
3. `EmuLibrary/RomTypes/ISOInstaller/ISOInstallerScanner.cs` - Added archive extensions
4. `EmuLibrary/RomTypes/PCInstaller/PCInstallerInstallController.cs` - Integrated extraction
5. `EmuLibrary/RomTypes/ISOInstaller/ISOInstallerInstallController.cs` - Integrated extraction

## Dependencies

- **7-Zip**: Required external dependency (CLI tool)
- **No NuGet packages**: Uses 7-Zip CLI instead of managed libraries for better multi-part RAR support

