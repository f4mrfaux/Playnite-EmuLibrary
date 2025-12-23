@echo off
echo Cleaning build directory...
if exist "EmuLibrary\bin" rmdir /s /q "EmuLibrary\bin"
if exist "EmuLibrary\obj" rmdir /s /q "EmuLibrary\obj"
echo Cleaning old solution files...
if exist "EmuLibrary.sln" echo Keeping backup of EmuLibrary.sln
if exist "EmuLibrary\EmuLibrary.csproj" echo Keeping backup of EmuLibrary.csproj
echo Done cleaning.