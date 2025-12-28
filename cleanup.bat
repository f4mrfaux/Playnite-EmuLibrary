@echo off
echo Cleaning up GameVault project...

REM Copy essential files if they don't exist at the root
if not exist "extension.yaml" (
  if exist "EmuLibrary\extension.yaml" copy "EmuLibrary\extension.yaml" "extension.yaml"
)
if not exist "icon.png" (
  if exist "EmuLibrary\icon.png" copy "EmuLibrary\icon.png" "icon.png"
)

REM Delete unnecessary files
echo Removing unnecessary files...
if exist "EmuLibrary.sln" del "EmuLibrary.sln"
if exist "backup" rd /s /q "backup"
if exist "build-isolator.bat" del "build-isolator.bat"
if exist "copy-extension-yaml.bat" del "copy-extension-yaml.bat"
if exist "diagnose.bat" del "diagnose.bat"
if exist "clean.bat" del "clean.bat"
if exist "fix-paths.bat" del "fix-paths.bat"
if exist "copy-files.bat" del "copy-files.bat"
if exist "find-files.bat" del "find-files.bat"
if exist "updated-post-build.txt" del "updated-post-build.txt"
if exist "simple-fix.bat" del "simple-fix.bat"
if exist "fix.bat" del "fix.bat"

REM Remove any backup csproj file
if exist "EmuLibrary\EmuLibrary.csproj" del "EmuLibrary\EmuLibrary.csproj"

echo.
echo Cleanup complete!
echo Now rebuild the project with GameVault.sln.
pause