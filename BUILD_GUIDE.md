# ISOlator Build Guide

This document provides instructions for building the ISOlator Playnite extension after the project rename from EmuLibrary to ISOlator.

## Prerequisites

- Visual Studio 2019 or later
- .NET Framework 4.6.2 SDK
- Windows OS (for final build and packaging)

## First-Time Setup After Rename

1. Before opening the solution, run the `clean.bat` script to clean any lingering build artifacts:
   ```
   clean.bat
   ```

2. Open the new solution file:
   ```
   ISOlator.sln
   ```

3. If you previously had the project open in Visual Studio, you may need to close and reopen Visual Studio for the changes to take effect.

## Building the Extension

### Using Visual Studio

1. Open `ISOlator.sln` in Visual Studio
2. Select the desired configuration (Debug or Release)
3. Build the solution (F6 or Ctrl+Shift+B)

### Using Command Line

```
msbuild ISOlator.sln /p:Configuration=Release
```

## Packaging

The build process automatically packages the extension using the post-build event. The packaged extension will be located in the solution directory with a `.pext` extension.

## Troubleshooting

If you encounter any issues with the build process:

1. **Clean the solution**: Run `clean.bat` and try building again
2. **Check extension.yaml**: Ensure all GUIDs and versions match between:
   - `extension.yaml`
   - `EmuLibrary.cs` (PluginId constant)
   - `AssemblyInfo.cs` (Guid attribute)
3. **Check for remaining old references**: Search for "EmuLibrary" in the codebase to find any places that need to be renamed:
   ```
   findstr /S /I "EmuLibrary" *.*
   ```
   Note: Not all instances should be renamed, as the DLL is still named EmuLibrary.dll.

## Important Notes

- The assembly name and root namespace are still `EmuLibrary` for backward compatibility
- The project file has been renamed to `ISOlator.csproj`
- The solution file has been renamed to `ISOlator.sln`
- The extension name in `extension.yaml` is now "ISOlator"