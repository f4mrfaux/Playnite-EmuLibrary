#!/bin/bash
# Clean build script for Playnite extensions on Unix-like systems

# Build the project
dotnet build -c Release

# Directory setup
OUTPUT_DIR="./bin/Release/net462"
PACK_DIR="./pack-temp"
EXT_DIR="$PACK_DIR/extension"

# Create clean directories
rm -rf "$PACK_DIR"
mkdir -p "$EXT_DIR"

# Copy only the necessary files
cp "$OUTPUT_DIR/EmuLibrary.dll" "$EXT_DIR/"
cp "$OUTPUT_DIR/app.config" "$EXT_DIR/"
cp "$OUTPUT_DIR/extension.yaml" "$EXT_DIR/"
cp "$OUTPUT_DIR/icon.png" "$EXT_DIR/"

# For required third-party dependencies that are not provided by Playnite
cp "$OUTPUT_DIR/ZstdSharp.dll" "$EXT_DIR/" 2>/dev/null || true
cp "$OUTPUT_DIR/INIFileParser.dll" "$EXT_DIR/" 2>/dev/null || true
cp "$OUTPUT_DIR/LibHac.dll" "$EXT_DIR/" 2>/dev/null || true
cp "$OUTPUT_DIR/protobuf-net.dll" "$EXT_DIR/" 2>/dev/null || true

# If LibHac.dll is not in the output directory, try to find it in the NuGet packages
if [ ! -f "$EXT_DIR/LibHac.dll" ]; then
    NUGET_DIR="$HOME/.nuget/packages/libhac/0.7.0/lib/net46"
    if [ -f "$NUGET_DIR/LibHac.dll" ]; then
        echo "Found LibHac.dll in NuGet cache, copying to extension directory..."
        cp "$NUGET_DIR/LibHac.dll" "$EXT_DIR/"
    else
        echo "WARNING: LibHac.dll not found. The extension may not work properly without it."
        echo "Please manually download LibHac.dll version 0.7.0 and place it in the extension directory."
    fi
fi

# Get version and plugin ID from extension.yaml
VERSION=$(grep -oP 'Version: \K.+' "$OUTPUT_DIR/extension.yaml")
PLUGIN_ID=$(grep -oP 'Id: \K.+' "$OUTPUT_DIR/extension.yaml")
PEXT_PATH="./EmuLibrary_${PLUGIN_ID}_${VERSION}.pext"

# Pack as ZIP with .pext extension (using zip command)
cd "$PACK_DIR"
zip -r "../$PEXT_PATH" extension
cd ..

# Clean up
rm -rf "$PACK_DIR"

echo "Package created at $PEXT_PATH"