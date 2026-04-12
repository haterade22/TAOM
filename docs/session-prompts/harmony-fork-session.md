# Session Prompt: Fork Harmony 2.4.2 into TAOM.Dependencies

## Objective

Fork Harmony 2.4.2 (including its bundled MonoMod, Mono.Cecil, and Iced.Intel dependencies) into the TAOM.Dependencies project. This eliminates the last external BUTR module dependency — after this, TAOM ships fully self-contained with zero external module requirements.

## Prior Research (completed — see memory)

Read `harmony-fork-research.md` in memory. Key findings:

- **Feasible:** HarmonySharedState uses a dynamic type discovered by `Type.GetType("HarmonySharedState")` with `byte[]` serialized patch data. A renamed assembly still shares state with other mods' Harmony instances.
- **Size:** ~48K LOC, 1,392 decompiled files. All managed C# on Windows (native .so/.dylib are Linux/macOS only).
- **License:** MIT for all components.
- **TAOM uses:** 61 patch classes, conservative API surface (prefix/postfix/transpiler, AccessTools, PatchCategory).
- **Harmony.Extensions NuGet (3.2.0.77):** Source-included. TAOM core has 0 usage, but the forked UIExtenderEx uses AccessTools2 from it. Keep it.
- **Bannerlord runtime:** Mono (not CoreCLR), .NET Framework 4.7.2. `FxCLR4Runtime` is the active detour path.

## What Needs to Happen

### Phase 1: Decompile and Copy

1. The fat `0Harmony.dll` is already decompiled at `C:/tmp/harmony-decompiled/` (1,392 files from this session). Verify it's still there, or re-decompile from `C:/Users/mikew/.nuget/packages/lib.harmony/2.4.2/lib/net472/0Harmony.dll`.

2. Copy into `Dependencies/ThirdParty/Harmony/` with a clean folder structure:
   ```
   Dependencies/ThirdParty/Harmony/
   ├── HarmonyLib/           (~90 files — the public/internal Harmony API)
   ├── MonoMod.Core/         (~50 files — detour factory)
   ├── MonoMod.Utils/        (~100 files — DynamicMethodDefinition, reflection)
   ├── MonoMod.Cil/          (~10 files — IL helpers)
   ├── MonoMod.Backports/    (~20 files — polyfills)
   ├── Mono.Cecil/           (~400 files — IL read/write)
   ├── Iced.Intel/           (~600 files — x86 disassembler)
   └── Microsoft.Cci.Pdb/    (~20 files — PDB support)
   ```

3. Do NOT copy: native .so/.dylib files (Linux/macOS only), test files, unused framework targets.

### Phase 2: Build Integration

1. Add all Harmony source files to `Dependencies/TAOM.Dependencies.csproj`.
2. Remove the `Lib.Harmony` NuGet PackageReference from both `Dependencies/TAOM.Dependencies.csproj` and `Main/TAOM.csproj`.
3. The `Harmony.Extensions` NuGet (source-included) may still be needed by the UIExtenderEx fork — check if it compiles against our forked Harmony types. If not, inline those files too.
4. Set the assembly name to `TAOM.Harmony` (or keep as `0Harmony` if assembly identity matters for HarmonySharedState — research this first).

### Phase 3: Assembly Identity Decision (CRITICAL)

**Research question:** Does `HarmonySharedState.GetOrCreateSharedStateType()` at line 74 use `Type.GetType("HarmonySharedState")` which searches ALL loaded assemblies by type name only? Or does it use assembly-qualified names?

From the decompiled source (confirmed this session): it uses `Type.GetType("HarmonySharedState", throwOnError: false)` — this searches by **simple type name** across all loaded assemblies. Assembly identity doesn't matter.

**BUT:** The `PatchInfoSerialization` format must match. If it serializes type references with assembly-qualified names, a renamed assembly would break deserialization. Check `PatchInfoSerialization.Serialize/Deserialize` methods.

**Decision tree:**
- If serialization is assembly-neutral → rename to `TAOM.Harmony.dll` (clean isolation)
- If serialization includes assembly identity → keep as `0Harmony.dll` (binary drop-in)

### Phase 4: Safety Features

Replicate these 3 features from Bannerlord.Harmony's SubModule.cs (~50 LOC):

1. **UnpatchAll() guard:** Harmony prefix that blocks `UnpatchAll(null)` — prevents rogue mods from wiping all patches globally.
2. **Version validation:** Log a warning if another `0Harmony.dll` is detected with a different version.
3. **Load-order check:** Verify TAOM.Dependencies loads before Native.

Add these to `Dependencies/SubModule.cs`.

### Phase 5: Remove External Dependency

1. Remove `Bannerlord.Harmony` from TAOM's module dependencies (already not in SubModule.xml — verify).
2. Update `launchSettings.json` to remove `*Bannerlord.Harmony*` from the module list (TAOM.Dependencies now provides Harmony).
3. Update `Dependencies/_Module/SubModule.xml` — remove `<DependedModule Id="Bannerlord.Harmony" />` since we ARE Harmony now.

### Phase 6: ILSpy Decompilation Cleanup

The decompiled output will have artifacts:
- Compiler-generated class names (`<>c__DisplayClass12_0`, `<GetPatchInfo>d__5`)
- `[CompilerGenerated]` attributes everywhere
- Decompiler comments (`//IL_0005: Unknown result type`)
- Nullable annotation mismatches

Strategy: Leave most artifacts as-is (they compile fine). Only fix actual compile errors. This is third-party code — we maintain it, not beautify it.

### Phase 7: Verify

1. `dotnet build Dependencies` — 0 errors
2. `dotnet build Main` — 0 errors (TAOM compiles against forked Harmony)
3. `dotnet test TAOM.Tests` — 1055/1055 pass
4. In-game: load a save, verify all 61 Harmony patches apply
5. In-game: verify settlement nameplates still render (UIExtenderEx fork still works)
6. In-game: verify with another mod that uses Harmony (shared state test)

### Phase 8: Build `/harmony-update` Skill

Create a skill that automates future upstream merges:
1. Download latest Harmony NuGet
2. Decompile to temp folder
3. Diff against `Dependencies/ThirdParty/Harmony/`
4. Generate change report
5. Apply patches with review

## Key Constraints

- **Do NOT modify Harmony's public API** — all TAOM patches use standard `[HarmonyPatch]` attributes that must keep working.
- **Keep `HarmonySharedState` format version at 102** — this is the cross-mod compatibility protocol.
- **Keep `PatchInfoSerialization` format unchanged** — other mods must be able to read our patch entries.
- **Harmony.Extensions source files** are already compiled into TAOM via NuGet. If they reference `HarmonyLib` types, they'll resolve against our fork. Verify this works.

## Files to Reference

| File | What It Contains |
|------|-----------------|
| `C:/tmp/harmony-decompiled/` | Full decompiled 0Harmony.dll (1,392 files) |
| `C:/Users/mikew/.nuget/packages/lib.harmony/2.4.2/lib/net472/0Harmony.dll` | The fat merged DLL to decompile |
| `E:/Steam/.../Modules/Bannerlord.Harmony/bin/Win64_Shipping_Client/` | The thin shipped DLLs (for reference) |
| `Dependencies/SubModule.cs` | Where safety features go |
| `Dependencies/TAOM.Dependencies.csproj` | Where Harmony source gets added |
| `C:/tmp/harmony-decompiled/HarmonyLib/HarmonySharedState.cs` | The critical shared state mechanism |
| `C:/tmp/harmony-decompiled/HarmonyLib/PatchInfoSerialization.cs` | Serialization format to preserve |
