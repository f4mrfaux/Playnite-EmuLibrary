# Build Instructions for EmuLibrary PC Manager

## Prerequisites

- Visual Studio 2019 or newer (or Visual Studio Code with C# extension)
- .NET Framework 4.6.2 SDK
- PowerShell or Bash for running build scripts

## Building the Project

### Using Visual Studio

1. Open `EmuLibrary.sln` in Visual Studio
2. Set the build configuration to `Release`
3. Build the solution (Build > Build Solution or F6)

### Using Command Line

**On Windows:**
```
dotnet build -c Release
```

**On Linux/Mac:**
```
dotnet build -c Release
```

## Packaging for Playnite

After building the project, use the clean build scripts to create a proper package:

### Using PowerShell (Windows)

```powershell
.\build-clean.ps1
```

### Using Bash (Linux/Mac)

```bash
./build-clean.sh
```

## Important Notes

### Dependencies

The build system is configured to automatically include all required dependencies:

1. LibHac.dll - Required for Yuzu ROM scanning
2. INIFileParser.dll - Used for configuration parsing
3. ZstdSharp.dll - Used for archive handling

These dependencies are configured in EmuLibrary.csproj with proper settings:

```xml
<PackageReference Include="LibHac" Version="0.7.0">
  <!-- Allow LibHac.dll to be copied to output directory -->
  <PrivateAssets>analyzers</PrivateAssets>
</PackageReference>
```

The project is configured with `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` to ensure all required dependencies are copied to the output directory.

### Automatic Dependency Handling

The build-clean.ps1 script includes logic to:
1. Check if all required dependencies are present
2. Automatically download any missing dependencies
3. Include them in the final package

### Testing the Extension

1. Install the extension in Playnite using "Add plugin" and selecting the `.pext` file
2. Check the Playnite logs for any errors during loading
3. If the plugin fails to load, try adding the missing DLLs manually to:
   ```
   %AppData%\Playnite\Extensions\{Extension-ID}\
   ```

## Known Issues and Solutions

### Missing Dependencies

**Issue:** Playnite cannot load required dependencies like LibHac.dll or protobuf-net.dll.

**Solution:**
1. Run the included script to automatically download and install all dependencies:
   ```powershell
   .\download-dependencies.ps1
   ```
2. Alternatively, manually download these DLLs from NuGet and place them in the extension directory:
   - LibHac.dll (version 0.7.0)
   - protobuf-net.dll (version 2.4.6)
3. Restart Playnite

### Newtonsoft.Json Version Conflicts

The extension uses Newtonsoft.Json 10.0.3, but Playnite may use a different version. The `app.config` file contains binding redirects to handle this:

```xml
<dependentAssembly>
  <assemblyIdentity name="Newtonsoft.Json" publicKeyToken="30ad4fe6b2a6aeed" culture="neutral" />
  <bindingRedirect oldVersion="0.0.0.0-13.0.0.0" newVersion="10.0.0.0" />
</dependentAssembly>
```

If you encounter issues, make sure this configuration is properly included in the extension.