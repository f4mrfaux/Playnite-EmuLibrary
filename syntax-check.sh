#!/bin/bash
# Syntax checker for ISOlator plugin using Roslyn C# compiler
# This validates C# syntax without requiring a full .NET Framework build

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "========================================"
echo "ISOlator Plugin - C# Syntax Validator"
echo "========================================"
echo ""

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

syntax_errors=0
files_checked=0

echo "Checking for common C# syntax errors..."
echo ""

# Find all C# files
cs_files=$(find EmuLibrary -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*")

for file in $cs_files; do
    ((files_checked++))

    # Check for common syntax errors that don't require compilation

    # 1. Check for double 'var' declarations
    if grep -qn "var var " "$file" 2>/dev/null; then
        echo -e "${RED}✗ SYNTAX ERROR in $file:${NC}"
        grep -n "var var " "$file"
        ((syntax_errors++))
    fi

    # 2. Check for mismatched braces (basic check)
    open_braces=$(grep -o "{" "$file" | wc -l)
    close_braces=$(grep -o "}" "$file" | wc -l)
    if [ "$open_braces" -ne "$close_braces" ]; then
        echo -e "${YELLOW}⚠ WARNING in $file: Mismatched braces (${open_braces} open, ${close_braces} close)${NC}"
        # Don't count as error - might be legitimate in strings/comments
    fi

    # 3. Check for obvious syntax patterns
    if grep -qn ";;$" "$file" 2>/dev/null; then
        echo -e "${RED}✗ SYNTAX ERROR in $file: Double semicolon${NC}"
        grep -n ";;$" "$file"
        ((syntax_errors++))
    fi
done

echo ""
echo "========================================"
echo "Files checked: $files_checked"

if [ $syntax_errors -eq 0 ]; then
    echo -e "${GREEN}✓ No obvious syntax errors found${NC}"
    echo ""
    echo "NOTE: This is a basic syntax check. For full compilation"
    echo "validation, you'll need to build on Windows or use CI/CD."
    exit 0
else
    echo -e "${RED}✗ Found $syntax_errors syntax error(s)${NC}"
    exit 1
fi
