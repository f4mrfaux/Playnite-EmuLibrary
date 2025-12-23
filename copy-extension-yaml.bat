@echo off
echo ---------------------------------------------
echo ISOlator Extension.yaml Update Tool
echo ---------------------------------------------
echo.

echo Checking if extension.yaml exists in source folder...
if not exist "EmuLibrary\extension.yaml" (
  echo [ERROR] EmuLibrary\extension.yaml not found!
  goto :end
) else (
  echo [OK] Found EmuLibrary\extension.yaml
)

echo.
echo Checking if build folder exists...
if not exist "EmuLibrary\bin\Debug\net462\" (
  echo [ERROR] Build folder not found. Please build the project first.
  goto :end
) else (
  echo [OK] Build folder found
)

echo.
echo Copying extension.yaml to build folder...
copy /Y "EmuLibrary\extension.yaml" "EmuLibrary\bin\Debug\net462\"
if %ERRORLEVEL% EQU 0 (
  echo [OK] extension.yaml copied successfully
) else (
  echo [ERROR] Failed to copy extension.yaml
)

echo.
echo Checking extension.yaml in build folder...
if exist "EmuLibrary\bin\Debug\net462\extension.yaml" (
  echo [OK] extension.yaml exists in build folder
  echo Contents:
  type "EmuLibrary\bin\Debug\net462\extension.yaml"
) else (
  echo [ERROR] extension.yaml not found in build folder
)

:end
echo.
echo ---------------------------------------------
echo Done
echo ---------------------------------------------

pause