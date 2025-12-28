@echo off
echo ----------------------------------------------
echo GameVault Post-Build Diagnostic Tool
echo ----------------------------------------------
echo.

echo Environment Information:
echo Current Directory: %CD%
cd
echo.

echo Checking project structure...
if exist "EmuLibrary\GameVault.csproj" echo [OK] GameVault.csproj found in EmuLibrary folder
if not exist "EmuLibrary\GameVault.csproj" echo [ERROR] GameVault.csproj not found in EmuLibrary folder

if exist "GameVault.sln" echo [OK] GameVault.sln found
if not exist "GameVault.sln" echo [ERROR] GameVault.sln not found

echo.
echo Checking original project files (for backup)...
if exist "EmuLibrary\EmuLibrary.csproj" echo [INFO] Original EmuLibrary.csproj still exists
if exist "EmuLibrary.sln" echo [INFO] Original EmuLibrary.sln still exists

echo.
echo Checking build output...
if exist "EmuLibrary\bin\Debug\net462\EmuLibrary.dll" echo [OK] EmuLibrary.dll found
if not exist "EmuLibrary\bin\Debug\net462\EmuLibrary.dll" echo [ERROR] EmuLibrary.dll not found

if exist "EmuLibrary\bin\Debug\net462\extension.yaml" (
  echo [OK] extension.yaml found in build directory
  echo --- extension.yaml contents ---
  type "EmuLibrary\bin\Debug\net462\extension.yaml"
  echo -----------------------------
) else (
  echo [ERROR] extension.yaml NOT found in build directory
)

if exist "EmuLibrary\extension.yaml" (
  echo [OK] extension.yaml found in source directory
  echo --- extension.yaml contents ---
  type "EmuLibrary\extension.yaml"
  echo -----------------------------
) else (
  echo [ERROR] extension.yaml NOT found in source directory
)

if exist "EmuLibrary\bin\Debug\net462\icon.png" echo [OK] icon.png found
if not exist "EmuLibrary\bin\Debug\net462\icon.png" echo [ERROR] icon.png not found

echo.
echo Checking for GUIDs in codebase...
echo --- Looking for plugin GUID in EmuLibrary.cs ---
findstr /C:"PluginId" "EmuLibrary\EmuLibrary.cs"
echo.

echo --- Looking for GUID in AssemblyInfo.cs ---
findstr /C:"Guid" "EmuLibrary\Properties\AssemblyInfo.cs"
echo.

echo Checking toolbox...
if exist "toolbox\toolbox.exe" echo [OK] toolbox.exe found
if not exist "toolbox\toolbox.exe" echo [ERROR] toolbox.exe not found

echo.
echo Running toolbox with verbose flag...
if exist "toolbox\toolbox.exe" (
  echo Command: toolbox\toolbox.exe pack -v EmuLibrary\bin\Debug\net462\ .
  toolbox\toolbox.exe pack -v EmuLibrary\bin\Debug\net462\ .
  echo Exit code: %ERRORLEVEL%
)

echo.
echo ----------------------------------------------
echo Checking for extension files...
echo ----------------------------------------------
echo.
dir /b *.pext 2>nul
if %ERRORLEVEL% NEQ 0 echo No .pext files found in current directory

echo.
echo ----------------------------------------------
echo Diagnostic complete
echo ----------------------------------------------

pause