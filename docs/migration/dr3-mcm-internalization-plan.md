# DR3 — MCM + MBOptionScreen + ButterLib Internalization Deep Dive

**Status:** Investigation complete; awaiting user sign-off on phasing.
**Date:** 2026-05-22
**Branch:** `bannerlord-1.4.5`
**Triggered by:** MCM tab missing from in-game Options screen after DR1's launcher cleanup.

---

## Question 1: Why didn't prior sessions internalize MCM?

The original `TAOM.Dependencies` (commit `35fa3be`, then archived/restored at `0b16cca`) merged in only `Harmony` + `UIExtenderEx` + `NativeSkinFixes`. MCM was deliberately left as a bundled `MCMv5.dll` in `TAOM/_Module/bin/` AND a required external module dependency. Five plausible reasons:

1. **MCMv5.dll is byte-identical** between TAOM's bundle and `Bannerlord.MBOptionScreen`'s ship (md5 match verified 2026-05-22). At authoring time the author probably reasoned "the bytes are the same, the file is already on every player's install via MBOptionScreen anyway — no point duplicating internalization work that adds zero functional value."

2. **MCM is a BUTR meta-loader, not a single DLL.** `Bannerlord.MBOptionScreen` ships 25+ versioned DLLs (`Bannerlord.MBOptionScreen.v1.0.0.dll` … `v1.3.6.dll`) and a runtime loader (`Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen.dll`) that picks the right version based on installed game version. Merging this is structurally harder than merging Harmony — there's no single "Harmony.dll equivalent" for MCM; the dispatch logic is the loader.

3. **ButterLib is the real beast** — not MCM itself. MBOptionScreen hard-deps ButterLib, which has the same meta-loader pattern (25+ versioned `Bannerlord.ButterLib.Implementation.*.dll`) plus a large supporting tree: Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging, Microsoft.Extensions.Options, Serilog + Serilog.Sinks.File + Serilog.Extensions.Logging, BUTR.CrashReport (5 DLLs), System.Collections.Immutable, System.Reflection.Metadata, etc. The Microsoft.Extensions stack alone pulls a transitive dependency tree of 8+ DLLs.

4. **Native DLLs can't be ILMerged.** `Bannerlord.ButterLib` ships `cimgui.dll` (1.2 MB) and `glfw3.dll` (227 KB) — unmanaged native code. These must remain as standalone files even after internalization; the merged TAOM.Dependencies.dll can't absorb them. That breaks the "single artifact" purity argument that motivated Harmony+UIExtender merging.

5. **Version-coupled to game API.** Each `Bannerlord.ButterLib.Implementation.1.X.Y.dll` is compiled against a specific TaleWorlds API surface. The highest installed version on the user's machine is `1.3.8` — there is no `1.4.x` build available because Bannerlord 1.4.5 was released just before this migration; BUTR hasn't shipped 1.4.5-compatible builds yet. Internalizing today bakes in stale 1.3.x implementations into TAOM.Dependencies. The risk model is "we own the staleness from now on" — every BUTR upstream update requires a manual TAOM.Dependencies re-sync.

The honest summary: **MCM internalization is structurally 5-10× more work than Harmony/UIExtenderEx internalization was**, and the original authors made a defensible call to leave it external. That call is now coming due because we discovered 1.4.5 means BUTR's modules may not work even when externally enabled.

---

## Question 2: What's the actual surface area?

### 2a. MCM module (`Bannerlord.MBOptionScreen`)

| File | Size | Role |
|---|---|---|
| `MCMv5.dll` | 520 KB | The MCM API + attributes that TAOM's `TaomSettings.cs` compiles against. Already bundled in TAOM/bin/. |
| `MCM.UI.Adapter.MCMv5.dll` | 80 KB | UI adapter — bridges MCMv5 to the actual screen rendering. |
| `Bannerlord.MBOptionScreen.v1.0.0.dll` … `v1.3.6.dll` | varies (~300 KB each) | Per-game-version implementations. Loader picks one at runtime. |
| `Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen.dll` | 1.3 MB | The meta-loader. Reads SubModule.xml `LoaderFilter` tag, picks the right `MBOptionScreen.v*.dll`. |

### 2b. ButterLib module (`Bannerlord.ButterLib`)

| File | Size | Role |
|---|---|---|
| `Bannerlord.ButterLib.dll` | 376 KB | Public API + `ImplementationLoaderSubModule` that picks the right Implementation.*.dll. |
| `Bannerlord.ButterLib.Implementation.1.1.3.dll` … `1.3.8.dll` | varies (~90 KB each) | Per-game-version impls. Same pattern as MCM. **Highest is 1.3.8 — no 1.4.x yet.** |
| `BUTR.CrashReport.dll` + `.Models.dll` + `.Renderer.{Html,ImGui,WinForms,Zip}.dll` | 5 DLLs total | Crash reporting infrastructure. Required by ButterLib's `ButterLibSubModule`. |
| `Microsoft.Bcl.HashCode.dll`, `Microsoft.Extensions.{DependencyInjection,Logging,Options,Primitives}.dll` (8 DLLs total) | NuGet | DI + logging foundation ButterLib runs on. |
| `Serilog.dll`, `Serilog.Extensions.Logging.dll`, `Serilog.Sinks.File.dll` | NuGet | ButterLib's logger backend. |
| `System.{Buffers,Collections.Immutable,Memory,Numerics.Vectors,Reflection.Metadata,Runtime.CompilerServices.Unsafe}.dll` | NuGet | .NET runtime polyfills for .NET Framework 4.7.2 target. |
| `cimgui.dll` | 1.2 MB | **NATIVE** — ImGui binding for crash report renderer. Can't be ILMerged. |
| `glfw3.dll` | 227 KB | **NATIVE** — Window/GL context for ImGui. Can't be ILMerged. |

### 2c. UIExtenderEx (already merged into TAOM.Dependencies — keeping for context)

Required external because MBOptionScreen lists `Bannerlord.UIExtenderEx v2.13.1` as a hard `DependedModule`. Our merged copy lives only inside TAOM.Dependencies.dll's assembly identity — when MBOptionScreen's loader probes for the external module ID, it doesn't find our internal one.

This means even after DR3, the launcher dependency resolution must be satisfied: either (a) we also satisfy the external module ID with a stub `Bannerlord.UIExtenderEx` module folder, or (b) we ship our own MBOptionScreen replacement that doesn't declare the external dep.

---

## Question 3: What's the actual goal of DR3?

The user's stated intent (paraphrased from earlier sessions): "TAOM.Dependencies should be a complete replacement of harmony, mcm, butterlib etc because that is what it was built for."

Three concrete interpretations of "complete replacement":

**Interpretation A — One-DLL Purity.** Every BUTR dependency ends up inside `TAOM.Dependencies.dll`. Launcher sees only `TAOM`, `TAOM.Dependencies`, and the SandBox/Native chain — no Bannerlord.* modules. This is the cleanest end-state but requires re-implementing MBOptionScreen's screen-registration logic inside TAOM.Dependencies, AND handling the native DLLs (cimgui/glfw3) as separate ship artifacts (can't be merged).

**Interpretation B — Self-Contained Module Bundle.** TAOM.Dependencies module folder ships ALL the BUTR DLLs (MCMv5 + MBOptionScreen.v*.dll + ButterLib + Microsoft.Extensions + Serilog + native ImGui) but doesn't ILMerge them. The launcher still sees only TAOM + TAOM.Dependencies. TAOM.Dependencies's SubModule.cs runtime-initializes Harmony / UIExtenderEx / ButterLib / MCM in the right order. Doesn't require source-level decompilation — just careful module-load orchestration.

**Interpretation C — Hybrid (recommended).** Keep Harmony + UIExtenderEx as source-merged into TAOM.Dependencies.dll (already done, working). Ship MCM + MBOptionScreen + ButterLib + their deps as bundled DLLs inside TAOM.Dependencies' module folder, with TAOM.Dependencies's SubModule.cs orchestrating their load. Avoid re-decompiling the meta-loader logic (we'd inherit its bugs).

**Recommendation: Interpretation C.** It's a single multi-day session of work, doesn't require maintaining a fork of BUTR source, and achieves "zero external BUTR modules required" from the player's perspective.

---

## Question 4: 1.4.5 compatibility blocker

The user's installed `Bannerlord.ButterLib` ships up to `Implementation.1.3.8` — no `1.4.x`. Same for MBOptionScreen (highest is `v1.3.6`). Bannerlord 1.4.5 just released; BUTR has not yet shipped updates.

This is a hard blocker regardless of internalization choice:

- If we ship BUTR's current modules (internalized or external), the runtime meta-loader will fall back to the closest implementation (probably 1.3.8) and may or may not work against 1.4.5 game APIs.
- If we wait for BUTR to ship 1.4.5 builds, the entire MCM stack just works (externally or internalized).
- If we own the BUTR fork, we'd need to recompile the implementation DLLs ourselves against 1.4.5 TaleWorlds DLLs.

The user should check:
1. https://github.com/BUTR/Bannerlord.ButterLib — releases tab for 1.4.5 build
2. https://github.com/Aragas/Bannerlord.MCM — releases for 1.4.5
3. Discord (BUTR Discord) for ETA on 1.4.5 builds

Until BUTR ships 1.4.5 versions, **internalizing 1.3.x DLLs is just baking the staleness in deeper**. The cheaper near-term move may be to:

a. Confirm BUTR's 1.4.5 ETA (or test if 1.3.8 actually works on 1.4.5 — meta-loader's fallback might be fine).
b. Defer DR3 until BUTR 1.4.5 ships, OR
c. Accept "no MCM tab" as a degradation for the duration of smoke testing and ship MCM internalization as a post-migration follow-up.

---

## Question 5: Phasing recommendation

If we commit to DR3 now, the sequence:

### Phase DR3a — Stage external modules into Dependencies project
- Copy `MCMv5.dll`, `MCM.UI.Adapter.MCMv5.dll`, all `Bannerlord.MBOptionScreen.v*.dll`, `Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen.dll` into `Dependencies/_Module/bin/Win64_Shipping_Client/`.
- Copy all of ButterLib's DLLs (~30 files) into same location.
- Mark them as `<Content>` in `Dependencies/TAOM.Dependencies.csproj` so they ship in the module bin/ folder.

### Phase DR3b — Update TAOM.Dependencies SubModule.xml
- Add `<SubModule>` entries for MCMv5, MBOptionScreen Module Loader, ButterLib, ButterLib Implementation Loader.
- Order matters: Harmony (already merged into TAOM.Dependencies.dll) must initialize first, then ButterLib, then UIExtenderEx (also merged), then MCM.
- The merged-vs-bundled split needs TAOM.Dependencies.SubModule.cs to handle Harmony+UIExtenderEx initialization in OnSubModuleLoad before the bundled SubModules' classes run.

### Phase DR3c — Add `Bannerlord.UIExtenderEx` and `Bannerlord.Harmony` shim modules
- The launcher checks `<DependedModule Id="Bannerlord.Harmony">` declarations. ButterLib + MBOptionScreen both declare this.
- Options:
  i. Patch each bundled MBOptionScreen.v*.dll's SubModule.xml at deploy time to drop those `<DependedModule>` entries.
  ii. Ship empty stub modules `Bannerlord.Harmony` and `Bannerlord.UIExtenderEx` that satisfy the launcher dep check.
  iii. Have TAOM.Dependencies declare those module IDs as well via `<Id>` alias (not supported by BUTR's launcher).

Option (ii) is the safest — TAOM ships 2 extra tiny module folders containing just a SubModule.xml that re-exports nothing.

### Phase DR3d — Native DLL deployment
- `cimgui.dll` + `glfw3.dll` ship in TAOM.Dependencies's `bin/Win64_Shipping_Client/` (they're already DLLs; just copy).

### Phase DR3e — Test
- Launch Bannerlord with only TAOM + TAOM.Dependencies enabled (no external BUTR modules).
- Confirm MCM tab renders in Options.
- Confirm TAOM settings (TaomSettings, BattleBalance, etc.) appear in MCM.
- Confirm Career system, EquipPresets, etc. still work (they use UIExtenderEx through TAOM.Dependencies' merged copy — must not conflict with MCM's external UIExtenderEx demand which we'll satisfy via shim).

### Phase DR3f — Verify no double-load
- Open `mb.log` and confirm no "Type already registered" or "duplicate assembly" warnings.
- Run a save/load round trip.
- Verify HarmonySharedState shows expected patch counts (no doubles).

**Estimated effort: 1-2 full sessions if BUTR 1.4.5 builds exist, 3+ if we have to recompile their implementation DLLs ourselves.**

---

## Empirical findings — decompile-based source-merge attempt (2026-05-22)

Attempted full decompile of MCMv5.dll into `Dependencies/ThirdParty/MCM/` as a viability test. Results:

| Step | Outcome |
|---|---|
| Decompile MCMv5.dll via ilspycmd | 340 C# files, ~20K LOC. Includes ILRepack'd internals: BUTR.DependencyInjection, HarmonyLib.BUTR.Extensions, Bannerlord.ModuleManager, MCM.LightInject (its own DI impl), Bannerlord.BUTR.Shared |
| Identify cross-tree collisions | 3 files (DictionaryExtensions, WrappedMethodInfo, WrappedPropertyInfo) collide with UIExtenderEx's existing source-merge. Deleted from MCM tree. |
| First build attempt | **64 errors, all `CS1525: Invalid expression term 'ref'`** — ilspy emitted `((Type)(ref var))` casts for value-type struct method calls it couldn't fully resolve (ApplicationVersion, PlatformDirectoryPath, PlatformFilePath). |
| Mechanical fix: regex-replace `((Type)(ref var))` → `var` across all MCM files | Resolved the 64 errors. |
| Second build attempt | **826 errors** of 7 categories: CS0111 (424, "already defines a member"), CS0121 (154, ambiguous call), CS0246 (112, type not found), CS0579 (92, duplicate attribute), CS0101 (32, namespace duplicate), CS0102 (8, duplicate member), CS0260 (4, missing partial). |

The CS0111 errors are the killer: ilspy decompiled IL where ANY constructor/method had subtle differences that ilspy couldn't disambiguate, and emitted **multiple definitions of the same signature**. Each one is a per-site manual rewrite — there is no mechanical fix.

**Conclusion:** Decompile-based source-merge is not viable at this scale. The decompiled output has too many decompile artifacts requiring per-site manual rewrites.

## The actually-right path (multi-week effort)

Pull the BUTR libraries' source code from their official GitHub repos:
- https://github.com/BUTR/Bannerlord.MCM (MCMv5 source)
- https://github.com/BUTR/Bannerlord.ButterLib (ButterLib source)
- https://github.com/BUTR/Bannerlord.UIExtenderEx (UIExtenderEx upstream — for compatibility verification)

This source compiles cleanly (it's the canonical upstream). Steps:

1. **Per-library**: Clone the source at a specific commit/tag matching the Nexus DLL version (e.g., MCM v5.11.4)
2. Put cleaned source into `Dependencies/ThirdParty/<Library>/`
3. Identify BUTR.Shared/HarmonyLib.BUTR.Extensions duplicates across libraries; pick canonical version + delete others
4. Update Dependencies.csproj with PackageReferences for any non-source deps (Microsoft.Extensions.*, Serilog, etc.)
5. Build → fix any remaining (real) errors
6. Per-library acceptance check: types compile, no missing references
7. Add SubModule entries to Dependencies/_Module/SubModule.xml
8. Per-library code review pass: identify and document quality issues, edge-case bugs, 1.4.5-incompatible patterns
9. Integration test in-game

**Realistic per-library effort: 3-8 hours.** With 5 libraries (MCM, MBOptionScreen, MCM.UI.Adapter, ButterLib, ButterLib.Implementation) plus BUTR.CrashReport: **20-50 hours total**. Spread across multiple focused sessions.

Code review/improvement phase per user direction adds another 10-20 hours.

## Out-of-scope work that surfaced during this investigation

Items I noticed but did not action; future-DR3 sessions should consider:
- MCM ships its own DI implementation (`MCM.LightInject`) — 6,590 LOC. Replaceable with Microsoft.Extensions.DependencyInjection if we want to consolidate DI across MCM + ButterLib.
- ButterLib's `Bannerlord.ButterLib.Implementation.*.dll` versioned-impls pattern: we'd flatten to just 1.4.1 (the latest matching 1.4.5).
- BUTR.CrashReport's ImGui renderer requires `cimgui.dll` + `glfw3.dll` (native). If we skip the ImGui renderer, no native DLLs needed.
- Reflection-driven Settings discovery in MCM has performance overhead at game start — could be replaced with source generators if we own the source.

## Recommendation

Two equally-valid paths:

| Option | Best for | Effort |
|---|---|---|
| **Defer DR3 until BUTR ships 1.4.5** | Avoiding stale-fork ownership cost; lets the existing tested ecosystem do its job. Re-enable external modules in launcher for now. | Low (5 min: re-add `DependedModule` entries to TAOM SubModule.xml, enable modules in launcher) |
| **Execute DR3c (Interpretation C) now** | "Zero external modules" purity; complete control over the BUTR stack. | High (1-2+ sessions). Will inherit 1.3.8/1.3.6 staleness until we resync with BUTR's 1.4.5 release anyway. |

**My recommendation: defer DR3 until BUTR ships 1.4.5 builds.** Internalizing 1.3.x DLLs costs us a day of work and we'll redo it the moment BUTR ships 1.4.5. The interim state — TAOM.Dependencies provides Harmony+UIExtenderEx merged, external Bannerlord.ButterLib + Bannerlord.MBOptionScreen enabled in launcher — is functionally equivalent to the original TAOM design pre-1.4.5.

To unblock the immediate MCM-missing-tab issue: re-add the 4 BUTR modules as required deps in TAOM's SubModule.xml so the launcher enforces them. User enables them in the launcher. Done in 5 minutes.

DR3 stays on the migration board as a follow-up to be executed after BUTR ships 1.4.5 builds.

---

## Cross-references

- `docs/migration/dual-dll-setup.md` — DLL acquisition + swap procedure (relevant to DR3 native DLL handling)
- `docs/adrs/` — pending new ADR documenting DR3 phasing decision
- `memory/harmony-fork-research.md` — confirms HarmonySharedState handles multi-assembly Harmony coexistence (which is why DR1's merge works alongside external Bannerlord.Harmony when re-enabled)
