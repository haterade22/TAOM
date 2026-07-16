# DR3 — Dependency Maintenance Guide

**When to use:** Bannerlord ships a new minor/patch version. BUTR releases new versions of Harmony, UIExtenderEx, ButterLib, MCM, or MBOptionScreen. Microsoft.Extensions or Serilog needs a security update. A user reports a third-party mod is greyed out / silently breaking.

This guide explains exactly which files to update, how to verify changes, and how to read the defensive infrastructure's diagnostic output.

## Architecture summary

`TAOM.Dependencies` module bundles the entire BUTR + supporting stack PLUS 4 stub alias modules PLUS a runtime defensive shield layer. **End-user impact: the launcher needs ONLY `TAOM` + `TAOM.Dependencies` + the 4 auto-ticked stubs** — no external `Bannerlord.Harmony` / `.ButterLib` / `.UIExtenderEx` / `.MBOptionScreen` modules required, and third-party mods that depend on those standard IDs are toggleable + runtime-error-tolerant.

The dependencies fall into three categories:

### Category 1 — NuGet packages (deploy automatically on build)

These are pulled via `<PackageReference>` in `Dependencies/TAOM.Dependencies.csproj`.

> **This table deliberately carries NO version numbers.** `Dependencies/TAOM.Dependencies.csproj` is the single source of truth for every pin — read it there. A doc that restates a pin is a drift site: this table lagged the csproj for months (caught 2026-05), its stub list drifted again in June ([`plans/_audit/2026-06-12-harvest.md`](../../plans/_audit/2026-06-12-harvest.md) DEPS-05/06), and the whole stack silently rotted through the 1.4.6 **and** 1.4.7 engine bumps before the [2026-07-15 audit](dependency-audit-2026-07-15.md) caught it. The coupling invariants are now enforced by [`BundledDependencyManifestTests`](../../TAOM.Tests/Infrastructure/Dependencies/BundledDependencyManifestTests.cs) — a stale pin fails the build, not a code review.

| Package | What it provides |
|---|---|
| `Lib.Harmony` | `0Harmony.dll` (Harmony 2.x + MonoMod + Cecil + Iced ILRepack'd) |
| `Bannerlord.UIExtenderEx` | `Bannerlord.UIExtenderEx.dll` |
| `Bannerlord.MCM` | `MCMv5.dll` (the MCM API — settings attributes, base classes) |
| `System.Runtime.CompilerServices.Unsafe` | `System.Runtime.CompilerServices.Unsafe.dll` (Harmony dep) |
| `Harmony.Extensions` | Source-only extension methods |
| `BUTR.Harmony.Analyzer` | Roslyn analyzer (compile-time only) |
| `Bannerlord.BuildResources` | MSBuild tasks for module deployment |

**Update procedure:**
1. Open `Dependencies/TAOM.Dependencies.csproj` in an editor.
2. Bump the `Version=` attribute of the relevant `<PackageReference>`.
3. **Keep `Main/TAOM.csproj` in lockstep** — it pins the same three packages with `IncludeAssets="compile"`. Compile-time (Main) and runtime (Dependencies) MUST match or you risk `MissingMethodException` on APIs only in the newer build.
4. **If the bump crosses a MINOR** (e.g. 2.4.x → 2.5.x, 5.11.x → 5.12.x): ALSO bump the matching stub `<Version>` to the new minor's `.99.0` in `Stubs/Bannerlord.Harmony/_Module/SubModule.xml` (or `.UIExtenderEx`, `.MBOptionScreen` — note: MCM's stub is `Bannerlord.MBOptionScreen`). A **patch** bump within the same minor needs no stub edit — the `.99.0` sentinel already covers it. See the v99 rule under "Stub modules" below.
5. Run `dotnet restore Dependencies/TAOM.Dependencies.csproj`.
6. Run `./build.ps1 -RunTests` — must pass. (If Bannerlord is open and you only need build+test signal, `dotnet build TAOM.sln -p:DisableModuleCopy=true` + `dotnet test` skips the game-install deploy — but it does NOT prove the deploy, so the smoke test below is still owed.)
7. Run smoke test (see "Verification" below).

Steps 3 and 4 are enforced by [`BundledDependencyManifestTests`](../../TAOM.Tests/Infrastructure/Dependencies/BundledDependencyManifestTests.cs) — if you forget either, `dotnet test` fails with the expected value.

**Build prerequisite:** Bannerlord must be CLOSED during `./build.ps1`. The MSBuild `PostBuildCopyToModules` step deploys DLLs (including `0Harmony.dll`) directly into the game install — if Bannerlord is running, those files are file-locked and the build fails with `UnauthorizedAccessException`. Close the game, retry build.

### Category 2 — Bundled BUTR runtime DLLs (manually copied from Steam Workshop)

These DLLs are NOT on NuGet. They're distributed as Bannerlord modules via Steam Workshop or NexusMods. We bundle them in `Dependencies/_Module/bin/Win64_Shipping_Client/`:

Patterns, not a fixed list — the impl set grows every time BUTR ships a build for a new game version. The DLLs on disk in `Dependencies/_Module/bin/Win64_Shipping_Client/` are the source of truth for what we currently vendor.

| Module / DLL pattern | Source (Steam Workshop ID) | Where to copy from |
|---|---|---|
| `Bannerlord.ButterLib.dll` | `2859232415` | `E:\Steam\steamapps\workshop\content\261550\2859232415\bin\Win64_Shipping_Client\` |
| `Bannerlord.ButterLib.Implementation.<game-ver>.dll` (**all** of them ≤ the engine you target) | `2859232415` | same as above |
| `Bannerlord.MBOptionScreen.v<game-ver>.dll` (**all** of them) | `2859238197` | `E:\Steam\steamapps\workshop\content\261550\2859238197\bin\Win64_Shipping_Client\` |
| `Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen.dll` | `2859238197` | same as above |
| `MCM.UI.Adapter.MCMv5.dll` | `2859238197` | same as above |
| `BUTR.CrashReport*.dll` (6 files) | `2859232415` | same as ButterLib — **not optional**, see the inventory note below |

> **The `<game-ver>` suffix names the game build BUTR compiled that impl against — not a version TAOM supports.** The meta-loader picks the highest impl whose suffix is ≤ the running engine. So a gap between the newest bundled impl and the Native pin is normal whenever BUTR hasn't shipped an impl for the current engine yet (e.g. on 1.4.7 the loader correctly runs the `1.4.5` impl, because BUTR ships nothing newer). Bundling only a stale subset is the actual bug — see Scenario A.

**Steam Workshop folder mapping (Bannerlord app ID = 261550):**
- `2859188632` — Bannerlord.Harmony (we DON'T bundle this DLL — we use Lib.Harmony NuGet instead)
- `2859222409` — Bannerlord.UIExtenderEx (we DON'T bundle this DLL — we use Bannerlord.UIExtenderEx NuGet)
- `2859232415` — Bannerlord.ButterLib (BUNDLED — no NuGet equivalent for runtime)
- `2859238197` — Bannerlord.MBOptionScreen (MCM screen UI — BUNDLED)

**Update procedure when Bannerlord ships new minor version (e.g., 1.5.0):**

1. **Confirm BUTR has shipped matching versions.** Steam Workshop auto-updates the BUTR modules when they ship 1.5.x-compatible builds. Check the Workshop folders for new `Implementation.1.5.x.dll` and `MBOptionScreen.v1.5.x.dll` files.
2. **Copy the new versioned DLLs** into `Dependencies/_Module/bin/Win64_Shipping_Client/`:
   ```pwsh
   Copy-Item "E:\Steam\steamapps\workshop\content\261550\2859232415\bin\Win64_Shipping_Client\Bannerlord.ButterLib.Implementation.1.5.*.dll" `
             -Destination "Dependencies\_Module\bin\Win64_Shipping_Client\"
   Copy-Item "E:\Steam\steamapps\workshop\content\261550\2859238197\bin\Win64_Shipping_Client\Bannerlord.MBOptionScreen.v1.5.*.dll" `
             -Destination "Dependencies\_Module\bin\Win64_Shipping_Client\"
   ```
3. **Also copy the loader + adapter** (these typically don't change per game version, but verify):
   ```pwsh
   Copy-Item "E:\Steam\steamapps\workshop\content\261550\2859238197\bin\Win64_Shipping_Client\Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen.dll" `
             -Destination "Dependencies\_Module\bin\Win64_Shipping_Client\" -Force
   Copy-Item "E:\Steam\steamapps\workshop\content\261550\2859238197\bin\Win64_Shipping_Client\MCM.UI.Adapter.MCMv5.dll" `
             -Destination "Dependencies\_Module\bin\Win64_Shipping_Client\" -Force
   ```
4. **Update Main/_Module/SubModule.xml** — bump `<DependedModuleMetadata id="Native" version="v1.5.0.*" />` to the new version.
5. **Re-evaluate any older Implementation.1.4.x.dll files** — you can leave them for fallback compatibility OR delete them to slim the deployment. If deleting, also remove the version from `.gitignore` exception list.
6. **Rebuild + test:** `./build.ps1 -RunTests`. Run smoke test.

### Category 3 — Microsoft.Extensions + Serilog runtime DLLs (bundled with ButterLib, same source as Category 2)

These ship alongside ButterLib in its Steam Workshop folder:

| DLL | Purpose |
|---|---|
| `Microsoft.Bcl.HashCode.dll` | HashCode polyfill for net472 |
| `Microsoft.Extensions.DependencyInjection.dll` + `.Abstractions.dll` | DI container for ButterLib |
| `Microsoft.Extensions.Logging.dll` + `.Abstractions.dll` | Logging abstractions |
| `Microsoft.Extensions.Options.dll`, `.Primitives.dll` | Configuration system |
| `Serilog.dll`, `Serilog.Extensions.Logging.dll`, `Serilog.Sinks.File.dll` | ButterLib's structured logging backend |
| `System.Buffers.dll`, `System.Memory.dll`, `System.Numerics.Vectors.dll`, `System.Collections.Immutable.dll`, `System.Reflection.Metadata.dll` | .NET runtime polyfills for net472 |

**Update procedure:** copied alongside ButterLib in the Category 2 step above. Their versions are pinned by ButterLib's distribution; we don't manage them independently.

### Category 4 — Main module vendored DLLs (Warg AI + native skin fixes)

A separate pool of vendored DLLs lives in `Main/_Module/bin/Win64_Shipping_Client/` (NOT `Dependencies/_Module/bin/`). These ship with the **TAOM module itself**, not with `TAOM.Dependencies`. They were previously gitignored — `chore(build)` commit `c4231c8` (2026-05-23) added a `.gitignore` allowlist mirroring the Category 2 pattern.

| DLL | Origin | Why bundled |
|---|---|---|
| `MinHook.x64.dll` | [TsudaKageyu/MinHook](https://github.com/TsudaKageyu/minhook), MIT — third-party native hook lib | Runtime dep of `TAOM.NativeSkinFixes.dll` |
| `TAOM.NativeSkinFixes.dll` | **TAOM-owned C++** — source vendored at `Dependencies/NativeSkinFixes.NativeHooks/` (in-repo since 2026-05-26) | TAOM's own native plugin for covers_head morph fix + hair/beard cloth simulation. See [`docs/features/native-skin-fixes.md`](../features/native-skin-fixes.md). |

> `BehaviorTrees.dll` + `BehaviorTreeWrapper.dll` were removed from this table on 2026-05-24 — both libraries were decompiled (no upstream source repo) and inlined as TAOM source at `Main/BehaviorTrees/` + `Main/BehaviorTreeWrapper/`. They compile into `TAOM.dll` now. RCA: [docs/reviews/rca-looter-battle-nre-2026-05-24.md](../reviews/rca-looter-battle-nre-2026-05-24.md). Do NOT re-vendor these binaries — edit the inlined source instead.

**Update procedure (MinHook):** stable third-party binary; only update when the upstream releases a new build. Drop the new `.dll` into `Main/_Module/bin/Win64_Shipping_Client/`, run `./build.ps1 -RunTests`, smoke test, commit.

**Update procedure (TAOM.NativeSkinFixes):** the C++ source lives in-repo at `Dependencies/NativeSkinFixes.NativeHooks/`. Workflow per change:

1. Edit the C++ source under `Dependencies/NativeSkinFixes.NativeHooks/` (hook bodies in `*Hook.cpp`, byte patterns in `Signatures.h`). The `.vcxproj` is standalone — NOT in `TAOM.sln` — to keep MSVC off the critical path for teammates / CI building `TAOM.dll` only.
2. Run `pwsh Dependencies/NativeSkinFixes.NativeHooks/Build.ps1` to rebuild. Output writes directly into `Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll` (and `MinHook.x64.dll` is copied via post-build step).
3. Run `./build.ps1` — `Bannerlord.BuildResources` will deploy the new DLL into the game install on every dotnet build automatically.
4. `git add Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll && git commit` — the `.gitignore` allowlist explicitly permits this binary, but `git add` is still required (it's not auto-staged by `dotnet build`).

**Important:** `MCMv5.dll` is NOT in this folder. MCMv5 is provided by `TAOM.Dependencies` (`Bannerlord.MBOptionScreen*.dll` + `MCM.UI.Adapter.MCMv5.dll`) + the `Bannerlord.MCM` NuGet (compile-time). The vestigial `<Reference Include="MCMv5">` block was removed from `Main/TAOM.csproj` in commit `c4231c8`. Do not re-add it.

**Allowlist gotcha:** The `.gitignore` allowlist is explicit — only the 4 DLLs above are un-ignored. If you add a new vendored DLL to this folder, you MUST also add a `!Main/_Module/bin/Win64_Shipping_Client/<name>.dll` line, or git will silently keep ignoring it. `TAOM.dll` + `TAOM.pdb` stay ignored by design (build outputs regenerated by `dotnet build`).

## Verification (smoke test after any dependency update)

1. **Close Bannerlord** if running (DLLs are file-locked).
2. **Rebuild:** `./build.ps1 -RunTests`
   - Both build and test must be green.
3. **Verify TAOM.Dependencies bin deployment:**
   ```pwsh
   Get-ChildItem "$env:BANNERLORD_GAME_DIR\Modules\TAOM.Dependencies\bin\Win64_Shipping_Client\" | Select-Object Name, LastWriteTime
   ```
   - Should match the file inventory at the bottom of this doc, with timestamps matching the build.
4. **Launch Bannerlord** via Steam.
5. **Check the launcher's enabled modules:** Only `TAOM` + `TAOM.Dependencies` (plus Native/SandBox/SandBoxCore/CustomBattle) should be required. `Bannerlord.Harmony`, `Bannerlord.UIExtenderEx`, `Bannerlord.ButterLib`, `Bannerlord.MBOptionScreen` should NOT be required.
6. **Confirm in-game:**
   - Mod loads (no "TAOM.TAOM submodule could not be loaded" warning).
   - Options screen has a **Mod Options** tab.
   - Mod Options tab lists TAOM's settings (BattleBalance, RevoltTuning, etc.).
   - Click into a setting category and verify values render correctly.
   - Change a value, exit Options, re-enter — value persists.

If any of these fail, the most common causes are:
- Workshop folder didn't have an impl at or below your engine version (if you're on a new minor and BUTR hasn't shipped a matching build yet, MCM/ButterLib won't load — see "BUTR ships behind Bannerlord").
- DLL got corrupted on copy (re-copy from Workshop).
- SubModule.xml version constraint doesn't match installed Bannerlord (check `Main/_Module/SubModule.xml`'s `DependedModuleMetadata id="Native" version=` line).

## Common scenarios

### Scenario A: Bannerlord ships a patch within the current minor (e.g. 1.4.6 → 1.4.7)

**This scenario's old advice ("most likely nothing needs to change") is what caused the 2026-07 drift — it was followed literally on both the 1.4.6 and 1.4.7 bumps, and both the Native pin and the vendored impl set went stale.** Do all of:

1. **Bump `Main/_Module/SubModule.xml`'s Native constraint** to the new version (`v<new>.*`). Enforced by `BundledDependencyManifestTests.NativeConstraint_MatchesPinnedGameVersion` against `.claude/pinned-game-version.txt`.
2. **Re-check the Workshop impl set.** BUTR ships new `Implementation.<game-ver>.dll` / `MBOptionScreen.v<game-ver>.dll` builds on its own cadence, independent of the engine. If the Workshop folders now hold impls newer than what we vendor, copy them in — otherwise the meta-loader silently keeps selecting an older impl than BUTR intends. (This is exactly what happened: TAOM shipped only `1.4.0`/`1.4.1` while BUTR had shipped through `1.4.5`.)
3. **Re-check the NuGet pins** (`Lib.Harmony`, `Bannerlord.UIExtenderEx`, `Bannerlord.MCM`) against current releases. An engine patch is a natural checkpoint even though these version independently of the game.
4. Run the smoke test.

### Scenario B: Bannerlord ships 1.5.0 (new minor version)

1. Wait for BUTR to ship matching builds (check NexusMods or BUTR Discord).
2. Steam Workshop auto-updates the BUTR modules — verify new DLLs appear in `261550/<module-id>/bin/`.
3. Copy `Bannerlord.ButterLib.Implementation.1.5.0.dll` + `Bannerlord.MBOptionScreen.v1.5.0.dll` into our bin/.
4. Bump SubModule.xml Native version constraint.
5. Test + commit.

### Scenario C: Security patch in Microsoft.Extensions

1. ButterLib's Workshop release will ship updated Microsoft.Extensions DLLs.
2. Re-copy ALL the Microsoft.Extensions.* and Serilog.* DLLs from the ButterLib Workshop folder.
3. No NuGet bump needed (we don't reference these as packages — only as bundled DLLs).
4. Test + commit.

### Scenario D: BUTR releases new MCM or ButterLib major version (e.g., MCM v6)

This is a larger change. New major versions can:
- Change SubModule class names (e.g., `MCM.MCMv6SubModule` instead of `MCM.MCMSubModule`).
- Change runtime dependencies (e.g., require new Microsoft.Extensions version).
- Add new SubModule classes (e.g., new screen renderer).

Process:
1. Read the BUTR release notes for the new version.
2. Read the new `SubModule.xml` from the BUTR module's release.
3. Update `Dependencies/_Module/SubModule.xml` to match (add/rename/remove `<SubModule>` entries).
4. Bump NuGet version pins in `Dependencies/TAOM.Dependencies.csproj` if the new version is on NuGet.
5. Copy the new bundled DLLs from Workshop.
6. Test thoroughly — major versions often have breaking API changes that ripple into TAOM source.

### Scenario E: We need a Lib.Harmony version bump (e.g., security patch in 0Harmony)

1. Update `Dependencies/TAOM.Dependencies.csproj`'s `<PackageReference Include="Lib.Harmony" Version="X.Y.Z" />`.
2. Update `Main/TAOM.csproj`'s `<PackageReference Include="Lib.Harmony" Version="X.Y.Z" IncludeAssets="compile" />` (must match).
3. `dotnet restore`.
4. Build + test.

## Risk scenarios + mitigations

### Stub modules (third-party-mod compatibility)

To preserve compatibility with third-party Bannerlord mods that declare `<DependedModule Id="Bannerlord.Harmony"/>` (or `.ButterLib` / `.UIExtenderEx` / `.MBOptionScreen`) in their `SubModule.xml`, TAOM.Dependencies ships **four passive stub modules** at the standard BUTR IDs.

Each stub is a single ~20-line `SubModule.xml` deployed to `Modules/<ID>/_Module/SubModule.xml`, for the four standard BUTR IDs: `Bannerlord.Harmony`, `Bannerlord.UIExtenderEx`, `Bannerlord.ButterLib`, `Bannerlord.MBOptionScreen`. Their `<Version>` values are NOT listed here — read `Stubs/<ID>/_Module/SubModule.xml`, and see the v99 rule below for how each is derived.

Source files live in `Stubs/<ID>/_Module/SubModule.xml` and are deployed by the `DeployTAOMDependenciesStubs` MSBuild target in `Dependencies/TAOM.Dependencies.csproj` (fires `AfterTargets="PostBuildCopyToModules"`).

Each stub:
- Declares the standard BUTR `<Id>` so the vanilla launcher's `AreAllDependenciesOfModulePresent` check passes.
- Has `<SubModules />` empty — no DLLs load from the stub, so no duplicate `0Harmony.dll` / `Bannerlord.ButterLib.dll` enters the AppDomain.
- Declares `<DependedModule Id="TAOM.Dependencies"/>` with `<DependedModuleMetadata id="TAOM.Dependencies" order="LoadBeforeThis"/>` so the real DLLs are loaded by TAOM.Dependencies BEFORE any third-party mod tries to consume them.
- Uses `<DefaultModule value="true"/>` so the vanilla launcher auto-ticks the stub on first launch. Without this flag, the launcher's first-launch enablement logic (`item.IsSelected = item.IsNative || ((item.IsRequiredOfficial || item.IsDefault) && AreAllDependenciesOfModulePresent(item))` in `LauncherModsVM.cs:~350`) leaves the stub unchecked — and while the launcher's dep-presence check is file-on-disk only (doesn't require the stub to be ticked), users perceive un-ticked stubs as "deps missing" and may not realize they need to manually tick four placeholder entries. Auto-enable is the BetaDeps-community convention for stub modules.

**DR3 Phase 4 update (2026-05-27):** each stub now ships a real `<SubModule>` entry referencing `TAOM.Dependencies.AliasStubSubModule` (a try/catch-wrapped `MBSubModuleBase`), superseding the original empty-`<SubModules />` design described above — this is the early-phase install point for the defensive infrastructure (see "Defensive infrastructure" below). No BUTR DLLs load from the stub itself.

**Maintenance rule (v99 strategy, BetaDeps parity, DR3 Phase 4 — 2026-05-25):** stub `<Version>` values use the `vX.Y.99.0` pattern, where `X.Y` is the **minor** of the version we actually ship (the `PackageReference` pin for Harmony/UIExtenderEx/MCM; the vendored DLL's own version for ButterLib). They are deliberately NOT exact-match to the shipped DLL.

Third-party mods often declare `<DependedModuleMetadata version="vX.Y.x"/>` as a minimum-version check; the `.99.0` sentinel satisfies any reasonable lower-bound within that minor without claiming a major-version jump. Consequences:

- **Minor bump** (e.g. Harmony 2.4.x → 2.5.x, MCM 5.11.x → 5.12.x) → bump the stub to the new minor's `.99.0`.
- **Patch bump** (e.g. UIExtenderEx 2.13.1 → 2.13.2) → **no stub edit**; `v2.13.99.0` already covers it.
- The vanilla launcher does not enforce these constraints at all; BLSE does.
- Caveat (see the Harmony stub's own comment): a legacy mod declaring a strict `v2.4.0.*` wildcard will NOT match `v2.4.99.0`.

Both derivations are enforced by [`BundledDependencyManifestTests`](../../TAOM.Tests/Infrastructure/Dependencies/BundledDependencyManifestTests.cs) — you cannot forget the minor bump silently.

> An earlier revision of this section also carried a stricter rule ("bump the stub whenever *any* version changes"), which contradicted the minor-keyed rule above on the patch-bump case. The minor-keyed rule is correct and the contradiction is removed (2026-07-16).

**Red `(!)` icon on third-party mods:** this is the launcher's `IsDangerous` flag (`LauncherModuleVM.cs:280-282`) fired by TaleWorlds's `LauncherDLLData` code-verification system whenever an unsigned/third-party DLL is detected. It is a permanent warning tooltip ("Couldn't verify some or all of the code included in this module") and is **independent of toggleability** — every non-Bannerlord mod gets it. Do not mistake it for a missing-dep error. The two phenomena (`IsDisabled` = greyed/un-toggleable from missing deps, vs `IsDangerous` = red icon from unsigned code) are separate concepts in the launcher source.

### Defensive infrastructure (DR3 Phase 4 — BetaDeps parity)

TAOM.Dependencies ships 11 classes under `Dependencies/Foundation/` (added DR3 Phase 4, 2026-05-27) that catch third-party mod runtime errors and let the game keep running. Adopted from BetaDeps v0.7.5.1 (Nexus 11274) via clean-room rewrite under MIT — see `Dependencies/_Module/THIRD-PARTY-LICENSES.txt`.

The full 11-class roster: `RuntimeLog` (log-path resolver), `DiagLog` (threadsafe append-only logger — see the diagnostic-logs table below), `ReflectionUtils` (small reflection helpers), `VersionProbe` (Bannerlord version + branch detection), `IncompatibleModDetector`, `PatchShield`, `SaveShield` + `FailureRecord` + `FailedModsCatalog` (the record type + catalog writer behind `failed-mods-catalog.txt`), `SubModuleConstructionGuard`, `CollectAssemblyTypesShim`.

**Components installed in stub-ctor (early phase, before any third-party mod):**
- `IncompatibleModDetector` — writes `session-launching.marker` at startup, deletes on main-menu reach. If marker survives to next launch, previous session crashed pre-menu; diffs modlist against `last-good-modlist.txt` to identify newly-added likely-culprit mods. **Detection only**, no XML mutation of LauncherData.xml.
- `CollectAssemblyTypesShim` — wraps `Assembly.GetTypes()` + `.GetExportedTypes()` with a Finalizer that catches `ReflectionTypeLoadException` and returns the partial type list (`ex.Types.Where(t => t != null)`). Prevents cascade failures.
- `SubModuleConstructionGuard` — Harmony Finalizer on `MBSubModuleBase` ctors. Swallows third-party SubModule ctor exceptions, logs culprit, lets launcher continue. Refuses to shield TAOM-owned SubModules.

**Components installed in TAOM.Dependencies/SubModule.OnSubModuleLoad (late phase):**
- `PatchShield` — **biggest user-value**. Iterates every Harmony-patched method in the AppDomain, attaches a Finalizer to each non-TAOM patch. Catches the trinity (`MissingMethodException`, `MissingFieldException`, `TypeLoadException`) — the canonical errors when a mod was compiled against an old Bannerlord version. Auto-unpatches the offending owner's prefixes/postfixes/transpilers from the failing target. Re-run in `OnGameInitializationFinished` to catch late-registered patches. **Hot-target exclusion (2026-07-10, #331):** targets in `TaleWorlds.GauntletUI`/`TaleWorlds.TwoDimension`/`TaleWorlds.MountAndBlade.GauntletUI` are NEVER shielded (`ExcludedTargetNamespacePrefixes`) — the shield finalizer binds `__originalMethod`, so Harmony's wrapper pays `GetMethodFromHandle` + try/catch per CALL (~50µs); stacked on UIExtenderEx's prefab-system patches (`WidgetFactory.IsCustomType`, `WidgetTemplate.OnRelease`) it turned the tournament UI's ~10^6-call template release into a measured 104-109s frozen exit. Shield value in that layer is nil (its only patcher is BUTR's own UIExtenderEx); the third excluded prefix (`TaleWorlds.MountAndBlade.GauntletUI`) also covers TAOM's own per-frame widget targets like Patch38's nameplate fade (#331 round 2). Before shielding any new layer, cost the wrapper against the hottest conceivable target — see LESSONS-LEARNED "Blanket-patching infrastructure".
- `SaveShield` — targeted Finalizer on 10 specific TaleWorlds save/load/mission methods. Catches `DuplicateKey` + other save failures; stack-walks to attribute culprit assembly. Writes records to `failed-mods-catalog.txt` for user diagnosis.

**Diagnostic logs (always written if at all possible):**

| File | Purpose |
|---|---|
| `<game>/Modules/TAOM.Dependencies/diag.log` | Append-only runtime event log (DiagLog). All shield activity, AssemblyResolve redirects, version probe results, crash-loop detection. Inspect first when diagnosing any incident. |
| `<game>/Modules/TAOM.Dependencies/failed-mods-catalog.txt` | One line per (culprit-mod, exception-type, owner-method) that a shield swallowed. Format: `<UTC> | <culprit> | <category> | <ExceptionType> | <owner method> | <message head>`. |
| `<game>/Modules/TAOM.Dependencies/session-launching.marker` | Crash-loop sentinel. Created at SubModule construction; deleted on `OnGameInitializationFinished` (main menu reached). Survival to next launch = previous session crashed pre-menu. |
| `<game>/Modules/TAOM.Dependencies/last-good-modlist.txt` | Snapshot of enabled modules at last main-menu reach. Used by `IncompatibleModDetector` to diff against current modlist for culprit identification. |

**Opt-out flags (place an empty file at the path to activate):**

| Flag file (path: `<game>/Modules/TAOM.Dependencies/<name>`) | Effect |
|---|---|
| `patchshield-disabled.flag` | Skip `PatchShield.Install` entirely. Use when diagnosing whether a crash is masked by PatchShield vs an actual problem in TAOM. |
| `saveshield-swallow-disabled.flag` | Install SaveShield BUT re-throw exceptions instead of swallowing. Use when investigating which save/mission method is failing. |

(No opt-out for `SubModuleConstructionGuard` / `CollectAssemblyTypesShim` / `IncompatibleModDetector` — these are detection-only or have no destructive effect.)

**Verifying the shields are healthy:**

After a normal launch (made it to main menu), `diag.log` should contain entries like:
```
[INFO  ] [AliasStub]                  alias stub loaded: TAOM.Dependencies
[INFO  ] [VersionProbe]               detected via ApplicationVersionHelper: v1.4
[INFO  ] [IncompatibleModDetector]    MarkSessionLaunchSuccessful: saved 47-mod last-good snapshot
[INFO  ] [PatchShield]                shield pass: +N new, 0 already-shielded, M skipped (total: N)
[INFO  ] [SaveShield]                 install complete: shielded +K new, 0 already-shielded, 0 skipped
[INFO  ] [TAOM.Dependencies]          OnSubModuleLoad complete
[INFO  ] [PatchShield]                SESSION SUMMARY: shielded N method(s), unpatched 0 target(s), swallowed 0 exception(s)...
```

If the session summary shows non-zero swallow counts, **a mod in the user's modlist is broken** — check `failed-mods-catalog.txt` for the culprit attribution. If swallow counts are climbing across sessions for the same mod, that mod needs updating or removing.

### External Bannerlord.Harmony module conflict (HIGH)

**Symptom:** User has the external standalone `Bannerlord.Harmony` module (e.g., from Steam Workshop) installed in `Modules/Bannerlord.Harmony/` AND TAOM.Dependencies's stub module is also deployed to the same path.

**Risk:** Folder-name collision. The Bannerlord.BuildResources `PostBuildCopyToModules` step will OVERWRITE the external module's `SubModule.xml` with our stub's `SubModule.xml`. If the user later disables TAOM and reinstalls the external Harmony module via Workshop, that restores the external. But if both are installed concurrently (e.g., user subscribes to the Workshop Harmony module after TAOM is already deployed), Steam's update may re-overwrite our stub, breaking third-party compatibility silently.

Additionally, if the external module folder somehow survives our stub deploy (e.g., on a non-Steam install where stubs are manually copied), both folders would claim `<Id value="Bannerlord.Harmony"/>` — the launcher's behavior for duplicate IDs is undefined (probably first-parsed wins).

**Mitigation:** When users install TAOM, instruct them to **uninstall** any standalone `Bannerlord.Harmony` / `Bannerlord.ButterLib` / `Bannerlord.UIExtenderEx` / `Bannerlord.MBOptionScreen` modules from their Steam Workshop subscriptions and `Modules/` directory. TAOM.Dependencies + the four stubs provide everything those modules would. Add this to TAOM's README or launcher tooltip.

### BUTR ships behind Bannerlord (HIGH)

**Symptom:** Bannerlord ships a new minor. BUTR hasn't shipped a matching impl yet, so our newest bundled `Implementation.<game-ver>.dll` is the closest the loader can pick.

**Risk:** The fallback impl makes API calls that broke across the minor boundary. ButterLib / MCM crashes at startup.

**Mitigation:** **Do not upgrade Bannerlord until BUTR ships matching builds.** Use the `dual-dll-setup.md` procedure (Steam backup of your last-known-good install) to roll back if you accidentally updated.

> Note this risk applies at a **minor** boundary. Within a minor, running an older impl than the engine is normal and expected — BUTR does not ship an impl per patch.

### Fresh clone on non-Steam machine (HIGH)

**Symptom:** Contributor clones repo on a machine without Steam (or with Steam but no Bannerlord installed + no Workshop subscription). They run `./build.ps1`, get past compilation, but `Dependencies/_Module/bin/Win64_Shipping_Client/` is missing the bundled BUTR DLLs (they're tracked in git so this WORKS for a fresh clone — but if they delete the dir, they can't easily rebuild it).

**Risk:** Build appears to succeed but produces a broken module folder. Player can't run.

**Mitigation:**
- Bundled BUTR DLLs are committed to git per `.gitignore` exception (see Section X).
- If the bundled DLLs are missing for any reason (manual delete, corrupted clone), recover via either:
  - **Steam Workshop:** Re-subscribe to BUTR modules. Steam re-downloads to `E:\Steam\steamapps\workshop\content\261550\`. Copy DLLs from there.
  - **NexusMods (no Steam required):**
    - ButterLib: https://www.nexusmods.com/mountandblade2bannerlord/mods/2018 → download → unzip → copy `Modules/Bannerlord.ButterLib/bin/Win64_Shipping_Client/*.dll`
    - MCM (MBOptionScreen): https://www.nexusmods.com/mountandblade2bannerlord/mods/612 → download → unzip → copy `Modules/Bannerlord.MBOptionScreen/bin/Win64_Shipping_Client/*.dll`
    - Harmony: https://www.nexusmods.com/mountandblade2bannerlord/mods/2006 → download → unzip (NOTE: We use the NuGet 0Harmony.dll, not the Workshop version)
    - UIExtenderEx: https://www.nexusmods.com/mountandblade2bannerlord/mods/2102 → download → unzip (we use NuGet)
- Or, ask a maintainer for a pre-built `Dependencies/_Module/bin/` archive.

### Linux compatibility break (MED)

**Symptom:** TAOM is built on/for Linux. BUTR.CrashReport's ImGui renderer references `cimgui.dll` + `glfw3.dll` (Windows native).

**Risk:** Loading TAOM.Dependencies on Linux fails when ButterLib tries to initialize crash report renderer.

**Mitigation:** Currently TAOM is Windows-only. We do NOT bundle `cimgui.dll` or `glfw3.dll`. ButterLib's crash report renderer is opt-in (per its config); the base ButterLib initializes fine without it. If Linux support is later added, OS-specific gating will be needed.

### Bundled Implementation falls back wrong version (MED)

**Symptom:** BUTR's meta-loader picks an older bundled `Implementation.<game-ver>.dll` than the running engine (it selects the closest match ≤ the game version). This is normal *if* it's the newest impl BUTR ships — it's a **bug** if we simply failed to vendor the newer impls BUTR has released (the 2026-07 case: we shipped `1.4.0`/`1.4.1` while BUTR had `1.4.5`).

**Risk:** If the gap spans an API break in the methods Implementation calls, runtime crash.

**First check:** compare the impls in the Workshop folder against ours. If BUTR has newer ones, this is a vendoring miss — copy them in (Category 2).

**Mitigation:** Test smoke (see "Verification" above) catches this. If MCM tab fails to render or settings crash on access, check `mb.log` for `TypeLoadException` / `MissingMethodException`. Fix by:
- Updating SubModule.xml's `LoaderFilter` to specifically reference the version that's known-working
- Or removing the older Implementation.*.dll files so the loader is forced to pick a specific one

## Documentation links

For the wider TAOM team:

- **CLAUDE.md** — Critical Rules section (when touching TAOM.Dependencies): see this maintenance doc first.
- **docs/migration/dr3-execution-handoff.md** — Empirical findings from the 9 hours of source-merge attempts that motivated this architecture.
- **docs/migration/dr3-mcm-internalization-plan.md** — Original DR3 plan + investigation log.
- **docs/migration/dual-dll-setup.md** — Steam DLL backup/swap procedure (for rolling back Bannerlord versions during dependency-stale events).

## Reference: file inventory

After a fresh build, `Dependencies/_Module/bin/Win64_Shipping_Client/` should contain these files. The `Implementation.*` / `MBOptionScreen.v*` sets are **open-ended** — one per game version BUTR has shipped a build for, so the count grows over time. Check the folder itself, not this list, for the current set.

```
0Harmony.dll                                                  (NuGet — Lib.Harmony)
Bannerlord.UIExtenderEx.dll                                   (NuGet — Bannerlord.UIExtenderEx)
MCMv5.dll                                                     (NuGet — Bannerlord.MCM)
System.Runtime.CompilerServices.Unsafe.dll                    (NuGet — Lib.Harmony transitive)
TAOM.Dependencies.dll                                         (built from our source)
TAOM.Dependencies.pdb                                         (built from our source)
Bannerlord.ButterLib.dll                                      (vendored from Workshop 2859232415)
Bannerlord.ButterLib.Implementation.<game-ver>.dll  × N       (vendored — one per BUTR-supported game build)
Bannerlord.MBOptionScreen.v<game-ver>.dll           × N       (vendored from Workshop 2859238197)
Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen.dll         (vendored)
MCM.UI.Adapter.MCMv5.dll                                      (vendored)
BUTR.CrashReport.dll                                          (vendored — ButterLib dep, MANDATORY)
BUTR.CrashReport.Models.dll                                   (vendored — ButterLib dep, MANDATORY)
BUTR.CrashReport.Renderer.Html.dll                            (vendored — ButterLib dep, MANDATORY)
BUTR.CrashReport.Renderer.ImGui.dll                           (vendored — ButterLib dep, MANDATORY)
BUTR.CrashReport.Renderer.WinForms.dll                        (vendored — ButterLib dep, MANDATORY)
BUTR.CrashReport.Renderer.Zip.dll                             (vendored — ButterLib dep, MANDATORY)
Microsoft.Bcl.HashCode.dll                                    (vendored — ButterLib dep)
Microsoft.Extensions.DependencyInjection.dll                  (vendored — ButterLib dep)
Microsoft.Extensions.DependencyInjection.Abstractions.dll     (vendored — ButterLib dep)
Microsoft.Extensions.Logging.dll                              (vendored — ButterLib dep)
Microsoft.Extensions.Logging.Abstractions.dll                 (vendored — ButterLib dep)
Microsoft.Extensions.Options.dll                              (vendored — ButterLib dep)
Microsoft.Extensions.Primitives.dll                           (vendored — ButterLib dep)
Serilog.dll                                                   (vendored — ButterLib dep)
Serilog.Extensions.Logging.dll                                (vendored — ButterLib dep)
Serilog.Sinks.File.dll                                        (vendored — ButterLib dep)
System.Buffers.dll                                            (vendored — ButterLib dep)
System.Collections.Immutable.dll                              (vendored — ButterLib dep)
System.Memory.dll                                             (vendored — ButterLib dep)
System.Numerics.Vectors.dll                                   (vendored — ButterLib dep)
System.Reflection.Metadata.dll                                (vendored — ButterLib dep)
```

> **The 6 `BUTR.CrashReport*` DLLs are not optional and were missing from this inventory until 2026-07-16.** ButterLib references them in its metadata; without them, ButterLib's type enumeration throws `ReflectionTypeLoadException` at SubModule init — a crash this project has already shipped once. A maintainer following the old inventory would have rebuilt a bin folder that reproduces it.

Count is intentionally not stated — it moves with the impl set. Compare against the folder.

## Future improvement: ILRepack consolidation

The current architecture ships ~28 separate DLLs in TAOM.Dependencies's bin folder. A future improvement is to use [`ILRepack.Lib.MSBuild.Task`](https://www.nuget.org/packages/ILRepack.Lib.MSBuild.Task/) to merge all of these into a single `TAOM.Dependencies.dll`. Steps:

1. Add `<PackageReference Include="ILRepack.Lib.MSBuild.Task" Version="2.0.34.2" PrivateAssets="all" />` to Dependencies csproj.
2. Add an MSBuild `Target` that runs after Build:
   ```xml
   <Target Name="ILRepack" AfterTargets="Build">
     <ItemGroup>
       <InputAssemblies Include="$(OutputPath)0Harmony.dll" />
       <InputAssemblies Include="$(OutputPath)Bannerlord.UIExtenderEx.dll" />
       <InputAssemblies Include="$(OutputPath)Bannerlord.ButterLib*.dll" />
       <InputAssemblies Include="$(OutputPath)Bannerlord.MBOptionScreen*.dll" />
       <InputAssemblies Include="$(OutputPath)MCMv5.dll" />
       <InputAssemblies Include="$(OutputPath)MCM.UI.Adapter.MCMv5.dll" />
       <InputAssemblies Include="$(OutputPath)Microsoft.Extensions.*.dll" />
       <InputAssemblies Include="$(OutputPath)Serilog*.dll" />
       <InputAssemblies Include="$(OutputPath)System.*.dll" />
       <InputAssemblies Include="$(OutputPath)Microsoft.Bcl.HashCode.dll" />
     </ItemGroup>
     <ILRepack
       Parallel="true"
       Internalize="false"
       InputAssemblies="@(InputAssemblies)"
       OutputFile="$(OutputPath)TAOM.Dependencies.dll"
       ... />
   </Target>
   ```
3. Test thoroughly — ILRepack can cause issues with reflection (types end up under different assembly identity), assembly identity collisions, signing key conflicts.

Not done in DR3 because:
- The bundled-DLL approach already achieves the user-visible goal (no external BUTR modules in launcher).
- ILRepack adds build-time complexity and a real risk of subtle runtime issues (e.g., HarmonySharedState's cross-assembly hash table doesn't expect merged Harmony).
- Each iteration requires full game launch to validate.

If ILRepack consolidation is later desired, do it as a focused project with thorough in-game testing of each module's functionality.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
