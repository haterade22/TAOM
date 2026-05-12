# Codex Adversarial Review — EditorCacheRebuild

**Feature:** TAOM-side parallel + incremental + resumable replacement for the Bannerlord Editor's settlement distance cache builder. Intercepts `NavigationCache<SettlementRecord>.GenerateCacheData()` via Harmony, routes through `CacheBuilderService` which drives `Parallel.For` over Phase 1 (settlement-to-settlement A*) and Phase 2 (fortification neighbor detection). The cache instance, `SettlementRecord`, and `SettlementPositionScriptNavigationCache` are all `private sealed nested` inside `SandBox.View.Map.SettlementPositionScript` so the entire adapter is reflection-driven.

**Date:** 2026-05-12
**Reviewer:** Codex (independent, no shared session context with Claude)
**Output target:** Paste your review back into this file or into `docs/reviews/codex-adversarial-editorcacherebuild-2026-05-12-review.md`.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid — use "dolguldur".

(This feature has zero kingdom/culture ID references — it's reflection + threading + serialization. The cheatsheet is included only because every TAOM review prompt includes it; you can ignore ID checks for this review.)

## READ FIRST

- `docs/features/editor-cache-rebuild.md` — full feature design + architecture + reservations
- `CHANGELOG.md` 2026-05-12 entry — what was built, prior `/deep-review` findings, post-review fixes
- `Main/_Module/ModuleData/configs/cache_rebuild_config.json` — feature config
- Vanilla reference (v1.4 decompile; v1.3.15 signatures verified via `ilspycmd` on installed DLLs):
  - `E:\Decompiled_Bannerlord\Modules\SandBox.View\SandBox.View.Map\SettlementPositionScript.cs` lines 70-500 (private nested cache subclass + private nested `SettlementRecord`) and lines 1157-1201 (`SaveSettlementDistanceCacheEditor`)
  - `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Map.DistanceCache\NavigationCache.cs` (base class — has Generate*, Add*, Set*, Serialize, Deserialize, _settlementToSettlementDistanceWithLandRatio, _fortificationNeighbors, _navigationType)
  - `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Map.DistanceCache\NavigationCacheElement.cs` (struct + Sort method)

## Known Suspects (CONFIRM or DISPUTE each)

The pre-commit `/deep-review` (5 Claude agents) already caught one CRITICAL bug and two cleanup items. Those are fixed. I want you to focus on what `/deep-review` may have missed.

1. **Stale Phase 2 dict on resume / incremental.** `CacheBuilderService.Build` calls `adapter.DeserializeCache(GetFinalCachePath(...))` on the incremental + resume paths. This populates BOTH `_settlementToSettlementDistanceWithLandRatio` AND `_fortificationNeighbors` from the prior `.bin`. Phase 2 (`SerialPhase2Builder` / `ParallelPhase2Builder`) then calls `adapter.AddNeighbor(s1, s2)` for newly-detected neighbors WITHOUT first clearing `_fortificationNeighbors`. Vanilla `GenerateNeighborSettlementsCache` starts with `_fortificationNeighbors.Clear();` (per `NavigationCache.cs:351`). My hypothesis: on resume or incremental rebuilds, Phase 2 appends to stale neighbor data, producing duplicates in the dict and ultimately in the serialized output. Read `NavigationCache.cs` to confirm Clear behavior, then check if our Phase 2 needs an analogous reset. **PROVE OR DISPUTE.**

2. **Other reflection field/property mismatches.** Pre-review caught `_navigationType` was a property, not a field. Adapter still uses `GetField` on `_settlementToSettlementDistanceWithLandRatio` and `_fortificationNeighbors`. Decompile v1.3.15 `NavigationCache<T>` via `ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache\`1"` and verify these two are actually FIELDS (not auto-properties with `_xxx` naming). The MaximumDistanceBetweenTwoConnectedSettlements is a known property — confirms the auto-property-with-underscore-name pattern is in play. Check the other two carefully. **PROVE OR DISPUTE.**

3. **ThreadStatic argument pool reentrancy.** `NavigationCacheAdapter` uses `[ThreadStatic] object[] _args2/3/4` (added today to eliminate ~2.2M allocations per build). Claim: no reflection target invokes a callback that re-enters the adapter on the same thread. Verify by tracing each reflected method's body: `AddClosestEntrancePairBase`, `GetCacheElement(T,bool)`, `GetRealDistanceAndLandRatioBetweenSettlements`, `SetSettlementToSettlementDistanceWithLandRatio`, `CheckBeingNeighbor(List,T,T)`, `AddNeighbor(T,T)`, `Sort` (static, on the struct), `GenerateClosestSettlementToFaceCache`. Does any of them invoke another method that fires our Harmony patch or otherwise re-enters `NavigationCacheAdapter`? **PROVE OR DISPUTE.**

4. **Patch37 `Prepare()` ordering with module load.** `Patch37_CacheBuildOverride.Prepare()` returns `Type.GetType("SandBox.View.Map.SettlementPositionScript+SettlementRecord, SandBox.View") != null`. `Prepare` runs during `_harmony.PatchCategory("Patch37_EditorCacheRebuild")` which fires inside TAOM's `OnSubModuleLoad`. SandBox.View module — when is its DLL actually loaded into the AppDomain relative to TAOM? If TAOM's SubModule fires before SandBox.View's, `Type.GetType` returns null, `Prepare` returns false, patch is SILENTLY skipped — feature is a no-op forever, no error logged. Check the SubModule.xml load order or test empirically. If risk exists, suggest deferring patch application to `OnGameInitializationFinished` or a later lifecycle event when View assemblies are guaranteed loaded. **PROVE OR DISPUTE.**

5. **Editor `CampaignVec2.Face` access NRE risk.** `SettlementSnapshotStore.Save` reads `s.GatePosition.Face.FaceIndex`. `CampaignVec2.Face` is a computed property that lazy-initializes via `Campaign.Current.MapSceneWrapper.GetFaceIndex(this)` (see `CampaignVec2.cs:30-40`). In editor mode (the only context our patch fires), is `Campaign.Current` guaranteed non-null and `MapSceneWrapper` initialized? Vanilla's `SettlementPositionScriptNavigationCache.GetRealDistanceAndLandRatioBetweenSettlements` calls `settlement1.PortPosition.ToVec2()` etc — implying `.Face` resolution works in this context. But our snapshot save runs AFTER the vanilla build returns; is the context still valid? **PROVE OR DISPUTE.**

6. **Patch37 + vanilla Serialize chain.** Vanilla `SaveSettlementDistanceCacheEditor` at `SettlementPositionScript.cs:1185-1187` calls `cache.GenerateCacheData()` then `cache.Serialize(filePath)`. Our Patch37 Prefix returns false → skips GenerateCacheData body only. Vanilla's Serialize still runs on the mutated cache. **PRE-REVIEW NOTE:** Agent 5 of the prior `/deep-review` claimed Serialize is unreachable — I disputed that as a false positive. Verify my dispute is correct. Read the vanilla method end-to-end. **PROVE OR DISPUTE.**

7. **`SortedPathKey` sort order match with vanilla.** Pre-review caught that `SortedPathKey` had inverted sort order vs vanilla `NavigationCacheElement<T>.Sort` (gate-first vs port-first when ids match). Today's fix:
```csharp
int cmp = string.Compare(id1, id2, StringComparison.Ordinal);
bool swap = cmp >= 0 && (cmp != 0 || !isPort1);
```
Vanilla:
```csharp
int num = string.Compare(settlement1.StringId, settlement2.StringId, StringComparison.Ordinal);
if (num >= 0 && (num != 0 || !settlement1.IsPortUsed))
{
    // swap settlement1 and settlement2
    isPairChanged = true;
}
```
Identical. But: do my tests actually exercise the case `id1 == id2 && isPort1 == isPort2`? That's the degenerate input. Should it be rejected? **PROVE OR DISPUTE the equivalence + flag any degenerate cases.**

## File Inventory

### Production (under `Main/Features/EditorCacheRebuild/` + adapters)

Adapters:
- `Main/Adapters/IEditorSceneAdapter.cs` (interface only — no impl — RESERVED scaffold per design doc)
- `Main/Adapters/INavigationCacheAdapter.cs`
- `Main/Adapters/NavigationCacheAdapter.cs` ← MOST RISK (reflection-heavy)

Orchestration:
- `Main/Features/EditorCacheRebuild/CacheBuilderService.cs` ← second-most risk (mode selection, checkpoint, snapshot, incremental)
- `Main/Features/EditorCacheRebuild/IDistanceCacheBuilderService.cs`
- `Main/Features/EditorCacheRebuild/CacheRebuildConfig.cs`
- `Main/Features/EditorCacheRebuild/CacheRebuildConfigProvider.cs`
- `Main/Features/EditorCacheRebuild/ICacheRebuildConfigProvider.cs`
- `Main/Features/EditorCacheRebuild/CacheElementKey.cs`
- `Main/Features/EditorCacheRebuild/EditorCacheRebuildIoC.cs`

Phase 1 / Phase 2 builders:
- `Main/Features/EditorCacheRebuild/Phase1/IPhase1Builder.cs`
- `Main/Features/EditorCacheRebuild/Phase1/IPhase1Filter.cs`
- `Main/Features/EditorCacheRebuild/Phase1/SerialPhase1Builder.cs`
- `Main/Features/EditorCacheRebuild/Phase1/ParallelPhase1Builder.cs`
- `Main/Features/EditorCacheRebuild/Phase2/IPhase2Builder.cs`
- `Main/Features/EditorCacheRebuild/Phase2/SerialPhase2Builder.cs`
- `Main/Features/EditorCacheRebuild/Phase2/ParallelPhase2Builder.cs`

Caching (scaffold — registered in IoC, NOT yet consumed by builders):
- `Main/Features/EditorCacheRebuild/Caching/SortedPathKey.cs` ← had inverted sort order, fixed today
- `Main/Features/EditorCacheRebuild/Caching/NavigationPathCloner.cs`
- `Main/Features/EditorCacheRebuild/Caching/IPathReuseCache.cs`
- `Main/Features/EditorCacheRebuild/Caching/PathReuseCache.cs`
- `Main/Features/EditorCacheRebuild/Caching/IPersistentPathCache.cs`
- `Main/Features/EditorCacheRebuild/Caching/PersistentPathCache.cs`

Checkpoint:
- `Main/Features/EditorCacheRebuild/Checkpoint/CheckpointMetadata.cs`
- `Main/Features/EditorCacheRebuild/Checkpoint/ICheckpointSerializer.cs`
- `Main/Features/EditorCacheRebuild/Checkpoint/CheckpointSerializer.cs`

Diff (incremental rebuild):
- `Main/Features/EditorCacheRebuild/Diff/SettlementSnapshot.cs`
- `Main/Features/EditorCacheRebuild/Diff/ISettlementSnapshotStore.cs`
- `Main/Features/EditorCacheRebuild/Diff/SettlementSnapshotStore.cs`
- `Main/Features/EditorCacheRebuild/Diff/ISettlementDiffer.cs`
- `Main/Features/EditorCacheRebuild/Diff/SettlementDiffer.cs`
- `Main/Features/EditorCacheRebuild/Diff/SettlementDiff.cs`

Validation:
- `Main/Features/EditorCacheRebuild/Validation/ISmokeTestGate.cs`
- `Main/Features/EditorCacheRebuild/Validation/SmokeTestGate.cs`
- `Main/Features/EditorCacheRebuild/Validation/IValidationReportWriter.cs`
- `Main/Features/EditorCacheRebuild/Validation/ValidationReportWriter.cs`
- `Main/Features/EditorCacheRebuild/Validation/ValidationReport.cs`

Logging:
- `Main/Features/EditorCacheRebuild/Progress/ProgressLogger.cs`

Harmony entry:
- `Main/Features/EditorCacheRebuild/Hooks/Patch37_CacheBuildOverride.cs`

Modified shared files:
- `Main/IoC.cs` (just adds `EditorCacheRebuildIoC.RegisterEditorCacheRebuildFeature(container)`)
- `Main/SubModule.cs` (just adds `_harmony.PatchCategory("Patch37_EditorCacheRebuild")`)

Config:
- `Main/_Module/ModuleData/configs/cache_rebuild_config.json`

### Tests (`TAOM.Tests/Features/EditorCacheRebuild/`)

- `CacheBuilderServiceTests.cs`
- `CacheRebuildConfigProviderTests.cs`
- `Caching/{NavigationPathCloner,PathReuseCache,PersistentPathCache,SortedPathKey}Tests.cs`
- `Diff/SettlementDiffTests.cs`
- `Phase1/{ChangedSettlementsFilter,SerialPhase1Builder,ParallelPhase1Builder}Tests.cs`
- `Phase2/{SerialPhase2Builder,ParallelPhase2Builder}Tests.cs`
- `Validation/{SmokeTestGate,ValidationReportWriter}Tests.cs`

Total: 96 tests, all pass. **Missing dedicated tests** (deferred to Phase 14 integration): `NavigationCacheAdapter`, `SettlementDiffer`, `CheckpointSerializer`, `SettlementSnapshotStore`. Coverage flows through mocked-adapter unit tests + the orchestrator integration test path.

## VANILLA CODE (decompile and verify)

You MUST decompile these targets via `ilspycmd` against the INSTALLED v1.3.15 DLL — `E:\Decompiled_Bannerlord\` is v1.4 and signatures may differ.

```
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1"
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCacheElement`1"
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBox/bin/Win64_Shipping_Client/SandBox.View.dll" -t "SandBox.View.Map.SettlementPositionScript+SettlementPositionScriptNavigationCache"
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBox/bin/Win64_Shipping_Client/SandBox.View.dll" -t "SandBox.View.Map.SettlementPositionScript"
```

Paste the verbatim signatures for:
- `NavigationCache<T>._settlementToSettlementDistanceWithLandRatio` (field or property?)
- `NavigationCache<T>._fortificationNeighbors` (field or property?)
- `NavigationCache<T>.GenerateNeighborSettlementsCache()` body — specifically the first line(s); does it `_fortificationNeighbors.Clear()` ?
- `NavigationCache<T>.AddNeighbor(T,T)` body — does it dedupe?
- `NavigationCacheElement<T>.Sort(ref, ref, out)` body — full source
- `SettlementPositionScript.SaveSettlementDistanceCacheEditor` lines 1170-1200 — the foreach with try/catch/finally around the cache build

## Feature-Specific Deep Analysis

### Adapter reflection-resolution fidelity

For each reflected member in `NavigationCacheAdapter.cs`, verify the lookup strategy is robust:
- `_settlementToSettlementDistanceWithLandRatio` — currently `GetField("...", AnyInstance)`. If v1.3.15 made it a property (auto-property with `_xxx` style name), this throws.
- `_fortificationNeighbors` — same concern.
- `_navigationType` — confirmed property in v1.3.15, fix applied. Verify the fix uses `GetProperty` + `PropertyInfo.GetValue` correctly.
- `_getCacheElementByRecord` — discriminated by `(SettlementRecord, bool)` param types. There's also a `GetCacheElement(string)` overload on the abstract base — confirm our binding finds the right one.
- `_getRealDistance` — discriminated by 3 parameters with element[0] and element[1] being `NavigationCacheElement<SettlementRecord>`. The abstract on the base has the same shape; our finder matches by param-type. OK?
- `_addClosestEntrancePairBase` — `private` on base. `BindingFlags.Instance | Public | NonPublic` finds it.
- `_setSettlementDistance` — `protected` on base. Same binding flags.
- `_checkBeingNeighbor2Arg` — finder filters by 3 params + first is `IList`. Vanilla has the 3-param `protected bool CheckBeingNeighbor(List<T>, T, T)` plus the 6-param abstract `CheckBeingNeighbor(List<T>, T, T, bool, bool, out float)`. Our filter correctly returns the 3-param.
- `_getSceneCrcValues` — looked up on `concreteType` (the subclass), not base. The base may declare it abstract; the subclass overrides public. Confirm the lookup finds the override.

### Threading

- `Parallel.For` in Phase 1 uses `MaxDegreeOfParallelism = config.Parallelism`.
- The reflection methods are invoked via `ThreadStatic` arg arrays. Each thread reuses its own arrays. No callbacks per claim.
- `WriteComputedPair` takes `_writeLock` — but writes happen single-threaded after Parallel.For. Lock is defensive.
- The `Sort` method invocation reuses the same `Args3()` array sequentially within `ComputeClosestEntrancePair`. Each call reads-then-overwrites; pattern is safe iff no nested reentry.
- `Interlocked.Add(ref pairsComputed, ...)` in Phase 1 parallel — yes, this is needed.

### Cancellation

- `CancellationToken` flows from `Patch37` → `service.Build(adapter, CancellationToken.None)`. **Question:** Patch37 passes `None`. Is there ANY path to cancel a running build? If not, the cancellation infrastructure throughout is dead unless we expose a cancel hotkey or external trigger. Today there is none — this is OK for v1 but flag if it implies dead code.
- `Parallel.For` propagates cancellation via `ParallelOptions.CancellationToken`. The catch block re-throws so `CacheBuilderService.Build`'s outer catch captures it and sets `Cancelled=true`.
- Resume path: if cancelled mid-Phase-2, the checkpoint from Phase-1 stays (no cleanup) — so the next run resumes from Phase 2. OK by design.

### Checkpoint correctness

- `CheckpointSerializer.Save` writes `.ckpt.bin` (via vanilla `adapter.SerializeCache`) + `.ckpt.meta` (JSON).
- On load, validates sceneCrc + navMeshCrc + navigationType.
- If valid: calls `adapter.DeserializeCache(binPath)` to populate the cache from the partial state.
- THEN: Phase 1 is skipped; Phase 2 runs.
- **Question:** Phase 2 reads from `_fortificationNeighbors` (per `GenerateNeighborSettlementsCache`) which is now stale from the previous build's data. Are dupes prevented? See Suspect 1.

### Incremental rebuild correctness

- `SettlementDiffer.Compute` detects added/moved/removed by `StringId` + position + flags.
- If `TotalChanged > IncrementalMaxChanged` → falls back to full rebuild.
- Otherwise: `adapter.DeserializeCache(...)` populates from old cache → `ChangedSettlementsFilter` skips pairs not involving any changed settlement → Phase 1 ONLY recomputes affected pairs.
- **Question:** After Phase 1 (filtered), unchanged pairs still hold OLD distances. If a "moved" settlement also has unchanged pairs as a neighbor... wait the filter says ANY pair touching a changed settlement → recompute. So pairs touching the moved settlement DO recompute. Other pairs use old distances. OK.
- **Phase 2 always runs full.** It calls `GetFortificationsForNeighborDetection` which calls `GetUpdatedSettlementsForNeighborDetection` on the cache. Then for each fortification pair, `CheckBeingNeighbor`. Then `AddNeighbor`. Does it clear `_fortificationNeighbors` first? See Suspect 1.

### Config validation edge cases

`CacheRebuildConfigProvider.Validate` revert-to-default rules:
- `parallelism` outside [1, ProcessorCount] → revert
- `checkpointEvery` outside [1, 1000] → revert (but this field is UNUSED — orphan)
- `incrementalMaxChanged` outside [0, 200] → revert
- `incrementalSpatialRadius` outside [0.1, 100.0] → revert (UNUSED — orphan)
- `smokeTestPairs` outside [1, 100] → revert
- `smokeTestDistanceTolerance` outside [1e-8, 1e-2] → revert
- `logVerbosity` not in [error,warn,info,debug] → revert (UNUSED — orphan)

Orphan config fields are documented in the deep-review as "v2-reserved scaffolding." Flag if you disagree.

## CONFIG CROSS-REFERENCE

`Main/_Module/ModuleData/configs/cache_rebuild_config.json`:

```json
{
  "enabled": true,
  "forceVanilla": false,
  "parallelism": 4,
  "checkpointEvery": 20,
  "enablePathReuse": true,
  "enablePersistentPathCache": true,
  "enableIncremental": true,
  "incrementalMaxChanged": 30,
  "incrementalSpatialRadius": 5.0,
  "enableDebugQualityCheck": false,
  "enableUiOverlay": true,
  "smokeTestPairs": 10,
  "smokeTestDistanceTolerance": 0.0001,
  "phase1SkipReversePathfind": false,
  "validationReportRelativePath": "TAOM_Map/ModuleData/DistanceCaches/last_rebuild_report.json",
  "enableCheckpoint": true,
  "checkpointRelativeDirectory": "TAOM_Map/ModuleData/DistanceCaches",
  "settlementSnapshotRelativePath": "TAOM_Map/ModuleData/DistanceCaches/settlements_snapshot.json",
  "logVerbosity": "info"
}
```

Cross-reference every field to its consumer. Flag any field that's validated but has no read site (deep-review found 8 such fields — verify).

## FINDINGS OR OBSERVATIONS

Use this structure:

```
### Finding N: [title]
**Severity:** P1 / P2 / P3
**File(s):** path:line
**What:** [bug description]
**Why:** [why it's broken, with code citations]
**Evidence:**
  TAOM source:
  ```csharp
  [verbatim block]
  ```
  Vanilla:
  ```csharp
  [verbatim block from ilspycmd output]
  ```
**Fix:** [concrete change]
```

If the suspect is NOT actually a bug, mark it DISPUTED and explain why with citations.

## QUALITY GATES

- [ ] Decompiled v1.3.15 NavigationCache via `ilspycmd` (not v1.4 decompile folder)
- [ ] Verified `_settlementToSettlementDistanceWithLandRatio` is a field vs property
- [ ] Verified `_fortificationNeighbors` is a field vs property
- [ ] Verified vanilla `GenerateNeighborSettlementsCache` Clear behavior
- [ ] Verified vanilla `SaveSettlementDistanceCacheEditor` calls `Serialize` AFTER `GenerateCacheData`
- [ ] All Known Suspects answered with CONFIRMED or DISPUTED (with citations)
- [ ] Each finding has both TAOM source and vanilla source quoted

## Prior review lessons

SUCCESSES from past TAOM Codex reviews:
- Vanilla decompilation caught missing safety gates (replicate-vanilla-safety rule)
- Config ID cross-reference caught `rohan` / `dol_guldur` typos
- Lifecycle tracing caught stale caches

FAILURES Codex has produced in past TAOM reviews:
- Assumed `empire = Rohan` (it is Dunland)
- Flagged vanilla-matching code as bugs (when our patch was correct)
- Skipped hard sections marked TODO

This review is unusually reflection-heavy. The highest-value findings will likely be field-vs-property mismatches in the reflected base type (Suspect 2) and the stale Phase 2 dict (Suspect 1).

## Constraints

- Test framework is MSTest + NSubstitute (no Moq).
- Target framework `net472` so no `IReadOnlySet<>` etc.
- DryIoc for IoC.
- Harmony 2.4.2.
- `SettlementRecord` is `private sealed nested` in `SandBox.View.Map.SettlementPositionScript` — cannot be named in C#, hence the reflection.
