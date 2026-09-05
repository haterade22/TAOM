# Distance Cache Rebuild

> Despite the directory name `Main/Features/EditorCacheRebuild/`, this is now a runtime-only feature triggered from the in-game MCM menu. The name stems from the original editor-mode design; renaming the directory was deferred (mechanical refactor across ~30 files, zero behavioral benefit).

## Overview

Parallel + incremental + resumable settlement distance cache builder, triggered from the in-game MCM menu (Options → Mod Options → TAOM → "Map Tools / Distance Cache Rebuild" → **Rebuild Now**). Runs in-game against the live campaign's `MapSceneWrapper`, writes output atomically with a `.prev` backup, includes round-trip verification.

A full rebuild drops from ~108 hours to ~7 minutes on TAOM's 863-settlement map; incremental rebuilds after small edits target ~30 seconds.

## Why This Exists

- **Vanilla behavior:** `SettlementPositionScript.SaveSettlementDistanceCacheEditor()` (editor button) calls `NavigationCache<SettlementRecord>.GenerateCacheData()` which runs three serial phases: closest-settlement-to-face (cheap), settlement-to-settlement distance (O(n²) A\*, ~6hr on TAOM), neighbor cache (O(n²) corridor scan, ~102hr on TAOM). All single-threaded, no checkpointing, no incremental support.
- **TAOM requirement:** TAOM has 863 settlements vs ~70 in vanilla Native. The cache rebuild is ~5 days of editor frozen on a click. The user observed 22 hours of progress with 86hr remaining on the May 11 vanilla run.
- **Why MCM trigger, not editor patch:** the original design intercepted the editor button via Harmony, but third-party community mods (Harmony, UIExtenderEx, MCMv5, ButterLib) opt out of editor-mode activation by default and crash when forced to load there. Maintaining a per-dependency editor compatibility matrix is fragile. The MCM trigger runs against the same engine pathfinder via `Campaign.Current.MapSceneWrapper` and produces byte-equivalent output without the editor-mode prerequisites.

## Architecture

### Design Challenge

1. The cache class is `NavigationCache<T>` where `T : ISettlementDataHolder`. Runtime path uses `NavigationCache<Settlement>` via `SandBoxNavigationCache` (constructor reads `Campaign.Current.Models` for excluded face IDs + region-switch costs).
2. Vanilla's `AddClosestEntrancePairBase` does (A\* × 2 → write dict) as one unit. To parallelize the A\* but serialize the dict write, the per-pair work must be split — adapter exposes `ComputeClosestEntrancePair` (parallel-safe) + `WriteComputedPair` (lock-protected).
3. The native engine pathfinder (`Scene.GetPathDistanceBetweenAIFaces`) has no documented thread-safety guarantee. Vanilla battle code uses `ThreadLocal<NavigationPath>` to isolate output state — same pattern we copy. Verdict: YELLOW; gated behind a smoke-test that compares serial vs parallel pathfind outputs at build start.

### Solution Approach

MCM `SettingPropertyButton` invokes a boundary lambda that resolves `IRuntimeCacheRebuildService` from IoC. The service pre-flights `Campaign.Current` + `MapSceneWrapper`, acquires an Interlocked lock to prevent double-trigger, then spawns the build on `Task.Run`. `ICampaignSessionAdapter` wraps the construction of `SandBoxNavigationCache` (no direct TaleWorlds types leak into the service). `NavigationCacheAdapter` uses reflection to find the methods on `NavigationCache<T>` (the cache type chain is walked at construction; cached `MethodInfo` for the duration). `CacheBuilderService` runs Phase 0 + Phase 1 + Phase 2 with optional checkpoint resume and incremental filter. Output is written atomically via `File.Replace(tmp, final, .prev, ignoreMetadataErrors: true)` — single Win32 `ReplaceFile` call, atomically swaps and preserves the previous version as `.prev`. Round-trip verification re-deserializes the file and compares counts; failure gates the success popup and emits a red `InformationMessage` with restoration instructions.

### Component Diagram

```
MCM "Rebuild Now" button click
    ↓
TaomSettings.RebuildDistanceCacheAction (static lambda, try/catch boundary)
    ↓
IoC.Resolve<IRuntimeCacheRebuildService>().Trigger()
    ↓ [pre-flight: ICampaignSessionAdapter.IsReadyForRebuild + Interlocked lock]
RuntimeCacheRebuildService.Trigger
    ↓ [Task.Run on threadpool — background, non-blocking]
RuntimeCacheRebuildService.RunBuild
    ├─→ ICampaignSessionAdapter.CreateDefaultRuntimeCacheAdapter(logger)
    │     // wraps SandBoxNavigationCache(NavigationType.Default) in NavigationCacheAdapter
    ├─→ resolve output path, log env + scene CRCs + existing file diagnostics
    ├─→ CacheBuilderService.Build(adapter, ct)
    │     ├─→ Check checkpoint → maybe resume
    │     ├─→ SettlementDiffer.Compute → maybe incremental
    │     ├─→ SmokeTestGate.Run → maybe fall back to serial
    │     ├─→ Phase 1 (Serial or Parallel) with optional filter
    │     ├─→ CheckpointSerializer.Save (after Phase 1)
    │     ├─→ Phase 2 (Serial or Parallel)
    │     ├─→ CheckpointSerializer.Delete
    │     ├─→ SettlementSnapshotStore.Save (for next incremental)
    │     └─→ ValidationReportWriter.Write
    ├─→ Capture live distance count from adapter (handles resume mode)
    ├─→ WriteOutputAtomically (File.Replace .tmp → final, atomically preserving .prev)
    └─→ VerifyOutputRoundTrip → VerificationResult
          ├─ Ok=true → log "BUILD COMPLETE" + yellow InformationMessage
          └─ Ok=false → log "BUILD FAILED (verification)" + red InformationMessage
                       + .prev restoration instructions; abort without success popup
```

## Configuration

### Config file: `Main/_Module/ModuleData/configs/cache_rebuild_config.json`

Only fields that actually affect runtime behavior are shipped in the JSON. Reserved/scaffolding fields (for dropped or future-phase features) remain in `CacheRebuildConfig.cs` with defaults so the C# API stays stable — they're not exposed in JSON to avoid misleading users with knobs that silently do nothing.

| Field | Type | Description |
|---|---|---|
| `enabled` | bool | Master toggle (default `true`). Disabling routes back to vanilla. |
| `forceVanilla` | bool | Force vanilla path even with feature enabled (debug switch). |
| `parallelism` | int | `Parallel.For` max degree of parallelism. Range [1, ProcessorCount]. Default `4`. |
| `enableIncremental` | bool | Enable settlement-diff incremental Phase 1. Default `true`. |
| `incrementalMaxChanged` | int | Above this many added+moved+removed → force full rebuild. Range [0, 200]. Default `30`. |
| `enableCheckpoint` | bool | Save state after Phase 1, resume on crash. Default `true`. |
| `checkpointRelativeDirectory` | string | Where to put `.ckpt.bin`/`.ckpt.meta`. Default `TAOM_Map/ModuleData/DistanceCaches`. |
| `settlementSnapshotRelativePath` | string | Path to settlement snapshot for incremental. Default in TAOM_Map. |
| `validationReportRelativePath` | string | JSON report destination. Empty disables. |
| `smokeTestPairs` | int | Number of pairs to test at gate. Range [1, 100]. Default `10`. |
| `smokeTestDistanceTolerance` | float | Max acceptable serial-vs-parallel delta. Range [1e-8, 1e-2]. Default `1e-4`. |

All fields validated per `CLAUDE.md "Config Providers MUST Validate"` — invalid values revert to default with logged warning. NaN/Infinity rejected via `FiniteFloatValidator`.

**Reserved fields (in `CacheRebuildConfig.cs`, not in shipped JSON):** `checkpointEvery`, `enablePathReuse`, `enablePersistentPathCache`, `incrementalSpatialRadius`, `enableDebugQualityCheck`, `enableUiOverlay`, `phase1SkipReversePathfind`, `logVerbosity` — all correspond to dropped phases (Phase 9 spatial index, Phase 12 path reuse, Phase 13 multi-pass quality check, UI overlay) or features whose scope is mod-wide rather than per-feature. They'll be wired into JSON when a future phase actually consumes them.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/EditorCacheRebuild/IRuntimeCacheRebuildService.cs` + `RuntimeCacheRebuildService.cs` | MCM-trigger entry point. Pre-flight gates, background `Task.Run`, atomic write with `.prev` backup, round-trip verification (returns `VerificationResult` that gates the success popup), build-correlation-ID logging. |
| `Main/Adapters/ICampaignSessionAdapter.cs` + `CampaignSessionAdapter.cs` + `CampaignSnapshot.cs` | Adapter wrapping `Campaign.Current` checks, `SandBoxNavigationCache` construction, and diagnostic campaign-state snapshot. Keeps the service ADR-007 compliant (no TaleWorlds types in service body). |
| `Main/Features/TaomSettings.cs` (`RebuildDistanceCacheAction` property) | MCMv5 `SettingPropertyButton` — boundary. Static lambda that resolves `IRuntimeCacheRebuildService` from IoC and calls `Trigger()`, wrapped in try/catch with red `InformationMessage` on failure. |
| `Main/Features/EditorCacheRebuild/CacheBuilderService.cs` | Orchestrator. Mode selection, smoke test, checkpointing, validation report write. |
| `Main/Features/EditorCacheRebuild/Phase1/SerialPhase1Builder.cs` | Reference serial Phase 1 implementation |
| `Main/Features/EditorCacheRebuild/Phase1/ParallelPhase1Builder.cs` | `Parallel.For` + `ConcurrentBag` + locked-write Phase 1 |
| `Main/Features/EditorCacheRebuild/Phase2/SerialPhase2Builder.cs` | Reference serial Phase 2 implementation |
| `Main/Features/EditorCacheRebuild/Phase2/ParallelPhase2Builder.cs` | Parallel Phase 2 with buffered neighbor pair writes |
| `Main/Features/EditorCacheRebuild/Phase1/IPhase1Filter.cs` | `ChangedSettlementsFilter` for incremental — skips pairs not touching changed settlements |
| `Main/Features/EditorCacheRebuild/Validation/SmokeTestGate.cs` | Pre-build serial-vs-parallel correctness check |
| `Main/Features/EditorCacheRebuild/Validation/ValidationReportWriter.cs` | JSON report after every build |
| `Main/Features/EditorCacheRebuild/Checkpoint/CheckpointSerializer.cs` | Save / load / delete `.ckpt.bin` + `.ckpt.meta` between phases |
| `Main/Features/EditorCacheRebuild/Diff/SettlementSnapshotStore.cs` | JSON sidecar with previous settlement positions for incremental diff |
| `Main/Features/EditorCacheRebuild/Diff/SettlementDiffer.cs` | Compare snapshot vs current → `SettlementDiff{Added,Removed,Moved,ForcedFullRebuild}` |
| `Main/Features/EditorCacheRebuild/Caching/PathReuseCache.cs` | In-memory `ConcurrentDictionary<SortedPathKey, NavigationPath>` (reserved for path-reuse v2) |
| `Main/Features/EditorCacheRebuild/Caching/PersistentPathCache.cs` | On-disk `.paths.bin` sidecar with magic + version + CRC validation (reserved for path-reuse v2) |
| `Main/Adapters/INavigationCacheAdapter.cs` + `NavigationCacheAdapter.cs` | Reflection bridge to private nested `SettlementRecord` and `NavigationCache<>` generic |
| `Main/_Module/ModuleData/configs/cache_rebuild_config.json` | Default config |

## Dependencies

- `IPathService` (Core/Infrastructure) — derives Bannerlord install root from TAOM module path
- `IModLogger` (Core/Logging) — file logger
- `ICampaignSessionAdapter` — wraps `Campaign.Current` readiness checks + `SandBoxNavigationCache` construction
- MCMv5 (`Bannerlord.MBOptionScreen`) — `SettingPropertyButtonAttribute` for the MCM trigger
- TaleWorlds: `Campaign.Current`, `Campaign.Current.MapSceneWrapper`, `Settlement.All`, `MobileParty.NavigationType`, `NavigationCache<Settlement>` via `SandBoxNavigationCache` (CampaignSystem.dll), `NavigationPath` (Library.dll), `InformationManager.DisplayMessage` + `Colors` (Library.dll)

## Tests

`TAOM.Tests/Features/EditorCacheRebuild/` — 103+ tests covering:

- Config provider validation (NaN/Infinity/range guards) (20 tests)
- Path cache + persistent sidecar (24 tests)
- Phase 1 serial + parallel builder mock-driven correctness (15 tests)
- Phase 2 serial + parallel builder mock-driven correctness (12 tests)
- Smoke test gate skip/pass/fail paths (8 tests)
- Cache builder service mode selection + cancellation (5 tests)
- Validation report writer round-trip + edge cases (5 tests)
- Settlement diff + change filter (9 tests)
- Runtime cache rebuild service: gate logic, Interlocked lock, path resolution, atomic write with `File.Replace`, round-trip verification result type, neighbor symmetric-storage doubling (18 tests)

**Live-test only:** `NavigationCacheAdapter` reflection plumbing against the real `NavigationCache<Settlement>` instance; the `RunBuild` end-to-end orchestration (covered by individual unit tests on `WriteOutputAtomically` and `VerifyOutputRoundTrip` but not as a single integration). Verified by in-game MCM-trigger runs producing byte-equivalent caches.

## How To Build / Use

1. Launch Bannerlord normally (singleplayer/sandbox mode).
2. Load a save (or start a new campaign) so `Campaign.Current` is initialized.
3. Open **Options → Mod Options → TAOM → Map Tools / Distance Cache Rebuild** and click **Rebuild Now**.
4. `TaomSettings.RebuildDistanceCacheAction` (an MCMv5 `SettingPropertyButton` action) resolves `IRuntimeCacheRebuildService` from IoC and calls `Trigger()`.
5. `RuntimeCacheRebuildService` validates pre-flight via `ICampaignSessionAdapter.IsReadyForRebuild` (Campaign present, MapSceneWrapper present), atomically acquires the running-flag via `Interlocked.CompareExchange`, logs an environment snapshot with a unique 6-hex correlation ID, then spawns the build on a `Task.Run` background thread.
6. The background task asks the adapter to construct `SandBoxNavigationCache(MobileParty.NavigationType.Default)` wrapped in `NavigationCacheAdapter`, then calls `IDistanceCacheBuilderService.Build(adapter, ct)`.
7. `WriteOutputAtomically` writes to `<final>.tmp`, then `File.Replace(tmp, final, .prev, ignoreMetadataErrors: true)` — single atomic Win32 `ReplaceFile` operation that swaps the file and preserves the previous version as `.prev` in one filesystem transaction. (First-build case with no existing `final` falls back to `File.Move`.)
8. `VerifyOutputRoundTrip` re-deserializes the written file and compares distance + neighbor counts against the live state (with 10% tolerance, accounting for vanilla's symmetric neighbor storage). Returns `VerificationResult { Ok, Reason, ActualDistanceCount, ActualNeighborCount }`. On `Ok=false`, `RunBuild` emits a red `InformationMessage` with `.prev` restoration instructions and skips the success popup.
9. Reload the save (or start a new campaign) to pick up the new cache.

### How To Recover From A Crash

1. Restart Bannerlord and reload the same save.
2. Click **Rebuild Now** again.
3. If a valid `.ckpt.meta` exists for the current scene CRCs, the service auto-resumes from Phase 2 (Phase 1 state loaded from `.ckpt.bin`). The log line will say `RESUMING from checkpoint`.

### How To Force A Full Rebuild

Edit `cache_rebuild_config.json`:
- Set `enableIncremental: false` and `enableCheckpoint: false`

OR
- Delete any `.ckpt.*` and `settlements_snapshot.json` files from the cache directory

### How To Revert The Last Rebuild

The previous cache is preserved as `settlements_distance_cache_Default.bin.prev` after every successful run. To roll back: close Bannerlord, then rename `settlements_distance_cache_Default.bin.prev` → `settlements_distance_cache_Default.bin` (overwriting the post-rebuild file). The atomic-write transaction guarantees `.prev` is always the last known-good cache.

> **The backup sweep takes this file.** `.prev` is 9.9 MB and ships inside `TAOM_Map`, so
> `tools/sweep_module_backups.ps1` moves it to the quarantine along with every other sidecar (it did
> on 2026-09-01). That is correct before a release and wrong in the middle of cache work, because it
> closes the rollback window described above. **Rebuild first, sweep afterwards.** If a sweep already
> took it, the file is at
> `E:\Bannerlord_Backups\module_bak_sweep_<date>\TAOM_Map\ModuleData\DistanceCaches\`. See
> [module-backup-sweep](../reference/module-backup-sweep.md).

### How To Disable Entirely

Edit `cache_rebuild_config.json`: `"forceVanilla": true` or `"enabled": false`. Restart Bannerlord. The MCM button is still visible but the lambda's `Trigger()` call returns false with a yellow popup. Vanilla cache loading from disk is unaffected — the game continues to use whatever `.bin` is on disk.

## Performance

| Operation | Vanilla | This feature (measured on TAOM, 863 settlements, 4-way parallel) |
|---|---|---|
| Full rebuild (cold) | ~108 hr | ~7 min (Phase 1: 1m 27s for 371,953 entrance pairs; Phase 2: 5m 37s for 372 unique neighbor pairs) |
| Incremental, 1-5 moved settlements | ~108 hr | ~30 sec to ~2 min (target — depends on Phase 2 corridor scan cost) |
| Resume after Phase-1-completed crash | Lose everything (5+ days of work) | ~5 min remaining (Phase 2 only) |
| Navmesh edit + rebuild | ~108 hr (no detection) | Full ~7 min (CRC mismatch auto-detected, refuses stale incremental) |

**Why ~30 min and not 5 min:** Phase 2's corridor scan (vanilla `CheckBeingNeighbor`) re-pathfinds every fortification pair. A future optimization would memoize Phase 1's paths for Phase 2 reuse (scaffold is in `Caching/PathReuseCache.cs` + `PersistentPathCache.cs`, not yet wired into the builders). That alone is a 2-3× win on top of the current 6-8× parallelism win.

## v1.4.8 verification (2026-08-10)

v1.4.8 claims "significantly improved Settlement Distance Cache computation performance (~2× faster)" — the largest real change in that bump, landing squarely on the vanilla class this feature reflects into. Diffing the whole `NavigationCache<T>` body (`_shipping_build_v1.4.7` vs the fresh `_shipping_build`) produces exactly **two hunks**:

1. **`GetClosestSettlementToPosition` gained an optional `bool useEarlyOut = false`** plus a 22-line early-out that `break`s the candidate loop once a candidate's gate/port position sits further from `checkPosition` than `minPathScoreEverFound + 25` (compared squared; which position it uses is keyed on `_navigationType` and `HasPort`). **No shipped call site enables it** — `useEarlyOut` across the 56 shipping-client base assemblies, the 125 module assemblies and the editor base build returns only the declaration and its own body, so the fast path is dormant in managed code.
2. **A self-pair guard in `GenerateSettlementToSettlementDistanceCache`:** the `NavigationType.All` branch's `if (settlement.HasPort)` became `if (settlement.HasPort && i != j)`. Only that branch starts its inner loop at `j = i` (every other type starts at `i + 1`), so only it ever visits a self-pair, and the guard drops the port→gate self-entry.

**TAOM binds neither, and nothing here needed changing:**

- All **16** catalogued distance-cache reflection sites (`TAOM.Tests/Migration/ReflectionSiteBindingTests.cs:65-80`) name members that did not move, and every one still resolves — `BindingVerification` 106/106. `GetClosestSettlementToPosition` appears nowhere in `Main/` or `TAOM.Tests/` source.
- The `i != j` guard has no TAOM analogue to be missing. All four builders iterate `for (int j = i + 1; ...)` unconditionally (`Phase1/SerialPhase1Builder.cs:44`, `Phase1/ParallelPhase1Builder.cs:63`, `Phase2/SerialPhase2Builder.cs:36`, `Phase2/ParallelPhase2Builder.cs:60`), so a self-pair is never visited. Independently, TAOM only ever constructs `SandBoxNavigationCache(MobileParty.NavigationType.Default)` (`Main/Adapters/CampaignSessionAdapter.cs:33`) — the builders' `NavigationType.All` arm, which does mirror vanilla's port/gate combination logic, is unreachable until naval is wired ([#120](https://github.com/haterade22/TAOM/issues/120)).
- **The `.bin` format did not move.** `Serialize` and `Deserialize` diff to zero hunks, and the entire `SandBoxNavigationCache` class — `GetSceneXmlCrcValues` included — is identical between the two builds. The shipped `settlements_distance_cache_Default.bin` stays valid; no rebuild is forced by the format. Worth knowing while reading `Deserialize`: it reads the two stored CRC words and calls `MapSceneWrapper.GetSceneXmlCrc()` / `GetSceneNavigationMeshCrc()`, then **discards all four**. The engine never rejects a cache on a CRC mismatch — TAOM's own checkpoint resume is the only thing that compares them.

### What to watch on the first campaign load

v1.4.8 also claims to fix "Settlement Distance Cache computation failing silently on modded world maps; errors now reported." There is **no v1.4.7 baseline to diff** for the assembly that owns that path: `SandBox.View` was one of the 34 assemblies outside the decompile stack until this bump (see [`docs/migration/v1.4.8-impact.md`](../migration/v1.4.8-impact.md)). So what follows is v1.4.8 behavior read first-hand, not a delta.

- `SettlementPositionScript.OnInit` wraps the whole registration in `try/catch` and on any exception prints two red `Debug.Print` lines plus a `FailedAssert` warning that "cache will be initialized now."
- `ReadNavigationCacheForNavigationTypeOnGameLoad` scans **every active module** for `ModuleData/DistanceCaches/settlements_distance_cache_<NavigationType>.bin` and keeps the **last** match (no early break). Finding none it raises `Debug.FailedAssert("Navigation type with id … file is not found, this should not be happening, will generate cache (this will take some time)")` and runs `GenerateCacheData()` in-process — the ~108 hr vanilla path on TAOM's map.

**The naval-triple read is the live exposure.** `OnInit` computes `useNavalNavigation = GetMapIsNavalDLC() || (!GetMapIsSandBox() && ModuleHelper.IsModuleActive("NavalDLC"))`, and both flags come from a case-insensitive compare of the map's owning module id against `"NavalDLC"` / `"Sandbox"` — for a TAOM campaign that id is `TAOM_Map`, so both are false. **If NavalDLC is in the active module list, a TAOM campaign load reads three caches, not one**: `Default` (1), `Naval` (2) and `All` (3). The 1.4.8 update wrote NavigationType-keyed files for NavalDLC to match — `settlements_distance_cache_All.bin` (2026-08-10 07:22) and `_Naval.bin` (07:32) beside its older `_Default.bin` — while `TAOM_Map/ModuleData/DistanceCaches/` still ships **only** `settlements_distance_cache_Default.bin` (plus its `.prev`, `settlements_snapshot.json`, `last_rebuild_report.json`). Of the four `DistanceCaches` folders in the install, NavalDLC is the only one supplying `_Naval.bin` / `_All.bin`, so with it active those two lookups resolve to NavalDLC's files — caches keyed by settlement `StringId` and rehydrated through `Settlement.Find`, which returns `null` for any id not on TAOM's map. What that does at runtime is **unverified**; without NavalDLC active the same two lookups find nothing and take the generate-in-process branch instead. Building `Naval` + `All` for TAOM_Map is part of #120, and the Phase 1 builders already carry those cases.

## Changelog

- 2026-08-10 — **v1.4.8 engine bump: verified, no code change.** The engine's `NavigationCache<T>` speed rewrite touches two members (`GetClosestSettlementToPosition`'s dormant `useEarlyOut`, and an `i != j` self-pair guard in the `NavigationType.All` cache build); TAOM reflects on neither, all 16 catalogued reflection sites still resolve, and `Serialize`/`Deserialize` plus the whole `SandBoxNavigationCache` class are byte-identical — so the shipped cache stays valid and no rebuild is forced. Recorded two watch items for the first campaign load: the now-reported modded-map cache failure, and the `Naval`/`All` lookups a NavalDLC-active TAOM campaign performs against a `_Default.bin`-only `TAOM_Map`. See "v1.4.8 verification" above.
- 2026-05-13 — Removed the legacy editor-mode integration: deleted the `Patch37_CacheBuildOverride` Harmony patch (never functioned in singleplayer; editor mode crashed third-party mods) and simplified the feature to the single in-game MCM-trigger path.
- 2026-05-12 — Pivoted from editor-mode Harmony integration to an in-game MCM trigger: added `IRuntimeCacheRebuildService` + the `Map Tools / Distance Cache Rebuild` MCM button, building against the live campaign's `MapSceneWrapper` with atomic `.tmp → final` write and `.prev` backup.
- 2026-05-12 — Initial feature: parallel + incremental + resumable settlement distance cache builder (`Parallel.For` Phase 1/2, smoke-test gate, checkpointing, settlement-diff incremental, validation report), plus deep-review and Codex review #38 correctness fixes.

## GitHub Issue

- **Issue:** [#118](https://github.com/haterade22/TAOM/issues/118)
- **Status:** Open (implementation complete; Phase 14 integration test pending vanilla run completion)

## Migrated notes (from CLAUDE.md, 2026-07-12)

- **Logging detail (verified in `RuntimeCacheRebuildService.cs` + the parallel builders):** the comprehensive build logging includes **per-phase memory deltas** (`GC.GetTotalMemory` before/after Phase 1, Phase 2, and the overall build), **first-pair liveness heartbeats** (Phase 1 logs the FIRST entrance pair to confirm the pathfinder is reachable; Phase 2 logs the first neighbor-check to confirm the corridor-scan path), and **atomic-write integrity diagnostics** (resolved output path, output-directory existence, existing cache file size + last-modified, target-drive free space, and step-by-step `.tmp` + `File.Replace` logging) — in addition to the build-correlation IDs, environment snapshot, and scene CRCs already documented above.
- **Correction to the Key Files table:** both `ParallelPhase1Builder` and `ParallelPhase2Builder` buffer results in a **`ConcurrentQueue`** (not `ConcurrentBag` as the Phase 1 row above says) with locked dict writes — the source comment notes `ConcurrentQueue` has cheaper enumeration for the single-threaded post-loop flush.
- **NavalDLC port support is tracked at [#120](https://github.com/haterade22/TAOM/issues/120)** — the Phase 1 builders already carry `MobileParty.NavigationType.Naval` cases.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/file-catalogue.md](../modding/file-catalogue.md)
- [docs/modding/module-map.md](../modding/module-map.md)
- [docs/modding/recipe-new-mod-from-zero.md](../modding/recipe-new-mod-from-zero.md)
- [docs/modding/settlements.md](../modding/settlements.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/module-backup-sweep.md](../reference/module-backup-sweep.md)

<!-- backlinks-end -->
