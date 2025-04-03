# Script to download missing dependencies and place them in the extension directory

# Create temp directory
$tempDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), [System.Guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path $tempDir | Out-Null

# Determine where to place the DLLs
$extensionDir = ".\EmuLibrary\bin\Release\net462"
if (-not (Test-Path $extensionDir)) {
    $extensionDir = ".\bin\Release\net462"
    if (-not (Test-Path $extensionDir)) {
        $extensionDir = "."
    }
}

try {
    # Download and install LibHac.dll
    try {
        # URL for LibHac 0.7.0 NuGet package
        $nugetUrl = "https://www.nuget.org/api/v2/package/LibHac/0.7.0"
        $packagePath = Join-Path $tempDir "LibHac.0.7.0.nupkg"
        
        # Download the NuGet package
        Write-Host "Downloading LibHac 0.7.0 NuGet package..."
        Invoke-WebRequest -Uri $nugetUrl -OutFile $packagePath
        
        # Extract the package (it's a ZIP file)
        Write-Host "Extracting package..."
        Expand-Archive -Path $packagePath -DestinationPath $tempDir
        
        # Find the LibHac.dll for .NET Framework 4.6
        $libHacPath = Join-Path $tempDir "lib\net46\LibHac.dll"
        
        if (Test-Path $libHacPath) {
            # Copy the DLL to the extension directory
            Write-Host "Copying LibHac.dll to $extensionDir..."
            Copy-Item $libHacPath $extensionDir
            Write-Host "LibHac.dll has been successfully downloaded and placed in $extensionDir" -ForegroundColor Green
        } else {
            Write-Host "ERROR: Could not find LibHac.dll in the NuGet package." -ForegroundColor Red
        }
    } catch {
        Write-Host "ERROR: Failed to download LibHac.dll: $_" -ForegroundColor Red
    }
    
    # Download and install protobuf-net.dll
    try {
        # URL for protobuf-net 2.4.6 NuGet package
        $nugetUrl = "https://www.nuget.org/api/v2/package/protobuf-net/2.4.6"
        $packagePath = Join-Path $tempDir "protobuf-net.2.4.6.nupkg"
        
        # Download the NuGet package
        Write-Host "Downloading protobuf-net 2.4.6 NuGet package..."
        Invoke-WebRequest -Uri $nugetUrl -OutFile $packagePath
        
        # Extract the package (it's a ZIP file)
        Write-Host "Extracting package..."
        Expand-Archive -Path $packagePath -DestinationPath (Join-Path $tempDir "protobuf")
        
        # Find the protobuf-net.dll for .NET Framework 4.6
        $protobufPath = Join-Path $tempDir "protobuf\lib\net40\protobuf-net.dll"
        
        if (Test-Path $protobufPath) {
            # Copy the DLL to the extension directory
            Write-Host "Copying protobuf-net.dll to $extensionDir..."
            Copy-Item $protobufPath $extensionDir
            Write-Host "protobuf-net.dll has been successfully downloaded and placed in $extensionDir" -ForegroundColor Green
        } else {
            Write-Host "ERROR: Could not find protobuf-net.dll in the NuGet package." -ForegroundColor Red
        }
    } catch {
        Write-Host "ERROR: Failed to download protobuf-net.dll: $_" -ForegroundColor Red
    }
} finally {
    # Clean up
    Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue
}