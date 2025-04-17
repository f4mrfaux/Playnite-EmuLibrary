# TODO Features

## Multi-level Archive Support

### Overview
Add support for multi-layered archives that contain ISOs which in turn contain installer executables. This will allow handling complex archival formats commonly found in game preservation.

### Requirements

1. **Archive Handling**
   - Support common archive formats (.zip, .rar, .7z)
   - Handle multi-part archives (especially split RAR files)
   - Extract archives to temporary location

2. **ISO Mounting from Archives**
   - After archive extraction, detect and mount contained ISO files
   - Support common ISO formats (ISO, BIN/CUE, MDF/MDS, etc.)
   - Use reliable mounting mechanism across platforms

3. **Installer Execution**
   - Detect setup executables within mounted ISO
   - Execute installer with appropriate parameters
   - Track installation progress
   - Clean up temporary files after installation

4. **UI/UX Considerations**
   - Show appropriate progress indicators for each stage
   - Allow cancellation at any point in the process
   - Provide clear error messages for troubleshooting

### Implementation Strategy

1. Create new RomType: `ArchiveInstaller`
2. Implement detection for archive files containing ISOs
3. Add multi-step installation controller with stages:
   - Archive extraction
   - ISO mounting
   - Installer execution
   - Cleanup
4. Add appropriate platform detection and compatibility checks
5. Implement proper resource management for each step

### Technical Considerations

- Need 7-Zip or similar command-line tool for reliable archive extraction
- Consider using PowerShell or WMI for ISO mounting on Windows
- Need proper error handling and resource cleanup between stages
- May require elevated permissions for certain operations

### Future Expansion
Consider supporting additional archive nesting levels and formats as needed.