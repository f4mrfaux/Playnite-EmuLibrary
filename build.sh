#!/bin/bash
set -e

echo "Restoring NuGet packages..."
nuget restore EmuLibrary.sln

echo "Building project with Mono..."
msbuild /p:Configuration=Debug /p:Platform="Any CPU" /p:PostBuildEvent= EmuLibrary.sln

# Ensure dependencies are copied
if [ ! -f "EmuLibrary/bin/Debug/LibHac.dll" ]; then
  echo "Copying LibHac.dll from packages..."
  find ~/.nuget/packages/libhac/0.7.0 -name "LibHac.dll" -exec cp {} EmuLibrary/bin/Debug/ \;
fi

if [ ! -f "EmuLibrary/bin/Debug/protobuf-net.dll" ]; then
  echo "Copying protobuf-net.dll from packages..."
  find ~/.nuget/packages/protobuf-net/2.4.6 -name "protobuf-net.dll" -exec cp {} EmuLibrary/bin/Debug/ \;
fi

echo "Build completed. Output should be in EmuLibrary/bin/Debug/net462/"
echo "Note: The PostBuildEvent was skipped because toolbox.exe is a Windows executable."