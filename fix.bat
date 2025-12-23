@echo off
echo Fixing ISOlator post-build issue...

REM Copy extension.yaml and icon.png from EmuLibrary folder to project root
copy /Y "EmuLibrary\extension.yaml" "extension.yaml"
copy /Y "EmuLibrary\icon.png" "icon.png"

echo Done! Now rebuild the project.