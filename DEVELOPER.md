# Developer's Guide to EmuLibrary

This guide provides a quick overview of the project structure and key concepts for developers who want to understand, modify, or extend the EmuLibrary plugin.

## Project Architecture

EmuLibrary is designed around the concept of ROM types, which are different ways of handling game files:

1. **Core Components**:
   - `EmuLibrary.cs`: Plugin entry point
   - `IEmuLibrary.cs`: Core interface
   - `Settings/`: Configuration system

2. **ROM Type System**:
   - Each type (SingleFile, MultiFile, GogInstaller, PcInstaller, etc.) follows a similar pattern
   - Each implements:
     - `XXXGameInfo.cs`: Metadata storage
     - `XXXScanner.cs`: File detection
     - `XXXInstallController.cs`: Installation process
     - `XXXUninstallController.cs`: Uninstallation process

3. **Utility Systems**:
   - `Util/FileCopier/`: File operations
   - `PlayniteCommon/`: Shared utilities

## Key Workflows

### Game Detection Workflow

1. User adds a mapping in settings
2. `EmuLibrary.GetGames()` is called by Playnite during library refresh
3. `RomTypeScanner.GetGames()` is called for each ROM type
4. Scanner detects valid files and creates `GameMetadata` objects
5. Playnite adds the games to the library

### Installation Workflow

1. User clicks "Play" on an uninstalled game
2. `EmuLibrary.GetInstallActions()` returns the appropriate controller
3. `XXXInstallController.Install()` is called
4. Files are copied or installers are executed
5. Game status is updated in Playnite

### Uninstallation Workflow

1. User right-clicks and selects "Uninstall"
2. `EmuLibrary.GetUninstallActions()` returns the appropriate controller
3. `XXXUninstallController.Uninstall()` is called
4. Files are removed or uninstallers are executed
5. Game status is updated in Playnite

## Adding a New ROM Type

1. Create a new directory under `RomTypes/`
2. Create your implementation classes:
   - `XXXGameInfo.cs`: Extending `ELGameInfo`
   - `XXXScanner.cs`: Extending `RomTypeScanner`
   - `XXXInstallController.cs`: Extending `BaseInstallController`
   - `XXXUninstallController.cs`: Extending `BaseUninstallController`
3. Add your type to the `RomType` enum in `RomType.cs`
4. Add a `RomTypeInfoAttribute` with your GameInfo and Scanner types

## Building in Visual Studio

When building in Visual Studio:

1. Make sure NuGet packages are restored (right-click solution, select "Restore NuGet Packages")
2. Build the solution in Debug or Release configuration

## Testing

When testing the plugin:

1. Build the project
2. Copy the output DLL and dependencies to:
   - `%AppData%\Playnite\Extensions\EmuLibrary\` (for installed Playnite)
   - Or the appropriate extensions directory if running Playnite portable
3. Use DEBUG compilation for detailed logging
4. Check Playnite logs for errors (`F12` → `Open application directory` → check log files)

### Troubleshooting Dependencies

The project has specific version requirements to match Playnite:
- LibHac: Version 0.7.0 (needed for Switch game support)
- protobuf-net: Version 2.4.0 (must match Playnite's version exactly)
- Newtonsoft.Json: Version 10.0.3 (must match Playnite's version)

If you encounter dependency issues like:
```
Could not load file or assembly 'LibHac, Version=0.7.0.0, Culture=neutral, PublicKeyToken=null'
Could not load file or assembly 'protobuf-net, Version=2.4.0.0, Culture=neutral, PublicKeyToken=257b51d87d2e4d67'
```

Try these solutions:

1. **Run the dependency download script**:
   ```
   PowerShell -File download-dependencies.ps1
   ```
   This script will download and place the required DLLs in the output directory.

2. **Manual NuGet Package Restore**:
   - Right-click on the solution in Solution Explorer
   - Select "Restore NuGet Packages"
   - Rebuild the solution

3. **Check Package References**:
   Ensure that the package references in EmuLibrary.csproj have the correct settings:
   - LibHac and protobuf-net should have:
     ```xml
     <CopyLocal>true</CopyLocal>
     <Private>true</Private>
     ```

4. **Verify Binding Redirects**:
   Check app.config for proper binding redirects:
   ```xml
   <!-- Ensure exact version match for protobuf-net -->
   <dependentAssembly>
     <assemblyIdentity name="protobuf-net" publicKeyToken="257b51d87d2e4d67" culture="neutral" />
     <bindingRedirect oldVersion="0.0.0.0-2.4.0.0" newVersion="2.4.0.0" />
   </dependentAssembly>
   
   <!-- Ensure LibHac binding redirect is present -->
   <dependentAssembly>
     <assemblyIdentity name="LibHac" publicKeyToken="null" culture="neutral" />
     <bindingRedirect oldVersion="0.0.0.0-0.7.0.0" newVersion="0.7.0.0" />
   </dependentAssembly>
   ```

5. **Manual DLL Copy**:
   If all else fails, manually copy the DLLs:
   - Find LibHac.dll in your NuGet cache (usually in `%USERPROFILE%\.nuget\packages\libhac\0.7.0\lib\net46\`)
   - Find protobuf-net.dll in your NuGet cache (usually in `%USERPROFILE%\.nuget\packages\protobuf-net\2.4.6\lib\net40\`)
   - Copy these files to your project's output directory (bin\Debug or bin\Release)

## Building on Linux

If you're developing on a Linux machine, you can still build and syntax-check the project using Mono:

1. Install the required packages:
   ```bash
   # For Arch Linux
   sudo pacman -S mono mono-msbuild mono-addins nuget
   
   # For Ubuntu/Debian
   sudo apt-get install mono-complete nuget msbuild
   ```

2. Use the provided build scripts:
   - `./build.sh` - Full build using Mono's MSBuild
   - `./check-syntax.sh` - Fast syntax validation of C# files without full build

3. For VS Code integration, open the provided workspace file:
   ```bash
   code EmuLibrary.code-workspace
   ```
   
   Install the C# extension to get intellisense and syntax highlighting.

Note: The Windows-specific post-build steps will be skipped when building on Linux.

## Building on Linux

If you're developing on a Linux machine, you can still build and syntax-check the project using Mono:

1. Install the required packages:
   ```bash
   # For Arch Linux
   sudo pacman -S mono mono-msbuild mono-addins nuget
   
   # For Ubuntu/Debian
   sudo apt-get install mono-complete nuget msbuild
   ```

2. Use the provided build scripts:
   - `./build.sh` - Full build using Mono's MSBuild
   - `./check-syntax.sh` - Fast syntax validation of C# files without full build

3. For VS Code integration, open the provided workspace file:
   ```bash
   code EmuLibrary.code-workspace
   ```
   
   Install the C# extension to get intellisense and syntax highlighting.

Note: The Windows-specific post-build steps will be skipped when building on Linux.

## UI Development Guidelines

### XAML Styling

- Ensure all UI elements have appropriate contrast for both light and dark themes
- For text elements, use the `Foreground` property with a color value of `#FF333333` (dark grey)
- For buttons and interactive controls, explicitly set both `Foreground` and `Background` properties

Example:
```xml
<Style TargetType="Button">
    <Setter Property="Foreground" Value="#FF333333"/>
    <Setter Property="Background" Value="#FFE0E0E0"/>
</Style>
```

## Migration Guidelines

When changing ROM types or their implementations, be sure to:

1. Keep the original enum values in `RomType.cs`
2. Create migration helpers for converting between types
3. Handle potential null or missing fields in migration code
4. Add explicit error handling for unknown types

Example from the GOG to PC Installer migration:
```csharp
try {
    // Code to migrate settings
} catch (Exception ex) {
    logger.Warn($"Error during migration: {ex.Message}");
    // Provide reasonable defaults or fallbacks
}
```

## Directory Documentation

Each directory contains a `DIRECTORY.md` file with specific information about the files and their purposes. Refer to these files for detailed documentation about each component.