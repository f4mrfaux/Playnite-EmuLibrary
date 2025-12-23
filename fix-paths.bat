@echo off
echo ==============================================
echo ISOlator Path Structure Diagnostic
echo ==============================================
echo.

echo Current Directory Structure:
dir /b

echo.
echo Checking critical files...

if exist "extension.yaml" (
  echo [OK] Found extension.yaml in root directory
  echo --- Contents ---
  type "extension.yaml"
  echo --------------
) else (
  echo [ERROR] extension.yaml not found in root directory
)

echo.
if exist "bin\Debug\net462\extension.yaml" (
  echo [OK] Found extension.yaml in build directory
  echo --- Contents ---
  type "bin\Debug\net462\extension.yaml"
  echo --------------
) else (
  echo [ERROR] extension.yaml not found in build directory
)

echo.
echo Copying extension.yaml to build directory...
if exist "extension.yaml" (
  if not exist "bin\Debug\net462\" mkdir "bin\Debug\net462\"
  copy /Y "extension.yaml" "bin\Debug\net462\"
  if %ERRORLEVEL% EQU 0 (
    echo [OK] Successfully copied extension.yaml to build directory
  ) else (
    echo [ERROR] Failed to copy extension.yaml
  )
) else (
  echo [ERROR] Cannot copy extension.yaml as it doesn't exist
)

echo.
echo Checking for icon.png...
if exist "icon.png" (
  echo [OK] Found icon.png in root directory
  echo Copying icon.png to build directory...
  copy /Y "icon.png" "bin\Debug\net462\"
  if %ERRORLEVEL% EQU 0 (
    echo [OK] Successfully copied icon.png to build directory
  ) else (
    echo [ERROR] Failed to copy icon.png
  )
) else (
  echo [ERROR] icon.png not found in root directory
)

echo.
echo Checking for toolbox.exe...
if exist "toolbox\toolbox.exe" (
  echo [OK] Found toolbox.exe
) else (
  echo [ERROR] toolbox.exe not found at toolbox\toolbox.exe
)

echo.
echo Trying manual pack with direct paths...
if exist "toolbox\toolbox.exe" (
  echo Command: toolbox\toolbox.exe pack bin\Debug\net462\ .
  toolbox\toolbox.exe pack bin\Debug\net462\ .
  set RESULT=%ERRORLEVEL%
  if %RESULT% EQU 0 (
    echo [SUCCESS] Toolbox pack command succeeded!
  ) else (
    echo [ERROR] Toolbox pack failed with error code %RESULT%
    echo Trying with verbose flag...
    toolbox\toolbox.exe pack -v bin\Debug\net462\ .
  )
)

echo.
echo Looking for generated .pext file...
dir /b *.pext 2>nul
if %ERRORLEVEL% NEQ 0 (
  echo No .pext files found
)

echo.
echo ==============================================
echo Path Diagnostic Complete
echo ==============================================
pause