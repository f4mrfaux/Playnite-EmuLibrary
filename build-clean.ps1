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
# Copy-Item "$outputDir/ZstdSharp.dll" "$extDir/"
# Copy-Item "$outputDir/ini-parser.dll" "$extDir/"
# Copy-Item "$outputDir/LibHac.dll" "$extDir/"

# Create the extension package (.pext is just a .zip file)
$version = (Get-Content "$outputDir/extension.yaml" | Select-String 'Version: (.+)').Matches.Groups[1].Value
$pluginId = (Get-Content "$outputDir/extension.yaml" | Select-String 'Id: (.+)').Matches.Groups[1].Value
$pextPath = "./EmuLibrary_${pluginId}_${version}.pext"

# Pack as ZIP with .pext extension
Compress-Archive -Path "$extDir/*" -DestinationPath $pextPath -Force

# Clean up
Remove-Item -Recurse -Force $packDir

Write-Host "Package created at $pextPath"