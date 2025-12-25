# Testing Guide for ISOlator Plugin

This document describes how to test and validate the ISOlator plugin on Linux without requiring a Windows machine.

## Quick Start

```bash
# Basic syntax validation (Linux/Mac)
./syntax-check.sh

# Advanced compilation check with Roslyn (Linux with Mono)
./roslyn-check.sh
```

---

## Testing Options

### Option 1: Basic Syntax Check (Recommended for Quick Validation)

**Requirements:** Bash, grep, basic Unix tools

**What it checks:**
- ✓ Double `var` declarations
- ✓ Double semicolons
- ✓ Mismatched braces (warning only)

**Usage:**
```bash
chmod +x syntax-check.sh
./syntax-check.sh
```

**Output:**
```
========================================
ISOlator Plugin - C# Syntax Validator
========================================

Checking for common C# syntax errors...

========================================
Files checked: 54
✓ No obvious syntax errors found

NOTE: This is a basic syntax check. For full compilation
validation, you'll need to build on Windows or use CI/CD.
```

**Limitations:**
- Does NOT validate type correctness
- Does NOT check NuGet references
- Does NOT verify .NET Framework compatibility
- Best for catching obvious syntax mistakes before committing

---

### Option 2: Roslyn Compilation Check (Advanced)

**Requirements:** Mono with Roslyn compiler

**Installation (Arch Linux):**
```bash
sudo pacman -S mono
```

**What it checks:**
- ✓ Full C# syntax validation
- ✓ Type inference
- ✓ Method signatures
- ✓ Variable declarations
- ✗ NuGet packages (not validated)
- ✗ .NET Framework 4.6.2 APIs (may show false errors)

**Usage:**
```bash
chmod +x roslyn-check.sh
./roslyn-check.sh
```

**Limitations:**
- Cannot validate PlayniteSDK references (NuGet not resolved)
- May show false positives for .NET Framework-specific APIs
- Does not test XAML resources
- Does not create a functioning plugin DLL

---

### Option 3: GitHub Actions CI/CD (Full Windows Build)

**Requirements:** GitHub repository (free)

**Setup:**
The `.github/workflows/build-test.yml` file is already configured. Simply push to GitHub:

```bash
git push origin pcinstaller-mature-logic
```

**What it does:**
1. **Builds on actual Windows Server** with MSBuild
2. Restores NuGet packages (PlayniteSDK, etc.)
3. Compiles for .NET Framework 4.6.2
4. Creates release artifacts
5. Runs Linux syntax check in parallel

**Viewing results:**
1. Go to your GitHub repository
2. Click "Actions" tab
3. View build results and logs
4. Download compiled DLL from "Artifacts"

**Triggers:**
- Push to: main, master, develop, pcinstaller-mature-logic
- Pull requests to: main, master, develop
- Manual trigger (workflow_dispatch)

---

## Comparison Table

| Feature | syntax-check.sh | roslyn-check.sh | GitHub Actions |
|---------|-----------------|-----------------|----------------|
| **Requirements** | Bash | Mono | GitHub account |
| **Speed** | 1-2 seconds | 10-30 seconds | 2-5 minutes |
| **Syntax errors** | ✓ Basic | ✓ Full | ✓ Full |
| **Type checking** | ✗ | ✓ Limited | ✓ Complete |
| **NuGet packages** | ✗ | ✗ | ✓ |
| **.NET Framework** | ✗ | ✗ | ✓ |
| **XAML resources** | ✗ | ✗ | ✓ |
| **Build artifacts** | ✗ | ✗ | ✓ DLL + ZIP |
| **Cost** | Free | Free | Free |

---

## Recommended Workflow

### Before Committing

```bash
# Quick syntax validation
./syntax-check.sh
```

If this passes, your code likely has no obvious syntax errors.

### Before Pushing to GitHub

```bash
# More thorough check (if you have Mono installed)
./roslyn-check.sh
```

This catches more subtle syntax and type errors.

### For Release Builds

```bash
# Push to GitHub and let CI/CD build on Windows
git push origin pcinstaller-mature-logic

# Or manually trigger the workflow on GitHub
# (Actions tab → Build and Test → Run workflow)
```

This ensures **100% compatibility** with Windows and Playnite SDK.

---

## Troubleshooting

### "Bad interpreter" error on Linux

**Problem:**
```
./syntax-check.sh: bad interpreter: /bin/bash^M
```

**Solution:**
```bash
dos2unix syntax-check.sh roslyn-check.sh
# or
sed -i 's/\r$//' syntax-check.sh roslyn-check.sh
```

### Mono not found

**Install on Arch Linux:**
```bash
sudo pacman -S mono
```

**Install on Ubuntu/Debian:**
```bash
sudo apt-get install mono-complete
```

**Install on macOS:**
```bash
brew install mono
```

### GitHub Actions failing

1. Check the Actions tab for detailed logs
2. Verify ISOlator.sln exists in repository root
3. Ensure NuGet packages are configured correctly
4. Check that .csproj targets net462

---

## CI/CD Artifacts

When GitHub Actions builds successfully, you can download:

**ISOlator-Plugin** artifact contains:
- EmuLibrary.dll (compiled plugin)
- All dependencies
- extension.yaml
- icon.png

**ISOlator-Release** artifact (main/master branch only):
- ISOlator-Plugin.zip (ready to install in Playnite)

---

## Next Steps After Testing

Once tests pass:

1. **Install in Playnite** (Windows required)
   - Extract ZIP to `%AppData%/Playnite/Extensions/`
   - Restart Playnite
   - Configure emulator mappings in Settings

2. **Manual Testing**
   - Test each RomType (SingleFile, MultiFile, PCInstaller, ISOInstaller)
   - Verify install/uninstall operations
   - Test Settings UI
   - Verify game name normalization

3. **Create Release**
   - Tag version: `git tag v1.6.0`
   - Push tags: `git push --tags`
   - Create GitHub Release with artifacts

---

## Known Limitations

### What These Tests CANNOT Catch

1. **Runtime Errors**
   - Null reference exceptions
   - File I/O errors
   - Path resolution issues

2. **Playnite Integration**
   - UI rendering
   - Settings binding
   - Database operations
   - Event handling

3. **Platform-Specific Issues**
   - Windows-only APIs
   - Path separator differences
   - Registry access

**Bottom line:** These tests validate **syntax and compilation**, not **functionality**. Manual testing on Windows with Playnite is still required for release.

---

## Questions?

See:
- `PRODUCTION_READINESS_REPORT.md` - Code quality analysis
- `CLAUDE.md` - Project overview and coding guidelines
- GitHub Actions logs - Detailed build output
