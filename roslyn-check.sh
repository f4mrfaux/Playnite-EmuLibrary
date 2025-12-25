#!/bin/bash
# Roslyn-based C# syntax checker using Mono
# Attempts to compile C# files directly without full project build

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo "========================================"
echo "ISOlator - Roslyn Syntax Compilation Check"
echo "========================================"
echo ""

# Check if Mono is available
if ! command -v mono &> /dev/null; then
    echo -e "${RED}✗ Mono not found. Install with: sudo pacman -S mono${NC}"
    exit 1
fi

# Check for Roslyn compiler
ROSLYN_CSC="/usr/lib/mono/msbuild/Current/bin/Roslyn/csc.exe"
if [ ! -f "$ROSLYN_CSC" ]; then
    echo -e "${RED}✗ Roslyn compiler not found at $ROSLYN_CSC${NC}"
    echo "Falling back to basic syntax check..."
    exec ./syntax-check.sh
fi

echo -e "${BLUE}Using Roslyn compiler: $ROSLYN_CSC${NC}"
echo ""

# Collect all C# source files
CS_FILES=$(find EmuLibrary -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*" | tr '\n' ' ')

if [ -z "$CS_FILES" ]; then
    echo -e "${RED}✗ No C# files found${NC}"
    exit 1
fi

FILE_COUNT=$(echo $CS_FILES | wc -w)
echo -e "${BLUE}Found $FILE_COUNT C# source files${NC}"
echo ""

# Create a temporary directory for output
TEMP_DIR=$(mktemp -d)
trap "rm -rf $TEMP_DIR" EXIT

echo "Attempting syntax-only compilation..."
echo ""

# Try to compile with Roslyn (syntax check only, no linking)
# /target:library = build as DLL
# /nologo = suppress copyright banner
# /warnaserror- = don't treat warnings as errors
# /debug- = no debug info
# /out: = output file

mono "$ROSLYN_CSC" \
    /target:library \
    /nologo \
    /debug- \
    /warnaserror- \
    /nowarn:1701,1702,CS8019,CS0168,CS0219 \
    /out:"$TEMP_DIR/syntax-check.dll" \
    $CS_FILES 2>&1 | tee "$TEMP_DIR/compile.log"

COMPILE_RESULT=$?

echo ""
echo "========================================"

if [ $COMPILE_RESULT -eq 0 ]; then
    echo -e "${GREEN}✓ Compilation successful! No syntax errors found.${NC}"
    echo ""
    echo "NOTE: This validates C# syntax, but does NOT verify:"
    echo "  - NuGet package references (PlayniteSDK, etc.)"
    echo "  - .NET Framework 4.6.2 compatibility"
    echo "  - XAML resources"
    echo "  - Full linking and assembly generation"
    echo ""
    echo "For complete validation, build on Windows or use CI/CD."
    exit 0
else
    echo -e "${RED}✗ Compilation failed with syntax errors${NC}"
    echo ""
    echo "Review the errors above. Common issues:"
    echo "  - Missing semicolons"
    echo "  - Mismatched braces"
    echo "  - Invalid variable declarations"
    echo "  - Type errors"
    exit 1
fi
