# DR3 Source-Merge — Execution Handoff (2026-05-22)

**Status:** Foundation laid. Multi-session project. **Build is currently clean (0 errors)** — repo is in stable state. All DR3 attempts from this session have been reverted; vendor source is downloaded (gitignored) and waiting for next session.

## What this document captures

The empirical findings from ~9 hours of source-merge investigation today, so next session(s) can pick up without re-learning what doesn't work.

## Final architecture decision (locked)

**User direction (confirmed twice):** everything source-merged into ONE TAOM.Dependencies.dll. No separate runtime DLLs for Harmony, UIExtenderEx, MCM, ButterLib, BUTR.Shared. Only native DLLs (cimgui, glfw3) ship as separate files because unmanaged code can't be ILMerged.

## What's vendored (gitignored, fresh each session)

All upstream source downloaded to `Dependencies/.vendor-source/` at these specific tags:

| Library | Tag | Size | URL |
|---|---|---|---|
| pardeike/Harmony | v2.4.2.0 | 7.3 MB | https://github.com/pardeike/Harmony/archive/refs/tags/v2.4.2.0.tar.gz |
| MonoMod/MonoMod | master | 1.3 MB | https://github.com/MonoMod/MonoMod/archive/refs/heads/master.tar.gz (no v25+ tags exist) |
| jbevain/cecil | 0.11.5 | 58 MB | https://github.com/jbevain/cecil/archive/refs/tags/0.11.5.tar.gz |
| BUTR/Bannerlord.UIExtenderEx | v2.13.2 | 938 KB | https://github.com/BUTR/Bannerlord.UIExtenderEx/archive/refs/tags/v2.13.2.tar.gz |
| Aragas/Bannerlord.MBOptionScreen | v5.11.4 | 3.1 MB | https://github.com/Aragas/Bannerlord.MBOptionScreen/archive/refs/tags/v5.11.4.tar.gz |
| BUTR/Bannerlord.ButterLib | v2.10.4 | 172 KB | https://github.com/BUTR/Bannerlord.ButterLib/archive/refs/tags/v2.10.4.tar.gz |

**Not downloaded yet** (decide in next session):
- icedland/iced — disassembler used by MonoMod. Possibly skippable for Bannerlord use case.

## Empirical findings — what tries fail

### Attempt 1: Decompile-based source-merge (failed at 826 errors)

Used ilspycmd to decompile MCMv5.dll → ThirdParty/MCM/. Got 826 errors including 424 CS0111 ("already defines a member") that are unfixable mechanically because ilspy can't disambiguate IL constructor overloads. **Discarded.**

### Attempt 2: Upstream source + source-only NuGet (failed at 190 errors)

Downloaded MCM/ButterLib upstream source. Added source-only NuGet packages (Bannerlord.BUTR.Shared, LightInject.Source, BUTR.DependencyInjection, Microsoft.Extensions.*, Serilog, Required polyfill).

Iteration path: 358 → 122 → 18 → 358 → 122 → 26 → 14 → 0 → 95 (Roslyn ICE) → 64 (Roslyn ICE) → 133 → 190.

**Why it failed:**
1. `Bannerlord.BUTR.Shared` NuGet's `Helpers` namespace shadows our `Dependencies/ThirdParty/Harmony/MonoMod.Utils.Helpers` class throughout 706+ call sites (`Helpers.ThrowIfNull(...)` no longer resolves).
2. Our existing Harmony source-merge is a DECOMPILE with ILRepack-encoded internal type names (`<1c2fb156-...>MaybeNullWhenAttribute`). When we add the canonical polyfill NuGets (Nullable, IsExternalInit, Required), the decompile's internal references become dangling.
3. Roslyn ICE (`LocalRewriter_Conversion.cs:684/698` "thought to be unreachable") triggers on certain conversions in the combined source on both .NET 9 and .NET 10 SDKs.

**Conclusion:** The Harmony source-merge IS the blocker. It's a lossy decompile and fights every cleanup attempt.

## The locked plan — next session(s)

### Phase A: Replace Harmony source-merge with upstream (CRITICAL — this is what unblocks everything)

1. Delete `Dependencies/ThirdParty/Harmony/` entirely.
2. Replace with clean source from:
   - `Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/` → `Dependencies/ThirdParty/Harmony/HarmonyLib/`
   - `Dependencies/.vendor-source/MonoMod-master/src/MonoMod.Core/` → `Dependencies/ThirdParty/Harmony/MonoMod.Core/`
   - `Dependencies/.vendor-source/MonoMod-master/src/MonoMod.Utils/` → `Dependencies/ThirdParty/Harmony/MonoMod.Utils/`
   - `Dependencies/.vendor-source/MonoMod-master/src/MonoMod.Backports/` → `Dependencies/ThirdParty/Harmony/MonoMod.Backports/`
   - `Dependencies/.vendor-source/cecil-0.11.5/` (relevant subdirs only — Cecil is 58MB extracted; will need to identify only the files HarmonyLib needs)
3. Iced library — research whether it's needed for Bannerlord HarmonyLib's use case OR can be replaced with simpler disassembly.

### Phase B: Replace UIExtenderEx source-merge with upstream

Delete `Dependencies/ThirdParty/UIExtenderEx/` and replace with clean upstream from `Dependencies/.vendor-source/Bannerlord.UIExtenderEx-2.13.2/src/`.

### Phase C: Add MCM + ButterLib + MBOptionScreen source

Copy from `Dependencies/.vendor-source/Bannerlord.MBOptionScreen-5.11.4/src/` and `Dependencies/.vendor-source/Bannerlord.ButterLib-2.10.4/src/`.

### Phase D: Configure Dependencies.csproj

Required PackageReferences (source-only NuGets that source-include into TAOM.Dependencies.dll):
- `Bannerlord.BUTR.Shared` v3.0.0.142
- `Bannerlord.ModuleManager.Source` v5.0.221
- `BUTR.DependencyInjection` v2.0.0.52
- `BUTR.DependencyInjection.ButterLib` v2.0.0.52
- `BUTR.MessageBoxPInvoke` v1.0.0.1
- `LightInject.Source` v6.6.4 (note: SDK csproj doesn't process .cs.pp; need manual extract with `$rootnamespace$` → `MCM` substitution)
- `Required` v1.0.0 (polyfill for C# 11 `required` keyword on net472)
- `IsExternalInit` v1.0.3
- `Nullable` v1.3.1
- `Newtonsoft.Json` v13.0.1 (compile-only)
- `Microsoft.Extensions.DependencyInjection` v3.1.32 (with .Abstractions)
- `Microsoft.Extensions.Logging` v3.1.32 (with .Abstractions)
- `Microsoft.Extensions.Options` v3.1.32
- `Serilog` v2.12.0 (with .Extensions.Logging, .Sinks.File)

Required DefineConstants:
- `BANNERLORDMCM_PUBLIC` (make MCM types public)
- `BANNERLORDMCM_NOT_SOURCE` (distinguishes compiled-DLL vs source-NuGet)
- `BUTRDEPENDENCYINJECTION_PUBLIC` (make BUTR.DI types public)
- `BUTTERLIB` (ButterLib code paths)
- `v141` (game-version conditional compile — closest to 1.4.5)

BCL References needed:
- `System.Windows.Forms`
- `System.IO.Compression`
- `System.IO.Compression.FileSystem`

Source-deletion needed (BUTR libs ship features we don't ship):
- All `ExceptionHandler/` and `CrashUploader/` files in ButterLib (skip crash reporting)
- `ImplementationLoaderSubModule.cs` (uses System.Reflection.Metadata for runtime DLL loading; we flatten to one version)
- `LoggerTraceListener.cs` (uses C# 11 required keyword — we already polyfill that, but the class also uses other modern features)
- ButterLib's `ImplementationLoaderSubModule`-referencing code in `ButterLibSubModule.cs`

### Phase E: Wire SubModule.xml

`Dependencies/_Module/SubModule.xml` should declare these SubModule entries (all DLLName="TAOM.Dependencies.dll"):
1. `TAOM.Dependencies.SubModule` (existing — keep)
2. `Bannerlord.Harmony.SubModule` (Harmony bootstrap)
3. `Bannerlord.UIExtenderEx.SubModule` (UIExtenderEx bootstrap)
4. `Bannerlord.ButterLib.ButterLibSubModule`
5. `MCM.MCMSubModule`
6. `MCM.Internal.MCMImplementationSubModule`
7. `MCM.UI.MCMUISubModule` (the MBOptionScreen UI)
8. `MCM.UI.MCMUIAdapterSubModule`

### Phase F: Native DLL handling

`cimgui.dll` + `glfw3.dll` ship as files in `Dependencies/_Module/bin/Win64_Shipping_Client/`. They're required by `BUTR.CrashReport.Renderer.ImGui` IF we include crash reporting. **Recommend: skip ImGui crash renderer** (use WinForms only via `System.Windows.Forms`) and drop the native DLLs entirely.

### Phase G: Iterate compile errors

Common patterns we've already seen:
- `Helpers.X(...)` namespace shadowing → fully-qualify via sed
- ILRepack-encoded internal references in decompiled Harmony source → resolved by replacing with upstream
- `protected override` vs `protected internal override` access modifier mismatches → widen to `protected internal`
- Duplicate polyfill files between source-merge and NuGets → delete the source-merge copies
- Roslyn ICE on certain conversions → bisect specific source files; may need to skip or annotate

### Phase H: Tests

`dotnet test TAOM.Tests` must remain green.

### Phase I: Deploy + verify

- Bannerlord exits → deploy → relaunch
- Confirm: only TAOM + TAOM.Dependencies enabled in launcher (no external BUTR modules)
- Confirm: MCM tab appears in Options screen
- Confirm: TAOM settings appear under MCM tab
- Confirm: campaign loads, no `mb.log` errors related to dependency loading

### Phase J: /deep-review

Multi-agent review of:
- Source-merge architectural decisions
- Skipped features (crash reporter, ImGui renderer) — document deliberate exclusions
- BUTR fork ownership going forward — who updates when BUTR ships new versions

### Phase K: /codex:review

Codex independent review of the architectural changes.

## What this session DID accomplish

Preserved on `bannerlord-1.4.5`:
- DR1: TAOM.csproj → TAOM.Dependencies wiring (2a0eeba) — currently still depends on the existing Harmony decompile-based source-merge in TAOM.Dependencies.dll. Once Harmony is replaced with upstream, DR1 stays as-is (just the underlying source changes).
- UI: 80+ prefab sites flipped (9ee2b2a + ad836d1) — verified in-game working
- Alliance: Harad permanent + MakePeace diagnostic (a0a63c8 + 97f564d) — pending verification on next campaign load
- DR3 plan + empirical findings (c4d0da5 + 86cb894 + this doc)

Repo build state is clean: `dotnet build Main/TAOM.csproj` returns 0 errors.

## What NEXT session should do

Estimated 6-15 hours of focused work to complete Phases A-K. Realistically 2-4 dedicated sessions.

**Session 1 next:** Phase A (replace Harmony source-merge with upstream).
**Session 2 next:** Phase B + C + D (UIExtenderEx + MCM + ButterLib + csproj).
**Session 3 next:** Phase E + F + G (SubModule + native + iterate errors).
**Session 4 next:** Phase H + I + J + K (test + deploy + reviews).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
