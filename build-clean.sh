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
cp "$OUTPUT_DIR/ZstdSharp.dll" "$EXT_DIR/"
cp "$OUTPUT_DIR/INIFileParser.dll" "$EXT_DIR/"
cp "$OUTPUT_DIR/LibHac.dll" "$EXT_DIR/"

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