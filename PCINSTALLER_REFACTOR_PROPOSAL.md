# PCInstaller Consolidation Refactor Proposal

## Current State

**Problem:**
- "PCInstaller" name doesn't clearly indicate it's for EXE files
- Users must choose between PCInstaller and ISOInstaller, which is confusing
- Archives containing ISO files require ISOInstaller mapping (not intuitive)

**Current Behavior:**
- PCInstaller: Handles .exe files and archives containing EXE
- ISOInstaller: Handles .iso files and archives containing ISO
- If PCInstaller finds ISO in archive → Error message

## Proposed Solution

**Consolidate PCInstaller to be a "smart" handler:**
- Handles: EXE files, archives (ZIP/RAR/7Z), and routes based on content
- If archive contains ISO → Automatically mounts and installs (no error)
- If archive contains EXE → Runs installer (current behavior)
- If direct EXE → Runs installer (current behavior)

**Keep ISOInstaller as-is:**
- Self-explanatory name
- Handles ISO files and archives containing ISO
- Can coexist with PCInstaller (user choice)

## Feasibility Assessment

### ✅ **HIGHLY FEASIBLE** - Low Risk Refactor

**Why it's feasible:**
1. Archive extraction logic already exists
2. Content detection already works
3. ISO mounting logic exists (just needs to be accessible)
4. No breaking changes to data structures
5. Backward compatible (existing mappings still work)

**Effort Estimate:** 2-3 hours

## Implementation Approach

### Option 1: Extract ISO Logic to Shared Utility (Recommended)
**Pros:**
- Clean separation of concerns
- Reusable code
- Easier to maintain

**Cons:**
- Slightly more refactoring
- Need to extract mounting logic

**Steps:**
1. Create `ISOInstallerUtility` class with mounting/installation logic
2. Update `PCInstallerInstallController` to use utility when ISO detected
3. Update `ISOInstallerInstallController` to use utility
4. Both controllers share the same ISO handling code

### Option 2: Duplicate ISO Logic in PCInstaller (Faster)
**Pros:**
- Faster to implement
- No refactoring of ISOInstaller

**Cons:**
- Code duplication
- Harder to maintain
- Two places to fix bugs

**Steps:**
1. Copy ISO mounting logic to PCInstallerInstallController
2. When archive contains ISO, mount and install directly
3. Keep ISOInstallerInstallController unchanged

### Option 3: Delegate to ISOInstaller Controller (Complex)
**Pros:**
- No code duplication
- Uses existing controller

**Cons:**
- Complex controller-to-controller communication
- Requires changing game's RomType dynamically
- More error-prone

## Recommended: Option 1

### Implementation Plan

#### Phase 1: Extract ISO Installation Logic
```csharp
// New: EmuLibrary/Util/ISOInstallerUtility.cs
public class ISOInstallerUtility
{
    public static async Task<bool> MountAndInstallAsync(
        string isoPath,
        string installDirectory,
        Game game,
        IEmuLibrary emuLibrary,
        CancellationToken cancellationToken)
    {
        // Extract mounting and installation logic here
        // Returns true if successful
    }
}
```

#### Phase 2: Update PCInstallerInstallController
```csharp
// When archive contains ISO:
if (contentInfo.HasIsoFiles && !contentInfo.HasExeFiles)
{
    // Use ISO utility to mount and install
    var isoPath = contentInfo.IsoFiles[0];
    await ISOInstallerUtility.MountAndInstallAsync(
        isoPath,
        installDir,
        Game,
        _emuLibrary,
        cancellationToken
    );
    return; // Success
}
```

#### Phase 3: Update ISOInstallerInstallController
```csharp
// Refactor to use shared utility
await ISOInstallerUtility.MountAndInstallAsync(
    sourceISOPath,
    _gameInfo.InstallDirectory,
    Game,
    _emuLibrary,
    cancellationToken
);
```

#### Phase 4: Update UI/Tooltips
- Add tooltip to RomType dropdown:
  - "PCInstaller: Handles EXE files and archives (ZIP/RAR/7Z). Automatically routes to ISO handler if archive contains ISO files."
  - "ISOInstaller: Handles ISO disc images and archives containing ISO files."

## Benefits

1. **User-Friendly**: One mapping type handles all PC game formats
2. **Smart Routing**: Automatically detects content and routes appropriately
3. **Less Confusion**: Users don't need to know what's inside archives
4. **Backward Compatible**: Existing mappings continue to work
5. **Maintainable**: Shared utility reduces code duplication

## Migration Path

**No migration needed!**
- Existing PCInstaller mappings: Continue to work, now smarter
- Existing ISOInstaller mappings: Continue to work as-is
- New users: Can use PCInstaller for everything, or ISOInstaller for ISO-specific

## Testing Checklist

- [ ] Direct EXE file installation (existing)
- [ ] Archive containing EXE (existing)
- [ ] Archive containing ISO → Auto-routes to ISO handler (NEW)
- [ ] Direct ISO file (ISOInstaller, unchanged)
- [ ] Archive containing ISO (ISOInstaller, unchanged)
- [ ] Multi-part RAR with ISO (NEW in PCInstaller)
- [ ] Multi-part RAR with EXE (existing)

## Risk Assessment

**Low Risk:**
- No data structure changes
- No breaking API changes
- Backward compatible
- Can be tested incrementally

**Potential Issues:**
- Edge case: Archive contains both ISO and EXE (use heuristics: prefer ISO)
- User might expect different behavior (documentation helps)

## Recommendation

**✅ PROCEED WITH REFACTOR**

This is a low-risk, high-value improvement that makes the plugin more user-friendly without breaking existing functionality.

