---
name: harmony-update
description: Check for and apply upstream Harmony updates by diffing decompiled NuGet against forked source
argument-hint: "[version] (default: latest)"
---

# Harmony Upstream Update Check

Compare the forked Harmony source in `Dependencies/ThirdParty/Harmony/` against a new upstream version to identify changes and apply updates.

## Steps

1. **Determine target version:**
   - If user specified a version, use that
   - Otherwise, check latest `Lib.Harmony` NuGet version via `dotnet package search Lib.Harmony`

2. **Download and decompile:**
   ```bash
   # Install/update target version to NuGet cache
   dotnet add /tmp/harmony-check/harmony-check.csproj package Lib.Harmony --version <VERSION>
   
   # Decompile the fat DLL
   # Path: ~/.nuget/packages/lib.harmony/<VERSION>/lib/net472/0Harmony.dll
   # Use ILSpy MCP to decompile to C:/tmp/harmony-upstream/
   ```

3. **Diff against fork:**
   - Compare `C:/tmp/harmony-upstream/HarmonyLib/` against `Dependencies/ThirdParty/Harmony/HarmonyLib/`
   - Focus on `HarmonyLib/` (the public API) — changes here affect TAOM patches
   - Check `HarmonySharedState.cs` for `internalVersion` changes (cross-mod compat)
   - Check `PatchInfoSerialization.cs` for format changes
   - Summarize MonoMod/Cecil/Iced changes separately (internal only)

4. **Generate change report:**
   - List all changed files with change type (added/modified/deleted)
   - Highlight breaking changes to public API
   - Flag any changes to `HarmonySharedState.internalVersion` (currently 102)
   - Flag any changes to serialization format
   - Note new features TAOM could use

5. **Apply updates (with user approval):**
   - Copy changed files from upstream to fork
   - Re-apply TAOM's customizations:
     - `PatchProcessor.cs` line ~324: assembly name check includes "TAOM.Dependencies"
     - `TaleWorlds.CampaignSystem.dll` excluded from references (Helpers namespace conflict)
   - Rebuild and test: `dotnet build Dependencies && dotnet build Main && dotnet test TAOM.Tests`

## TAOM Customizations to Preserve

| File | Change | Why |
|------|--------|-----|
| `HarmonyLib/PatchProcessor.cs` | Assembly name check includes `TAOM.Dependencies` | VersionInfo diagnostic |
| `Iced.Intel/Decoder.cs` | `(RegInfo2)(ref regInfo)` → `regInfo` | Decompilation artifact fix |
| `--z__ReadOnlyArray.cs` | Added `_items` backing field | Decompilation artifact fix |
| `--z__ReadOnlySingleElementList.cs` | Added `_item`, `_moveNextCalled` backing fields | Decompilation artifact fix |
| `MonoMod/ILHelpers.cs` | `ObjectAsRef` uses GCHandle instead of `fixed` | Decompilation artifact fix |
| `MonoMod.Backports.ILHelpers/UnsafeRaw.cs` | SkipInit, AsRef, Unbox fixes | Decompilation artifact fix |
| `MonoMod.Core.Platforms/AllocationRequest.cs` | Removed `readonly` from record struct | Decompilation artifact fix |
| `System/SpanHelpers.cs` | Unsafe pointer bypass for ref-assign patterns | Decompilation artifact fix |
| `MonoMod.Cil/ILPatternMatchingExt.cs` | Unsafe pointer bypass for ref-assign pattern | Decompilation artifact fix |
| Various Windows.cs/Runtime files | Added `unsafe` to method signatures | Decompilation artifact fix |

## Key Constraint

The `HarmonySharedState.internalVersion` must match across all Harmony instances in the AppDomain. If upstream changes this value, ALL mods need to update simultaneously. Flag this prominently in the report.
