#!/bin/bash
set -e

echo "Restoring NuGet packages..."
nuget restore EmuLibrary.sln

echo "Building project with Mono..."
msbuild /p:Configuration=Debug /p:Platform="Any CPU" /p:PostBuildEvent= EmuLibrary.sln

echo "Build completed. Output should be in EmuLibrary/bin/Debug/net462/"
echo "Note: The PostBuildEvent was skipped because toolbox.exe is a Windows executable."