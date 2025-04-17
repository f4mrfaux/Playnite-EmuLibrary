# TODO Features

## Multi-level Archive Support with Local Asset Import

### Overview
Add support for multi-layered archives that contain ISOs which in turn contain installer executables. All installation assets will be imported locally before any extraction/mounting operations to avoid network reliability issues.

### Requirements

1. **Local Asset Import Mechanism**
   - Create a mechanism to import all installation assets (.exe, .iso, archives) to local temp storage
   - Perform all operations (extraction, mounting, execution) from local temp storage
   - Avoid any direct network operations for installer execution, ISO mounting, or archive extraction
   - Implement progress reporting for large file imports
   - Handle network disconnection gracefully during initial import phase

2. **Archive Handling**
   - Support common archive formats (.zip, .rar, .7z)
   - Handle multi-part archives (especially split RAR files)
   - Only extract archives from local temp storage

3. **ISO Mounting from Archives**
   - After archive extraction, detect and mount contained ISO files
   - Support common ISO formats (ISO, BIN/CUE, MDF/MDS, etc.)
   - Only mount ISOs from local temp storage, never directly from network

4. **Installer Execution**
   - Detect setup executables within mounted ISO
   - Only execute installers from local temp, never directly from network
   - Track installation progress
   - Clean up temporary files after installation

5. **UI/UX Considerations**
   - Show appropriate progress indicators for each stage
   - Allow cancellation at any point in the process
   - Provide clear error messages for troubleshooting

### Implementation Strategy

1. Create new RomType: `ArchiveInstaller`
2. Implement asset import system 
   - Create ImportManager class to handle copying files to local temp
   - Implement progress reporting for large file transfers
   - Add validation of imported assets
3. Implement detection for archive files containing ISOs
4. Add multi-step installation controller with stages:
   - Import assets to local temp storage (new step)
   - Archive extraction (from local temp)
   - ISO mounting (from local temp)
   - Installer execution (from local temp)
   - Cleanup
5. Add appropriate platform detection and compatibility checks
6. Implement proper resource management for each step

### Technical Considerations

- Use async file copy with proper buffering for efficient imports
- Need proper error handling for network disconnection during initial import
- Implement size checking before import to ensure sufficient temp space
- Need 7-Zip or similar command-line tool for reliable archive extraction
- Consider using PowerShell or WMI for ISO mounting on Windows
- Need proper error handling and resource cleanup between stages
- May require elevated permissions for certain operations

### Future Expansion
- Consider supporting additional archive nesting levels and formats as needed
- Implement downloaded asset caching to speed up reinstallation of previous imports
- Add asset verification via checksums to ensure integrity