@echo off
echo ==============================================
echo ISOlator File Finder
echo ==============================================
echo.

echo Searching for extension.yaml...
dir /s /b extension.yaml
echo.

echo Searching for icon.png...
dir /s /b icon.png
echo.

echo Checking EmuLibrary folder structure...
if exist "EmuLibrary" (
  echo EmuLibrary folder exists, checking contents:
  dir /b "EmuLibrary"
  
  if exist "EmuLibrary\extension.yaml" (
    echo [OK] Found extension.yaml in EmuLibrary folder
  )
  
  if exist "EmuLibrary\icon.png" (
    echo [OK] Found icon.png in EmuLibrary folder
  )
) else (
  echo [ERROR] EmuLibrary folder not found
)

echo.
echo ==============================================
pause