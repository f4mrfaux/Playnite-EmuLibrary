@echo off
echo ==============================================
echo ISOlator File Copy Utility
echo ==============================================
echo.

echo Step 1: Locating required files...

REM Check if files exist in EmuLibrary folder
set EXTENSION_YAML_SOURCE=
set ICON_PNG_SOURCE=

if exist "EmuLibrary\extension.yaml" (
  set EXTENSION_YAML_SOURCE=EmuLibrary\extension.yaml
  echo [OK] Found extension.yaml in EmuLibrary folder
) else (
  echo [ERROR] extension.yaml not found in EmuLibrary folder
  echo         Searching in other locations...
  for /f "delims=" %%i in ('dir /s /b extension.yaml 2^>nul') do (
    echo [FOUND] extension.yaml at: %%i
    set EXTENSION_YAML_SOURCE=%%i
  )
)

if exist "EmuLibrary\icon.png" (
  set ICON_PNG_SOURCE=EmuLibrary\icon.png
  echo [OK] Found icon.png in EmuLibrary folder
) else (
  echo [ERROR] icon.png not found in EmuLibrary folder
  echo         Searching in other locations...
  for /f "delims=" %%i in ('dir /s /b icon.png 2^>nul') do (
    echo [FOUND] icon.png at: %%i
    set ICON_PNG_SOURCE=%%i
  )
)

echo.
echo Step 2: Copying files to root directory...

if defined EXTENSION_YAML_SOURCE (
  echo Copying %EXTENSION_YAML_SOURCE% to root directory...
  copy /Y "%EXTENSION_YAML_SOURCE%" "extension.yaml"
  if %ERRORLEVEL% EQU 0 (
    echo [OK] extension.yaml copied to root directory
  ) else (
    echo [ERROR] Failed to copy extension.yaml
  )
) else (
  echo [ERROR] Could not find extension.yaml anywhere
)

if defined ICON_PNG_SOURCE (
  echo Copying %ICON_PNG_SOURCE% to root directory...
  copy /Y "%ICON_PNG_SOURCE%" "icon.png"
  if %ERRORLEVEL% EQU 0 (
    echo [OK] icon.png copied to root directory
  ) else (
    echo [ERROR] Failed to copy icon.png
  )
) else (
  echo [ERROR] Could not find icon.png anywhere
)

echo.
echo Step 3: Copying files to build directory...

if not exist "bin\Debug\net462" (
  echo Creating build directory...
  mkdir "bin\Debug\net462"
)

if exist "extension.yaml" (
  echo Copying extension.yaml to build directory...
  copy /Y "extension.yaml" "bin\Debug\net462\"
  if %ERRORLEVEL% EQU 0 (
    echo [OK] extension.yaml copied to build directory
  ) else (
    echo [ERROR] Failed to copy extension.yaml to build directory
  )
) else (
  echo [ERROR] extension.yaml not found in root directory, cannot copy to build directory
)

if exist "icon.png" (
  echo Copying icon.png to build directory...
  copy /Y "icon.png" "bin\Debug\net462\"
  if %ERRORLEVEL% EQU 0 (
    echo [OK] icon.png copied to build directory
  ) else (
    echo [ERROR] Failed to copy icon.png to build directory
  )
) else (
  echo [ERROR] icon.png not found in root directory, cannot copy to build directory
)

echo.
echo Step 4: Trying manual pack...

if exist "bin\Debug\net462\extension.yaml" (
  echo extension.yaml exists in build directory, proceeding with pack...
  if exist "toolbox\toolbox.exe" (
    echo Command: toolbox\toolbox.exe pack bin\Debug\net462\ .
    toolbox\toolbox.exe pack bin\Debug\net462\ .
    set PACK_RESULT=%ERRORLEVEL%
    if !PACK_RESULT! EQU 0 (
      echo [SUCCESS] Toolbox pack command succeeded!
    ) else (
      echo [ERROR] Toolbox pack failed with error code !PACK_RESULT!
      echo Trying with verbose flag...
      toolbox\toolbox.exe pack -v bin\Debug\net462\ .
    )
  ) else (
    echo [ERROR] toolbox.exe not found
  )
) else (
  echo [ERROR] Cannot pack, extension.yaml missing from build directory
)

echo.
echo ==============================================
echo File Copy Utility Complete
echo ==============================================
pause