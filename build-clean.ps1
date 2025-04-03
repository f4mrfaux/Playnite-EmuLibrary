# Clean build script for Playnite extensions

# Build the project
dotnet build -c Release

# Directory setup
$outputDir = "./bin/Release/net462"
$packDir = "./pack-temp"
$extDir = "$packDir/extension"

# Create clean directories
if (Test-Path $packDir) { Remove-Item -Recurse -Force $packDir }
New-Item -ItemType Directory -Path $extDir | Out-Null

# Copy only the necessary files
Copy-Item "$outputDir/EmuLibrary.dll" "$extDir/"
Copy-Item "$outputDir/app.config" "$extDir/"
Copy-Item "$outputDir/extension.yaml" "$extDir/"
Copy-Item "$outputDir/icon.png" "$extDir/"

# For required third-party dependencies that are not provided by Playnite
Copy-Item "$outputDir/ZstdSharp.dll" "$extDir/" -ErrorAction SilentlyContinue
Copy-Item "$outputDir/INIFileParser.dll" "$extDir/" -ErrorAction SilentlyContinue
Copy-Item "$outputDir/LibHac.dll" "$extDir/" -ErrorAction SilentlyContinue
Copy-Item "$outputDir/protobuf-net.dll" "$extDir/" -ErrorAction SilentlyContinue

# Ensure LibHac.dll is included - it should now be copied by default due to 
# CopyLocalLockFileAssemblies=true in the csproj file
if (-not (Test-Path "$extDir/LibHac.dll")) {
    Write-Host "LibHac.dll was not found in the output directory. Trying to find it..." -ForegroundColor Yellow
    
    # Try to find it in the NuGet packages
    $nugetDir = "$env:USERPROFILE/.nuget/packages/libhac/0.7.0/lib/net46"
    if (Test-Path "$nugetDir/LibHac.dll") {
        Write-Host "Found LibHac.dll in NuGet cache, copying to extension directory..."
        Copy-Item "$nugetDir/LibHac.dll" "$extDir/"
    } else {
        Write-Host "LibHac.dll not found. Running automatic download script..." -ForegroundColor Yellow
        # Run the download script
        & "$PSScriptRoot/download-dependencies.ps1"
        # Copy from wherever download-libhac.ps1 placed it
        if (Test-Path "./LibHac.dll") {
            Copy-Item "./LibHac.dll" "$extDir/"
            Write-Host "LibHac.dll has been automatically downloaded and included in the package." -ForegroundColor Green
        } else {
            Write-Host "ERROR: Could not include LibHac.dll. The extension will not work properly." -ForegroundColor Red
            Write-Host "Please run download-libhac.ps1 manually before packaging." -ForegroundColor Red
        }
    }
} else {
    Write-Host "LibHac.dll found and included in the package." -ForegroundColor Green
}

# Create the extension package (.pext is just a .zip file)
$version = (Get-Content "$outputDir/extension.yaml" | Select-String 'Version: (.+)').Matches.Groups[1].Value
$pluginId = (Get-Content "$outputDir/extension.yaml" | Select-String 'Id: (.+)').Matches.Groups[1].Value
$pextPath = "./EmuLibrary_${pluginId}_${version}.pext"

# Pack as ZIP with .pext extension
Compress-Archive -Path "$extDir/*" -DestinationPath $pextPath -Force

# Clean up
Remove-Item -Recurse -Force $packDir

Write-Host "Package created at $pextPath"