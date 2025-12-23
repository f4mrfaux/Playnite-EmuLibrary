@echo off
echo Simple fix for ISOlator build issues
echo.

if exist "EmuLibrary\extension.yaml" (
  echo Found extension.yaml in EmuLibrary folder, copying to root...
  copy /Y "EmuLibrary\extension.yaml" "extension.yaml"
) else (
  echo extension.yaml not found in EmuLibrary folder
)

if exist "EmuLibrary\icon.png" (
  echo Found icon.png in EmuLibrary folder, copying to root...
  copy /Y "EmuLibrary\icon.png" "icon.png"
) else (
  echo icon.png not found in EmuLibrary folder
)

if not exist "bin\Debug\net462" mkdir "bin\Debug\net462"

if exist "extension.yaml" (
  echo Copying extension.yaml to build folder...
  copy /Y "extension.yaml" "bin\Debug\net462\"
) else (
  echo extension.yaml not found in root
)

if exist "icon.png" (
  echo Copying icon.png to build folder...
  copy /Y "icon.png" "bin\Debug\net462\"
) else (
  echo icon.png not found in root
)

echo.
echo Attempting to run toolbox pack...
if exist "toolbox\toolbox.exe" (
  if exist "bin\Debug\net462\extension.yaml" (
    echo Running: toolbox\toolbox.exe pack bin\Debug\net462\ .
    toolbox\toolbox.exe pack bin\Debug\net462\ .
    echo Pack completed with error level: %ERRORLEVEL%
  ) else (
    echo Cannot run pack - extension.yaml missing from build folder
  )
) else (
  echo toolbox.exe not found
)

echo.
echo Done!
pause