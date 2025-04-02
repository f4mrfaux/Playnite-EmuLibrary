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

### Missing Dependencies

If you're encountering issues with missing dependencies like `LibHac.dll`, make sure:

1. The NuGet package is installed properly
2. The DLL is copied to the output directory
3. The package reference in EmuLibrary.csproj has the correct configuration:

```xml
<PackageReference Include="LibHac" Version="0.7.0">
  <IncludeAssets>compile; build</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

### Manual File Inclusion

If automatic dependency inclusion is not working, you may need to manually copy the required DLLs to the extension package:

1. Build the project
2. Find the required DLLs in your NuGet packages folder:
   - LibHac.dll
   - INIFileParser.dll 
   - ZstdSharp.dll
3. Copy these DLLs to the extension directory alongside EmuLibrary.dll

### Testing the Extension

1. Install the extension in Playnite using "Add plugin" and selecting the `.pext` file
2. Check the Playnite logs for any errors during loading
3. If the plugin fails to load, try adding the missing DLLs manually to:
   ```
   %AppData%\Playnite\Extensions\{Extension-ID}\
   ```

## Known Issues and Solutions

### LibHac.dll Missing

**Issue:** Playnite cannot load the LibHac.dll dependency.

**Solution:**
1. Download LibHac.dll version 0.7.0 from NuGet
2. Place it in the extension directory
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