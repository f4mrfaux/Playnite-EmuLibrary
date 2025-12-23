@echo off
echo ==============================================
echo ISOlator Build and Pack Tool
echo ==============================================
echo.

echo Step 1: Cleaning build folders...
if exist "EmuLibrary\bin" rmdir /s /q "EmuLibrary\bin"
if exist "EmuLibrary\obj" rmdir /s /q "EmuLibrary\obj"
echo [OK] Clean completed

echo.
echo Step 2: Building solution...
msbuild ISOlator.sln /p:Configuration=Debug
if %ERRORLEVEL% NEQ 0 (
  echo [ERROR] Build failed with error code %ERRORLEVEL%
  goto :end
) else (
  echo [OK] Build completed successfully
)

echo.
echo Step 3: Verifying output files...
if not exist "EmuLibrary\bin\Debug\net462\EmuLibrary.dll" (
  echo [ERROR] EmuLibrary.dll not found!
  goto :end
) else (
  echo [OK] EmuLibrary.dll found
)

echo.
echo Step 4: Ensuring correct extension.yaml...
echo --- Current extension.yaml contents ---
type "EmuLibrary\extension.yaml"
echo --------------------------------------
echo.

copy /Y "EmuLibrary\extension.yaml" "EmuLibrary\bin\Debug\net462\"
if %ERRORLEVEL% NEQ 0 (
  echo [ERROR] Failed to copy extension.yaml
  goto :end
) else (
  echo [OK] extension.yaml updated
)

echo.
echo Step 5: Ensuring icon is present...
if not exist "EmuLibrary\bin\Debug\net462\icon.png" (
  echo [ERROR] icon.png not found in output directory
  copy /Y "EmuLibrary\icon.png" "EmuLibrary\bin\Debug\net462\"
  echo [OK] icon.png copied to output directory
) else (
  echo [OK] icon.png is present
)

echo.
echo Step 6: Manually running toolbox pack...
if not exist "toolbox\toolbox.exe" (
  echo [ERROR] toolbox.exe not found!
  goto :end
) else (
  echo Executing: toolbox\toolbox.exe pack EmuLibrary\bin\Debug\net462\ .
  toolbox\toolbox.exe pack EmuLibrary\bin\Debug\net462\ .
  set EXIT_CODE=%ERRORLEVEL%
  if %EXIT_CODE% NEQ 0 (
    echo [ERROR] Toolbox pack failed with error code %EXIT_CODE%
    echo Trying with verbose output...
    echo.
    echo Toolbox.exe verbose output:
    toolbox\toolbox.exe pack -v EmuLibrary\bin\Debug\net462\ .
  ) else (
    echo [OK] Toolbox pack completed successfully
  )
)

echo.
echo Step 7: Checking for packed extension file...
echo Looking for: ISOlator_f0a33e7a-1f30-4761-b3ab-0fc73d54a7c3_*.pext
for %%f in (ISOlator_f0a33e7a-1f30-4761-b3ab-0fc73d54a7c3_*.pext) do (
  echo [OK] Found packed extension: %%f
)

:end
echo.
echo ==============================================
echo Build process complete
echo ==============================================

pause