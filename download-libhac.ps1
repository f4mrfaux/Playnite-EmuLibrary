# Script to download LibHac.dll version 0.7.0 and place it in the extension directory

# Create temp directory
$tempDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), [System.Guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path $tempDir | Out-Null

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
        # Determine where to place the DLL
        $extensionDir = ".\EmuLibrary\bin\Release\net462"
        if (-not (Test-Path $extensionDir)) {
            $extensionDir = ".\bin\Release\net462"
            if (-not (Test-Path $extensionDir)) {
                $extensionDir = "."
            }
        }
        
        # Copy the DLL to the extension directory
        Write-Host "Copying LibHac.dll to $extensionDir..."
        Copy-Item $libHacPath $extensionDir
        Write-Host "LibHac.dll has been successfully downloaded and placed in $extensionDir"
    } else {
        Write-Host "ERROR: Could not find LibHac.dll in the NuGet package." -ForegroundColor Red
    }
} finally {
    # Clean up
    Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue
}