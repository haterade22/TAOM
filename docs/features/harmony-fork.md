# Harmony 2.4.2 Fork

## Overview

Harmony 2.4.2 source (including MonoMod.Core, MonoMod.Utils, Mono.Cecil, and Iced.Intel) is compiled directly into `TAOM.Dependencies.dll`. This eliminates the external `Bannerlord.Harmony` module dependency, making TAOM fully self-contained with zero external module requirements.

## Why This Exists

- **Vanilla behavior:** Bannerlord mods depend on `Bannerlord.Harmony` (a BUTR-maintained module) for Harmony patching
- **TAOM requirement:** Full independence from external module dependencies for reliable deployment and version control
- **Without this feature:** Users must install `Bannerlord.Harmony` separately, and BUTR version updates can break TAOM without warning

## Architecture

### Design Challenge

Harmony is a fat merged DLL (48K LOC, 1,392 files from ILRepack). Cross-mod patch sharing relies on `HarmonySharedState`, a dynamic type created at runtime using Mono.Cecil's `ModuleDefinition.CreateModule`. The assembly identity of the Harmony DLL must not break this shared state mechanism.

### Solution Approach

Decompile the fat `0Harmony.dll` via ILSpy and compile the source directly into `TAOM.Dependencies.dll`. The `HarmonySharedState` mechanism is assembly-neutral (uses `Type.GetType("HarmonySharedState")` on Mono, which searches all loaded assemblies by type name). Three safety features are added to protect the patch ecosystem.

### Component Diagram

```
TAOM.Dependencies.dll
    |-- SubModule.cs (entry point: UIExtender init + Harmony guards)
    |-- ThirdParty/Harmony/ (1,392 vendored files)
    |       |-- HarmonyLib/ (public API: [HarmonyPatch], AccessTools, etc.)
    |       |-- MonoMod.Core/ (detour factory)
    |       |-- MonoMod.Utils/ (reflection utilities)
    |       |-- Mono.Cecil/ (IL read/write)
    |       |-- Iced.Intel/ (x86 disassembler)
    |       +-- System.*/MonoMod.Backports/ (BCL polyfills)
    +-- ThirdParty/UIExtenderEx/ (forked UI extension system)
```

## Configuration

No runtime configuration. The fork is compile-time only.

### Build Configuration: `Dependencies/TAOM.Dependencies.csproj`

| Setting | Value | Why |
|---------|-------|-----|
| `LangVersion` | `preview` | Decompiled source uses C# 12+ features |
| `AllowUnsafeBlocks` | `true` | MonoMod/Iced use unsafe pointer arithmetic |
| `TaleWorlds.CampaignSystem.dll` | Excluded from references | Its `Helpers` namespace shadows `MonoMod.Utils.Helpers` class |
| `Harmony.Extensions` NuGet | Retained (source-included) | UIExtenderEx needs `AccessTools2` |
| `Properties/AssemblyInfo.cs` | Excluded from compile | Conflicts with TAOM.Dependencies assembly identity |

## Key Files

| File | Purpose |
|------|---------|
| `Dependencies/SubModule.cs` | Entry point: UIExtender init, UnpatchAll guard, duplicate detection |
| `Dependencies/TAOM.Dependencies.csproj` | Build config: source glob, NuGet removals, embedded resources |
| `Dependencies/_Module/SubModule.xml` | Module metadata: no external dependencies, loads before Native |
| `Dependencies/ThirdParty/Harmony/` | 1,392 vendored Harmony source files |
| `Dependencies/ThirdParty/Harmony/HarmonyLib/PatchProcessor.cs` | One-line fix: assembly name check includes TAOM.Dependencies |
| `.claude/skills/harmony-update/skill.md` | Upstream merge automation skill |

## Dependencies

- .NET Framework 4.7.2 (Bannerlord runtime)
- `Harmony.Extensions` NuGet v3.2.0.77 (source-included, for `AccessTools2`)
- `BUTR.Harmony.Analyzer` NuGet v1.0.1.50 (Roslyn analyzer, compile-time only)

## Safety Features

### 1. UnpatchAll(null) Guard

Harmony prefix on `Harmony.UnpatchAll` blocks calls with `harmonyID == null`, which would wipe ALL patches across all mods in the AppDomain. Logs a warning via `FileLog.Log`.

### 2. Duplicate Harmony Detection

Scans `AppDomain.CurrentDomain.GetAssemblies()` for any assembly named `0Harmony` (separate from TAOM.Dependencies). Logs a warning if found.

### 3. HarmonySharedState Compatibility

`HarmonySharedState.internalVersion` remains at 102. `PatchInfoSerialization` binary format is unchanged. Other mods' Harmony instances share patch state correctly because the dynamic type lookup is assembly-neutral.

## Tests

No unit tests for the safety features (they require a live Bannerlord runtime with Harmony patching). The existing 1,055 tests verify that all 61 TAOM Harmony patches compile correctly against the forked types.

## How-To

### How to update Harmony upstream

Run `/harmony-update [version]`. The skill:
1. Downloads and decompiles the target version
2. Diffs against `Dependencies/ThirdParty/Harmony/`
3. Generates a change report
4. Applies patches with review

### TAOM customizations to preserve during updates

| File | Change |
|------|--------|
| `HarmonyLib/PatchProcessor.cs` | Assembly name check includes `TAOM.Dependencies` |
| `Iced.Intel/Decoder.cs` | Decompilation fix: `(RegInfo2)(ref regInfo)` to `regInfo` |
| `--z__ReadOnlyArray.cs` | Added `_items` backing field |
| `--z__ReadOnlySingleElementList.cs` | Added `_item`, `_moveNextCalled` backing fields |
| `MonoMod/ILHelpers.cs` | `ObjectAsRef` uses GCHandle instead of `fixed` |
| `MonoMod.Backports.ILHelpers/UnsafeRaw.cs` | SkipInit, AsRef, Unbox decompilation fixes |
| `MonoMod.Core.Platforms/AllocationRequest.cs` | Removed `readonly` from record struct |
| `System/SpanHelpers.cs` | Unsafe pointer bypass for ref-assign patterns |
| `MonoMod.Cil/ILPatternMatchingExt.cs` | Unsafe pointer bypass for ref-assign pattern |
| Various Windows.cs/Runtime files | Added `unsafe` to method signatures |
| `MonoMod.Utils/ReflectionHelper.cs` | Fixed local function scoping (closing brace) |
| `MonoMod.Core.Platforms/PlatformTriple.cs` | Fixed IntPtr null-coalescing |
| `MonoMod.Utils/DynamicReferenceManager.cs` | Fixed ulong-to-nuint switch cases |
| `-PrivateImplementationDetails-.cs` | Changed struct visibility from private to internal |
