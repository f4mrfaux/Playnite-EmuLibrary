# Production Readiness Report - ISOlator Plugin
**Date:** December 25, 2025
**Reviewer:** Claude Code Production Analysis
**Target:** Playnite SDK 6.4.0 / .NET Framework 4.6.2

## Executive Summary

The ISOlator plugin codebase has been analyzed against Playnite SDK best practices and production standards. **Overall Status: NOT PRODUCTION READY** due to critical syntax errors and thread safety violations. However, the code architecture is solid and the issues are straightforward to fix.

**Critical Issues:** 2 (MUST FIX before release)
**High Priority Issues:** 3 (thread safety violations)
**Medium Priority Issues:** 2
**Good Practices Observed:** 8

---

## Critical Issues (MUST FIX)

### 1. ❌ CRITICAL: Syntax Errors in MultiFileScanner.cs (COMPILATION FAILURE)

**Location:** `EmuLibrary/RomTypes/MultiFile/MultiFileScanner.cs`

**Issue:** Double "var" keyword on lines 82 and 178 - **this code will not compile**.

**Line 82:**
```csharp
var baseFileName = StringExtensions.GetPathWithoutAllExtensions(Path.GetFileName(file.Name));
var var patterns = EmuLibrary.Settings.EnableGameNameNormalization  // ❌ SYNTAX ERROR
    ? EmuLibrary.Settings.GameNameNormalizationPatterns?.ToArray()
    : null;
gameName = StringExtensions.NormalizeGameName(baseFileName, patterns);
```

**Line 178:**
```csharp
var baseFileName = StringExtensions.GetPathWithoutAllExtensions(Path.GetFileName(file.Name));
var var patterns = EmuLibrary.Settings.EnableGameNameNormalization  // ❌ SYNTAX ERROR
    ? EmuLibrary.Settings.GameNameNormalizationPatterns?.ToArray()
    : null;
gameName = StringExtensions.NormalizeGameName(baseFileName, patterns);
```

**Impact:** Plugin will not compile or load in Playnite.

**Fix Required:** Remove duplicate "var" keyword:
```csharp
var patterns = EmuLibrary.Settings.EnableGameNameNormalization
    ? EmuLibrary.Settings.GameNameNormalizationPatterns?.ToArray()
    : null;
```

**Priority:** CRITICAL - Must be fixed immediately

---

### 2. ❌ CRITICAL: Thread Safety Violation in RemoveSuperUninstalledGames()

**Location:** `EmuLibrary/EmuLibrary.cs:474, 488`

**Issue:** UI dialogs called without UIDispatcher when invoked from background thread (line 333 in GetGames()).

**Code:**
```csharp
// Line 333 - Can be called from background library update thread
if (Settings.AutoRemoveUninstalledGamesMissingFromSource)
{
    RemoveSuperUninstalledGames(false, args.CancelToken);  // ❌ Background thread
}

// Lines 474, 488 - Direct UI calls without dispatcher
private void RemoveSuperUninstalledGames(bool promptUser, CancellationToken ct)
{
    var toRemove = _scanners.Values.SelectMany(s => s.GetUninstalledGamesMissingSourceFiles(ct));
    if (toRemove.Any())
    {
        System.Windows.MessageBoxResult res;
        if (promptUser)
        {
            res = PlayniteApi.Dialogs.ShowMessage(...);  // ❌ UI operation on background thread!
        }
        // ...
    }
    else if (promptUser)
    {
        PlayniteApi.Dialogs.ShowMessage("Nothing to do.");  // ❌ UI operation on background thread!
    }
}
```

**Impact:** Race conditions, potential crashes, UI freezes.

**Fix Required:** Wrap all UI operations in UIDispatcher.Invoke():
```csharp
if (promptUser)
{
    Playnite.MainView.UIDispatcher.Invoke(() =>
    {
        res = PlayniteApi.Dialogs.ShowMessage(...);
    });
}
```

**Priority:** CRITICAL - Thread safety is mandatory per Playnite SDK docs

---

## High Priority Issues

### 3. ⚠️ HIGH: Thread Safety Violations in Uninstall Controllers

**Location:**
- `EmuLibrary/RomTypes/MultiFile/MultiFileUninstallController.cs:28`
- `EmuLibrary/RomTypes/SingleFile/SingleFileUninstallController.cs:29`

**Issue:** Dialog calls in uninstall controllers without UIDispatcher. Uninstall controllers run on background threads.

**Code (MultiFileUninstallController.cs:28):**
```csharp
public override void Uninstall(UninstallActionArgs args)
{
    if (!Directory.Exists(Game.InstallDirectory))
    {
        _emuLibrary.Playnite.Dialogs.ShowMessage(...);  // ❌ UI on background thread
        Game.IsInstalled = false;
        return;
    }
    // ...
}
```

**Impact:** Thread safety violations, potential UI exceptions.

**Fix Required:** Wrap in UIDispatcher (see PCInstallerInstallController for correct pattern):
```csharp
_emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
{
    _emuLibrary.Playnite.Dialogs.ShowMessage(...);
});
```

**Priority:** HIGH - Could cause instability

---

## Medium Priority Issues

### 4. ⚠️ MEDIUM: Microsoft.VisualBasic Reference

**Location:** `EmuLibrary/EmuLibrary.csproj:53`

**Issue:** References Microsoft.VisualBasic assembly.

**Code:**
```xml
<ItemGroup>
  <Reference Include="Microsoft.VisualBasic" />
</ItemGroup>
```

**Analysis:** This is a .NET Framework BCL assembly (not Playnite-specific), so it's technically allowed. However, it's unusual and may indicate legacy code.

**Question:** What functionality requires Microsoft.VisualBasic? This namespace is rarely needed in modern C# code.

**Recommendation:**
- Audit code to identify what uses Microsoft.VisualBasic
- Consider refactoring to pure C# if possible
- If truly needed, document the reason with a comment

**Priority:** MEDIUM - Not a blocker, but worth investigating

---

### 5. ⚠️ MEDIUM: RemoveSuperUninstalledGames() Performance Warning

**Location:** `EmuLibrary/EmuLibrary.cs:474`

**Issue:** User-facing dialog warns "This may take a while, during while Playnite will seem frozen" - indicates potential UI freeze.

**Code:**
```csharp
res = PlayniteApi.Dialogs.ShowMessage(
    $"Delete {toRemove.Count()} library entries?\n\n(This may take a while, during while Playnite will seem frozen.)",
    "Confirm deletion",
    System.Windows.MessageBoxButton.YesNo
);
```

**Analysis:** The `PlayniteApi.Database.Games.Remove(toRemove)` operation (line 483) may be slow for large collections.

**Recommendation:**
- Consider using BufferedUpdate() around the Remove() operation
- Show progress dialog for operations >100 items
- Or move the operation to a background task with progress reporting

**Priority:** MEDIUM - User experience issue, not a functional bug

---

## Good Practices Observed ✅

1. **✅ Correct Target Framework:** Uses .NET Framework 4.6.2 (matches Playnite SDK 6.4.0)

2. **✅ SDK-Only References:** Only references PlayniteSDK package, no direct Playnite.Common or internal assemblies

3. **✅ BufferedUpdate Usage:** Extensive use of `using (Playnite.Database.BufferedUpdate())` for bulk operations:
   - EmuLibrary.cs:95, 143, 496, 686
   - PCInstallerScanner.cs:496, 686
   - ISOInstallerScanner.cs:420, 609

4. **✅ Cancellation Token Support:** Proper cancellation handling throughout:
   - All GetGames() implementations check `args.CancelToken.IsCancellationRequested`
   - GetUninstalledGamesMissingSourceFiles() accepts CancellationToken

5. **✅ Extensive Error Handling:** Comprehensive try/catch blocks with proper logging

6. **✅ Proper Logging:** Uses `_emuLibrary.Logger` consistently (Info, Warn, Error, Debug levels)

7. **✅ UIDispatcher Pattern (Partial):** PCInstallerInstallController demonstrates correct UIDispatcher usage:
   ```csharp
   _emuLibrary.Playnite.MainView.UIDispatcher.Invoke(() =>
   {
       installDir = _emuLibrary.Playnite.Dialogs.SelectFolder();
   });
   ```
   This pattern just needs to be applied consistently across other controllers.

8. **✅ Settings Architecture:** Clean ObservableCollection-based settings with WPF binding support

---

## Detailed Analysis

### Project Configuration
✅ **Pass** - Targets net462, uses PlayniteSDK 6.4.0, proper WPF support

### SDK Usage
✅ **Pass** - Only PlayniteSDK package references, no forbidden assemblies
⚠️ **Review** - Microsoft.VisualBasic reference (BCL, not Playnite-specific, but unusual)

### Thread Safety
❌ **FAIL** - Multiple UI operations without UIDispatcher:
- EmuLibrary.cs:474, 488 (RemoveSuperUninstalledGames)
- MultiFileUninstallController.cs:28
- SingleFileUninstallController.cs:29

✅ **Good Example** - PCInstallerInstallController.cs:77, 173, 327, 381 (correct UIDispatcher usage)

### GetGames() Implementation
✅ **Pass** - All scanners:
- Yield GameMetadata objects
- Handle cancellation tokens
- Set proper GameId/PluginId
- Include platform metadata
- No direct UI operations in GetGames()

### Error Handling
✅ **Pass** - Comprehensive try/catch with logging throughout

### Resource Cleanup
✅ **Pass** - Uses `using` statements for BufferedUpdate, SafeFileEnumerator

### GameId/PluginId Patterns
✅ **Pass** - All GameMetadata objects:
- Set unique GameId via `info.AsGameId()`
- Set PluginId = EmuLibrary.PluginId
- Include proper GameActions
- Migration logic handles legacy formats (EmuLibrary.cs:91-113, 122-287)

---

## Compilation Test Status

❌ **WILL NOT COMPILE** due to syntax errors in MultiFileScanner.cs (lines 82, 178)

---

## Recommendations

### Immediate Actions (Before Release)

1. **FIX CRITICAL:** Remove double "var" in MultiFileScanner.cs:82, 178
2. **FIX CRITICAL:** Wrap all dialog calls in UIDispatcher.Invoke():
   - EmuLibrary.cs:474, 488
   - MultiFileUninstallController.cs:28
   - SingleFileUninstallController.cs:29

### High Priority (Next Sprint)

3. **INVESTIGATE:** Determine why Microsoft.VisualBasic is referenced and document or remove
4. **IMPROVE:** Optimize RemoveSuperUninstalledGames() for better UX (progress dialog or BufferedUpdate)

### Nice to Have

5. Add XML documentation comments to public APIs
6. Consider adding unit tests for GameId migration logic
7. Add integration tests for scanner implementations

---

## Test Plan

### Pre-Release Testing

1. **Compilation Test:** Verify project compiles cleanly after syntax fixes
2. **Thread Safety Test:**
   - Enable AutoRemoveUninstalledGamesMissingFromSource
   - Trigger library update
   - Verify no thread exceptions
3. **Dialog Test:**
   - Uninstall game when directory missing → verify dialog appears
   - Remove missing games → verify dialogs work
4. **Migration Test:**
   - Test with games from each RomType (SingleFile, MultiFile, PCInstaller, ISOInstaller)
   - Verify GameId stability across install/uninstall cycles

### Regression Testing

5. **Scanner Test:** Verify all 4 scanners find games correctly
6. **Install/Uninstall:** Test all 4 RomType install/uninstall flows
7. **Settings:** Verify all settings persist and load correctly

---

## Conclusion

**Current Status:** NOT PRODUCTION READY

**Blocking Issues:** 2 critical (syntax errors, thread safety)

**Effort to Fix:** ~2-4 hours of development work

**Code Quality:** Good architecture, just needs polish for production

**After Fixes:** Should be production-ready with proper testing

The codebase demonstrates solid understanding of Playnite SDK patterns (BufferedUpdate, cancellation tokens, proper GameId/PluginId usage). The issues found are straightforward to fix - mostly applying the UIDispatcher pattern consistently (which PCInstallerInstallController already demonstrates correctly).

---

## References

- [Playnite SDK Plugins Introduction](https://api.playnite.link/docs/tutorials/extensions/plugins.html)
- [Playnite SDK Library Plugins Guide](https://api.playnite.link/docs/tutorials/extensions/libraryPlugins.html)
- Thread Safety Documentation: "Playnite's SDK is not fully thread safe"
- Assembly Reference Requirements: "DO NOT reference non-SDK Playnite assemblies"
