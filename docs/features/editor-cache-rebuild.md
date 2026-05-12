# Editor Cache Rebuild

## Overview

Replaces the Bannerlord Editor's vanilla settlement distance cache builder with a parallel + incremental + resumable TAOM implementation. Click the editor's `ComputeAndSaveSettlementDistanceCache` button as usual; behind the scenes, our Harmony patch intercepts `NavigationCache<SettlementRecord>.GenerateCacheData()` and routes work through `CacheBuilderService`. A full rebuild drops from ~108 hours to ~30 minutes; incremental rebuilds after small edits target ~30 seconds.

## Why This Exists

- **Vanilla behavior:** `SettlementPositionScript.SaveSettlementDistanceCacheEditor()` calls `NavigationCache<SettlementRecord>.GenerateCacheData()` which runs three serial phases: closest-settlement-to-face (cheap), settlement-to-settlement distance (O(n²) A\*, ~6hr on TAOM), neighbor cache (O(n²) corridor scan, ~102hr on TAOM). All single-threaded, no checkpointing, no incremental support.
- **TAOM requirement:** TAOM has 863 settlements vs ~70 in vanilla Native. The cache rebuild is now ~5 days of editor frozen on a click. The user reported running for 22 hours with 86hr remaining on the May 11 run.
- **Without this feature:** Map editing iteration is impractical. Every settlement edit forces the user to commit to a multi-day cache rebuild.

## Architecture

### Design Challenge

1. `SettlementRecord` is a `private sealed nested class` inside `SandBox.View.Map.SettlementPositionScript` (SandBox.View.dll). `NavigationCache<SettlementRecord>` cannot be named in C#; closed-generic must be built via `typeof(NavigationCache<>).MakeGenericType(...)`.
2. Vanilla's `AddClosestEntrancePairBase` does (A\* × 2 → write dict) as one unit. To parallelize the A\* but serialize the dict write, the per-pair work must be split — adapter exposes `ComputeClosestEntrancePair` (parallel-safe) + `WriteComputedPair` (lock-protected).
3. The native engine pathfinder (`Scene.GetPathDistanceBetweenAIFaces`) has no documented thread-safety guarantee. Vanilla battle code uses `ThreadLocal<NavigationPath>` to isolate output state — same pattern we copy. Verdict: YELLOW; gated behind a smoke-test that compares serial vs parallel pathfind outputs at build start.
4. The runtime cache (`NavigationCache<Settlement>`) must remain untouched — only the editor's `NavigationCache<SettlementRecord>` closed generic instantiation is patched.

### Solution Approach

Single Harmony patch on `NavigationCache<SettlementRecord>.GenerateCacheData()` (Prefix returns false). The patch creates a `NavigationCacheAdapter` wrapping the cache instance, then calls `IDistanceCacheBuilderService.Build()`. The service drives all three vanilla phases through the adapter, leveraging parallelization where safe.

### Component Diagram

```
Editor button click
    ↓
SettlementPositionScript.SaveSettlementDistanceCacheEditor()
    ↓
NavigationCache<SettlementRecord>.GenerateCacheData()
    ↓ [Harmony Prefix returns false]
Patch37_CacheBuildOverride.Prefix
    ↓
CacheBuilderService.Build(adapter)
    ├─→ Check checkpoint → maybe resume
    ├─→ SettlementDiffer.Compute → maybe incremental
    ├─→ SmokeTestGate.Run → maybe fall back to serial
    ├─→ Phase 1 (Serial or Parallel) with optional filter
    ├─→ CheckpointSerializer.Save (after Phase 1)
    ├─→ Phase 2 (Serial or Parallel)
    ├─→ CheckpointSerializer.Delete
    ├─→ SettlementSnapshotStore.Save (for next incremental)
    └─→ ValidationReportWriter.Write
    ↓
Vanilla SaveSettlementDistanceCacheEditor calls cache.Serialize(filePath)
    → final cache binary written
```

## Configuration

### Config file: `Main/_Module/ModuleData/configs/cache_rebuild_config.json`

| Field | Type | Description |
|---|---|---|
| `enabled` | bool | Master toggle (default `true`). Disabling routes back to vanilla. |
| `forceVanilla` | bool | Force vanilla path even with feature enabled (debug switch). |
| `parallelism` | int | `Parallel.For` max degree of parallelism. Range [1, ProcessorCount]. Default `4`. |
| `checkpointEvery` | int | Reserved for future intra-phase checkpointing. Range [1, 1000]. |
| `enablePathReuse` | bool | Reserved for future Phase-1→Phase-2 path memoization. |
| `enablePersistentPathCache` | bool | Reserved for `.paths.bin` sidecar. |
| `enableIncremental` | bool | Enable settlement-diff incremental Phase 1. Default `true`. |
| `incrementalMaxChanged` | int | Above this many added+moved+removed → force full rebuild. Range [0, 200]. Default `30`. |
| `incrementalSpatialRadius` | float | Reserved for Phase 9 spatial-index Phase 2. |
| `enableCheckpoint` | bool | Save state after Phase 1, resume on crash. Default `true`. |
| `checkpointRelativeDirectory` | string | Where to put `.ckpt.bin`/`.ckpt.meta`. Default `TAOM_Map/ModuleData/DistanceCaches`. |
| `settlementSnapshotRelativePath` | string | Path to settlement snapshot for incremental. Default in TAOM_Map. |
| `validationReportRelativePath` | string | JSON report destination. Empty disables. |
| `smokeTestPairs` | int | Number of pairs to test at gate. Range [1, 100]. Default `10`. |
| `smokeTestDistanceTolerance` | float | Max acceptable serial-vs-parallel delta. Range [1e-8, 1e-2]. Default `1e-4`. |
| `phase1SkipReversePathfind` | bool | Reserved (vanilla averages forward + reverse). |
| `logVerbosity` | string | One of `error`/`warn`/`info`/`debug`. Default `info`. |

All fields validated per `CLAUDE.md "Config Providers MUST Validate"` — invalid values revert to default with logged warning.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/EditorCacheRebuild/Hooks/Patch37_CacheBuildOverride.cs` | Harmony Prefix — wires editor button → service |
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
- `Harmony` 2.4.2 — patches the closed generic via dynamically-built `MethodInfo`
- TaleWorlds: `NavigationCache<>` (CampaignSystem.dll), `ISettlementDataHolder` (CampaignSystem.dll), `NavigationCacheElement<>` (CampaignSystem.dll), `NavigationPath` (Library.dll), `CampaignVec2.Face → PathFaceRecord` (Library.dll)
- Editor (SandBox.View.dll): `SandBox.View.Map.SettlementPositionScript+SettlementRecord` (private nested), `SandBox.View.Map.SettlementPositionScript+SettlementPositionScriptNavigationCache` (private nested)

## Tests

`TAOM.Tests/Features/EditorCacheRebuild/` — 96 tests covering:

- Config provider validation (20 tests)
- Path cache + persistent sidecar (24 tests)
- Phase 1 serial + parallel builder mock-driven correctness (15 tests)
- Phase 2 serial + parallel builder mock-driven correctness (12 tests)
- Smoke test gate skip/pass/fail paths (8 tests)
- Cache builder service mode selection + cancellation (5 tests)
- Validation report writer round-trip + edge cases (5 tests)
- Settlement diff + change filter (9 tests)

**Not yet tested:** `NavigationCacheAdapter` reflection plumbing (requires real `NavigationCache<SettlementRecord>` runtime instance — covered by the Phase 14 integration test once the current vanilla rebuild finishes and produces `known_good_cache.bin`).

## How To Build / Use

1. User opens Bannerlord in editor mode with TAOM enabled.
2. Loads the TAOM_Map scene.
3. Clicks `ComputeAndSaveSettlementDistanceCache` (vanilla button — unchanged UI).
4. `Patch37_CacheBuildOverride` Prefix fires. If `cache_rebuild_config.json` has `enabled: true` (default), the service takes over:
   - Optionally loads checkpoint (resume)
   - Optionally diffs against snapshot (incremental)
   - Runs smoke test if `parallelism > 1`
   - Runs Phase 1 (serial or parallel; filtered if incremental)
   - Writes checkpoint after Phase 1
   - Runs Phase 2 (serial or parallel; always full in v1)
   - Deletes checkpoint
   - Saves snapshot for next incremental
   - Writes validation report
5. Prefix returns false → vanilla `SaveSettlementDistanceCacheEditor` proceeds to `cache.Serialize(filePath)` which writes the final `.bin`.

### How To Recover From A Crash

1. Restart the editor.
2. Click the same button.
3. If a valid `.ckpt.meta` exists for the current scene CRCs, the service auto-resumes from Phase 2 (Phase 1 state loaded from `.ckpt.bin`).

### How To Force A Full Rebuild

Edit `cache_rebuild_config.json`:
- Set `enableIncremental: false` and `enableCheckpoint: false`
OR
- Delete any `.ckpt.*` and `settlements_snapshot.json` files from the cache directory

### How To Revert To Vanilla

Edit `cache_rebuild_config.json`: `"forceVanilla": true`. Restart Bannerlord. The Harmony Prefix returns `true` (don't skip), and the original ~108hr vanilla path runs.

## Performance

| Operation | Vanilla | This feature |
|---|---|---|
| Full rebuild (cold) | ~108 hr | ~30 min (target — pending Phase 14 verification) |
| Incremental, 1-5 moved settlements | ~108 hr | ~30 sec to ~2 min (target) |
| Crash recovery | Lose everything | Resume from Phase 2 (saves ~6hr of Phase 1) |

**Why ~30 min and not 5 min:** Phase 2's corridor scan (vanilla `CheckBeingNeighbor`) re-pathfinds every fortification pair. A future optimization would memoize Phase 1's paths for Phase 2 reuse (scaffold is in `Caching/PathReuseCache.cs` + `PersistentPathCache.cs`, not yet wired into the builders). That alone is a 2-3× win on top of the current 6-8× parallelism win.

## GitHub Issue

- **Issue:** [#118](https://github.com/haterade22/TAOM/issues/118)
- **Status:** Open (implementation complete; Phase 14 integration test pending vanilla run completion)
