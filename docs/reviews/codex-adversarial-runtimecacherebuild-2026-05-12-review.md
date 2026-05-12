# Codex Adversarial Review - RuntimeCacheRebuild MCM Pivot

Date: 2026-05-12
Reviewer: Codex, independent pass
Scope: `a502ade..HEAD` (`646484b`, `024e9e9`, `6230c0c`)
Verdict: ISSUES FOUND

## Quality Gates

- Read scoped diff via `git diff a502ade..HEAD`: DONE
- Decompiled installed v1.3.15 `TaleWorlds.CampaignSystem.dll` via `ilspycmd`: DONE
- Decompiled MCMv5 5.11.3 local NuGet DLL for button persistence behavior: DONE
- Reviewed required TAOM files and new tests: DONE
- `dotnet test TAOM.Tests --no-restore`: BLOCKED by sandbox. Even with `DOTNET_CLI_HOME` redirected into the workspace, MSBuild failed on `C:\Users\mikew\AppData\Local\Microsoft SDKs` access.

## Vanilla Code

Source: `ilspycmd -t 'TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache\`1' 'E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll'`

`NavigationCache<T>.AddNeighbor` confirms symmetric storage:

```csharp
protected void AddNeighbor(T settlement1, T settlement2)
{
    bool flag = false;
    foreach (KeyValuePair<T, MBReadOnlyList<T>> fortificationNeighbor in _fortificationNeighbors)
    {
        if ((fortificationNeighbor.Key.StringId.Equals(settlement1.StringId) && fortificationNeighbor.Value.Contains(settlement2)) || (fortificationNeighbor.Key.StringId.Equals(settlement2.StringId) && fortificationNeighbor.Value.Contains(settlement1)))
        {
            flag = true;
            break;
        }
    }
    if (!flag)
    {
        if (!_fortificationNeighbors.TryGetValue(settlement1, out var value))
        {
            _fortificationNeighbors.Add(settlement1, new MBReadOnlyList<T>());
        }
        MBList<T> mBList;
        if (value != null)
        {
            mBList = new MBList<T>(value.Count + 1);
            mBList.AddRange(value);
        }
        else
        {
            mBList = new MBList<T>(1);
        }
        mBList.Add(settlement2);
        _fortificationNeighbors[settlement1] = mBList;
        if (!_fortificationNeighbors.TryGetValue(settlement2, out var value2))
        {
            _fortificationNeighbors.Add(settlement2, new MBReadOnlyList<T>());
        }
        if (value2 != null)
        {
            mBList = new MBList<T>(value2.Count + 1);
            mBList.AddRange(value2);
        }
        else
        {
            mBList = new MBList<T>(1);
        }
        mBList.Add(settlement1);
        _fortificationNeighbors[settlement2] = mBList;
    }
}
```

`NavigationCache<T>.Deserialize` is count-driven. It throws on normal truncation when `BinaryReader` cannot finish a declared record, but it does not compare CRCs and it accepts any structurally valid lower counts:

```csharp
public void Deserialize(string path)
{
    Debug.Print("Reading SettlementsDistanceCacheFilePath: " + path);
    System.IO.BinaryReader binaryReader = new System.IO.BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read));
    binaryReader.ReadUInt32();
    binaryReader.ReadUInt32();
    Campaign.Current.MapSceneWrapper.GetSceneXmlCrc();
    Campaign.Current.MapSceneWrapper.GetSceneNavigationMeshCrc();
    int num = binaryReader.ReadInt32();
    _settlementToSettlementDistanceWithLandRatio = new Dictionary<NavigationCacheElement<T>, Dictionary<NavigationCacheElement<T>, (float, float)>>(num);
    for (int i = 0; i < num; i++)
    {
        T cacheElement = GetCacheElement(binaryReader.ReadString());
        bool isPortUsed = binaryReader.ReadBoolean();
        NavigationCacheElement<T> settlement = GetCacheElement(cacheElement, isPortUsed);
        int num2 = binaryReader.ReadInt32();
        _settlementToSettlementDistanceWithLandRatio.Add(settlement, new Dictionary<NavigationCacheElement<T>, (float, float)>(num2));
        for (int j = 0; j < num2; j++)
        {
            T cacheElement2 = GetCacheElement(binaryReader.ReadString());
            bool isPortUsed2 = binaryReader.ReadBoolean();
            NavigationCacheElement<T> settlement2 = GetCacheElement(cacheElement2, isPortUsed2);
            NavigationCacheElement<T>.Sort(ref settlement, ref settlement2, out var _);
            float distance = binaryReader.ReadSingle();
            float landRatio = ((_navigationType == MobileParty.NavigationType.Naval) ? 0f : 1f);
            if (_navigationType == MobileParty.NavigationType.All)
            {
                landRatio = binaryReader.ReadSingle();
            }
            SetSettlementToSettlementDistanceWithLandRatio(settlement, settlement2, distance, landRatio);
        }
    }
    int num3 = binaryReader.ReadInt32();
    _fortificationNeighbors = new Dictionary<T, MBReadOnlyList<T>>(num3);
    for (int k = 0; k < num3; k++)
    {
        T cacheElement3 = GetCacheElement(binaryReader.ReadString());
        T cacheElement4 = GetCacheElement(binaryReader.ReadString());
        AddNeighbor(cacheElement3, cacheElement4);
    }
    int num4 = binaryReader.ReadInt32();
    _closestSettlementsToFaceIndices = new Dictionary<int, NavigationCacheElement<T>>(num4);
    for (int l = 0; l < num4; l++)
    {
        int faceId = binaryReader.ReadInt32();
        T cacheElement5 = GetCacheElement(binaryReader.ReadString());
        bool isPortUsed3 = binaryReader.ReadBoolean();
        NavigationCacheElement<T> cacheElement6 = GetCacheElement(cacheElement5, isPortUsed3);
        SetClosestSettlementToFaceIndex(faceId, cacheElement6);
    }
    binaryReader.Close();
}
```

Source: `ilspycmd -t 'TaleWorlds.CampaignSystem.Map.DistanceCache.SandBoxNavigationCache' ...`

`SandBoxNavigationCache` constructor confirms the runtime adapter needs a fully initialized campaign model set, not just `MapSceneWrapper`:

```csharp
private IMapScene MapSceneWrapper => Campaign.Current.MapSceneWrapper;

public SandBoxNavigationCache(MobileParty.NavigationType navigationType)
    : base(navigationType)
{
    _excludedFaceIds = Campaign.Current.Models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType(base._navigationType);
    _regionSwitchCostTo0 = Campaign.Current.Models.MapDistanceModel.RegionSwitchCostFromLandToSea;
    _regionSwitchCostTo1 = Campaign.Current.Models.MapDistanceModel.RegionSwitchCostFromSeaToLand;
}
```

## File Lists

New services/adapters reviewed:

- `Main/Adapters/ICampaignSessionAdapter.cs`
- `Main/Adapters/CampaignSessionAdapter.cs`
- `Main/Adapters/CampaignSnapshot.cs`
- `Main/Features/EditorCacheRebuild/IRuntimeCacheRebuildService.cs`
- `Main/Features/EditorCacheRebuild/RuntimeCacheRebuildService.cs`

Modified files reviewed:

- `Main/Features/TaomSettings.cs`
- `Main/Features/EditorCacheRebuild/EditorCacheRebuildIoC.cs`
- `Main/Features/EditorCacheRebuild/Hooks/Patch37_CacheBuildOverride.cs`
- `Main/Features/EditorCacheRebuild/Phase1/ParallelPhase1Builder.cs`
- `Main/Features/EditorCacheRebuild/Phase2/ParallelPhase2Builder.cs`
- `Main/Features/EditorCacheRebuild/Validation/SmokeTestGate.cs`
- `Main/IoC.cs`
- `Main/SubModule.cs`
- `Main/_Module/SubModule.xml`

Tests reviewed:

- `TAOM.Tests/Features/EditorCacheRebuild/RuntimeCacheRebuildServiceTests.cs`

Supporting files read:

- `CLAUDE.md`
- `docs/features/editor-cache-rebuild.md`
- `CHANGELOG.md`
- `Main/_Module/ModuleData/configs/cache_rebuild_config.json`
- `Main/Features/EditorCacheRebuild/CacheBuilderService.cs`
- `Main/Features/EditorCacheRebuild/CacheRebuildConfig.cs`
- `Main/Features/EditorCacheRebuild/CacheRebuildConfigProvider.cs`
- `Main/Adapters/NavigationCacheAdapter.cs`

## Known Suspects

1. **Race condition in `_runningFlag`: DISPUTED.** `Trigger()` uses `Interlocked.CompareExchange(ref _runningFlag, 1, 0)` at `RuntimeCacheRebuildService.cs:66`, and the `finally` uses `Volatile.Write(ref _runningFlag, 0)` at `RuntimeCacheRebuildService.cs:187`. ECMA-335 volatile read/write semantics are acquire/release, and `Interlocked` read-modify-write operations are atomic full-fence operations on .NET Framework. A thread that observes `IsRunning == false` can still lose the next `CompareExchange` to another racing trigger, but that is a real race with another caller, not a stale read.

2. **`SpawnBuild` overrideability: DISPUTED.** The `internal virtual SpawnBuild` seam at `RuntimeCacheRebuildService.cs:84` is acceptable for this service. A `Func<Action, Task>` or task-runner dependency would make the production constructor noisier only to test a one-line `Task.Run`. The current seam is narrow, internal, and does not leak into the interface. The separate problem is coverage: tests stop at the seam and never exercise `RunBuild` (see P3-2).

3. **Path resolution under non-Windows paths: DISPUTED.** `Path.GetFullPath(Path.Combine(_pathService.ModuleRootPath, ".."))` at `RuntimeCacheRebuildService.cs:282` is valid for Bannerlord's .NET Framework 4.7.2 Windows runtime. `".."` traversal is not Windows-specific. Wine/macOS/Linux host concerns are theoretical because the managed runtime here still sees Windows path semantics.

4. **Resume verification with `PairsComputed == 0`: CONFIRMED (P2-1).** Skipping the distance-count check avoids a false error in legitimate resume mode, but it also blinds verification to structurally valid low-count output. Vanilla `Deserialize` throws on ordinary EOF truncation, but it accepts a file whose declared distance count is low.

5. **Neighbor count assumption: CONFIRMED.** Vanilla `AddNeighbor` writes both `settlement1 -> settlement2` and `settlement2 -> settlement1`, and `Serialize` writes the sum of every neighbor list. TAOM's `expectedNeighborPairs * 2` check matches v1.3.15.

6. **`Patch37_EditorCacheRebuild` broad catch: CONFIRMED as a broad catch, not raised as a blocking finding.** `SubModule.cs:162-172` catches all exceptions around a single legacy patch category. Because only `Patch37_CacheBuildOverride` is in that category and the MCM path does not depend on it, swallowing the known runtime attach failure is a reasonable isolation choice. A stricter catch filter or full stack log would improve diagnostics, but I did not find evidence of an actually dangling Harmony hook.

7. **MCM lambda static-ness: DISPUTED.**
   - `IoC.Resolve` before `IoC.Configure()` would throw through the lambda and be surfaced by the catch at `TaomSettings.cs:452-461`.
   - MCMv5 does not persist `Action` buttons. Decompiled `BaseSettingsJsonConverter.WriteJson` and `ReadJson` both skip `SettingType.Button`, and `SettingsPropertyDefinition` maps `typeof(Action)` to `SettingType.Button`.
   - The 10-30 minute hint is conservative. The live 7-minute run does not prove slower CPUs/disks cannot approach 30 minutes, and the docs still describe 30 minutes as the target full-rebuild bound.

8. **Path A / Path B concurrent execution: DISPUTED for practical runtime, confirmed theoretical.** MCM is not available in editor mode, and singleplayer never calls the editor `NavigationCache<SettlementRecord>.GenerateCacheData()` target. There is no shared file lock between Path A and Path B, so a manual/reflection-triggered overlap would race on checkpoint/final files, but I would not treat that as a reachable user workflow unless MCM becomes editor-accessible.

9. **`CampaignSessionAdapter.GetSnapshot` swallows exceptions: DISPUTED.** `GetSnapshot()` is diagnostic-only and `RuntimeCacheRebuildService.Trigger()` already treats snapshot failure as non-fatal at `RuntimeCacheRebuildService.cs:57-64`. A partial snapshot is preferable to rejecting a rebuild because `CampaignTime.Now` or a settlement census is unavailable.

10. **Test subclass coverage gaps: CONFIRMED (P3-2).** The new tests cover trigger gating and path resolution, but the production `Task.Run -> RunBuild -> finally clear flag`, round-trip verification, and atomic write sequence are not directly exercised.

## Feature Scenarios

**S1. Double-click MCM button.** Correct. Both calls may run pre-flight logging before the lock, but only one can pass `Interlocked.CompareExchange`. The loser returns false and does not spawn a build.

**S2. Crash mid-Phase-2, resume next session.** Correct for the described live path. Checkpoint load deserializes Phase 1, Phase 2 runs fresh, then `WriteOutputAtomically` replaces the final file and refreshes `.prev`. Existing `.prev` is deleted at `RuntimeCacheRebuildService.cs:312-316` and replaced with the pre-rebuild final at `RuntimeCacheRebuildService.cs:317-318`. The remaining issue is if the crash occurs during the two rename steps themselves (P2-2).

**S3. Force-kill during serialization leaves `.tmp`.** Mostly correct. Stale temp detection logs at `RuntimeCacheRebuildService.cs:250-257`, and `WriteOutputAtomically` deletes stale temp before serializing at `RuntimeCacheRebuildService.cs:298-302`. If `File.Delete` fails on OneDrive/network/locked storage, the build fails before final replacement, which is safe but has no retry/backoff.

**S4. Non-English locale.** Runtime logic is locale-independent. Some log formatting uses current-culture numeric formats (`:N0`, `:F1`, `:E2`), so logs are not invariant-culture parse targets. I found no tests or production code parsing those logs, so this is acceptable.

**S5. Existing vanilla/older cache loaded at startup.** Correct and intended. `new SandBoxNavigationCache(MobileParty.NavigationType.Default)` constructs fresh dictionaries in the `NavigationCache<T>` base constructor, so the rebuild output is independent of the runtime cache instance already loaded by the game. The feature is a complete replacement generator.

## Config Cross-Reference

Actual config/class fields count is 19, not 17. The feature doc's table omits `enableDebugQualityCheck` and `enableUiOverlay`.

| JSON field | Production consumer status |
|---|---|
| `enabled` | Consumed by `RuntimeCacheRebuildService.RunBuild` and `Patch37_CacheBuildOverride`. |
| `forceVanilla` | Consumed by `RuntimeCacheRebuildService.RunBuild` and `Patch37_CacheBuildOverride`. |
| `parallelism` | Consumed by `CacheBuilderService`, both parallel builders, and `SmokeTestGate`. |
| `checkpointEvery` | Validated only; no production consumer. |
| `enablePathReuse` | No production consumer. |
| `enablePersistentPathCache` | No production consumer. |
| `enableIncremental` | Consumed by `CacheBuilderService`. |
| `incrementalMaxChanged` | Consumed by `SettlementDiffer` call in `CacheBuilderService`. |
| `incrementalSpatialRadius` | Validated only; no production consumer. |
| `enableDebugQualityCheck` | No production consumer and not documented in the feature doc's config table. |
| `enableUiOverlay` | No production consumer and not documented in the feature doc's config table. |
| `smokeTestPairs` | Consumed by `CacheBuilderService` logging and `SmokeTestGate`. |
| `smokeTestDistanceTolerance` | Consumed by `CacheBuilderService` logging and `SmokeTestGate`. |
| `phase1SkipReversePathfind` | No production consumer. |
| `validationReportRelativePath` | Consumed by `CacheBuilderService.WriteValidationReport`. |
| `enableCheckpoint` | Consumed by `CacheBuilderService`. |
| `checkpointRelativeDirectory` | Consumed by `CacheBuilderService.ResolveCheckpointDir`. |
| `settlementSnapshotRelativePath` | Consumed by `CacheBuilderService.ResolveSnapshotPath`. |
| `logVerbosity` | Validated/normalized only; no logger consumer. |

This is not a pivot regression: the MCM path uses the same provider and builder as the editor path. It is still user-facing config debt because the default JSON exposes knobs that do nothing.

## Findings Or Observations

P1: none

### P2 — Round-trip verification can fail or be blind while the user still gets a success popup

File: `Main/Features/EditorCacheRebuild/RuntimeCacheRebuildService.cs:158`

Evidence: `RunBuild` calls `VerifyOutputRoundTrip(...)` at lines 158-160, but the method returns `void`; both shortfall and exception branches only log at lines 369-379. `RunBuild` then logs `BUILD COMPLETE` and calls `NotifyOnMainThread(summary + " Load the next save to use it.")` at lines 170-173. In resume mode, `CacheBuilderService` leaves `phase1Result = default` when Phase 1 is skipped (`CacheBuilderService.cs:179-192`), so line 358 treats `expectedDistancePairs == 0` as automatically OK. Vanilla `Deserialize` is count-driven and ignores the stored CRCs; it detects normal EOF truncation, but not a structurally valid file that declares too few distance entries.

Impact: If the post-write check detects corruption/shortfall, the in-game message still says the rebuild completed and tells the user to load the next save. In resumed builds, the distance-count side of the check is disabled, so a structurally valid but logically incomplete file can pass with no warning. The `.prev` backup exists, but the user may not know to use it.

Fix: Make verification return a result or throw on failure. Gate the completion popup on `verification.Ok`; on failure, show a red failure message and keep the `.prev` restoration instruction user-visible. For resume mode, pass an expected distance count captured from `adapter.EnumerateExistingDistances().Count()` immediately before serialization, or include the Phase 1 distance count in checkpoint metadata.

### P2 — The final cache replacement is not crash-atomic across the two rename steps

File: `Main/Features/EditorCacheRebuild/RuntimeCacheRebuildService.cs:309`

Evidence: `WriteOutputAtomically` writes `<final>.tmp`, deletes any existing `.prev`, moves `final -> .prev`, then moves `.tmp -> final` at lines 309-322. Each `File.Move` is atomic, but the sequence is not atomic as a transaction. A process kill after line 318 and before line 322 leaves no live `settlements_distance_cache_Default.bin`; only `.prev` and `.tmp` remain. The diagnostic at lines 235-248 warns on a future rebuild, but the next game startup can happen before the user clicks rebuild again.

Impact: A crash during the narrow promotion window can leave the cache file missing. That is better than a corrupt live file, but it still violates the "atomic write" promise and can make the next session load without the expected cache unless the user manually restores `.prev`.

Fix: On Windows/.NET Framework, use `File.Replace(tempPath, finalPath, backupPath, ignoreMetadataErrors: true)` when `finalPath` exists. It atomically replaces final and writes the backup in one filesystem operation. Keep the `File.Move(tempPath, finalPath)` path only for the first build where no final exists.

P3:

### P3 — `cache_rebuild_config.json` exposes reserved/dead knobs as if they are active

File: `Main/Features/EditorCacheRebuild/CacheRebuildConfig.cs:9`

Evidence: `checkpointEvery`, `enablePathReuse`, `enablePersistentPathCache`, `incrementalSpatialRadius`, `enableDebugQualityCheck`, `enableUiOverlay`, `phase1SkipReversePathfind`, and `logVerbosity` have no production consumer beyond declaration and, for some fields, validation. The repo search shows no `Main/Features/EditorCacheRebuild` consumer for `EnablePathReuse`, `EnablePersistentPathCache`, `EnableDebugQualityCheck`, `EnableUiOverlay`, or `Phase1SkipReversePathfind`. The feature doc says some are reserved, but the shipped JSON has no comments and includes two undocumented fields.

Impact: Users can edit config fields that silently do nothing. This is especially misleading for `logVerbosity`, because it validates successfully but does not change logger output, and for `phase1SkipReversePathfind`, because it sounds like a major performance/correctness switch.

Fix: Remove inactive fields from the shipped JSON until they are wired, or move them into a clearly named reserved/internal section that the provider ignores with an explicit warning. If they remain public, every field should have at least one production consumer or a runtime log saying it is reserved.

### P3 — New tests stop at `SpawnBuild` and miss the production background path

File: `TAOM.Tests/Features/EditorCacheRebuild/RuntimeCacheRebuildServiceTests.cs:195`

Evidence: `TestableRuntimeCacheRebuildService.SpawnBuild` intentionally does not call base at lines 195-199. That makes trigger tests deterministic, but it means the 11 new tests do not cover `Task.Run`, `RunBuild`, the `finally` flag clear at `RuntimeCacheRebuildService.cs:185-188`, `VerifyOutputRoundTrip`, or the `WriteOutputAtomically` rename sequence.

Impact: The exact edge cases most likely to regress in this pivot - background failure cleanup, verification failure handling, and interrupted-write behavior - are covered only by live testing and manual reasoning. The live test exercised the successful path, not the failure paths.

Fix: Extract small collaborators for output writing and verification, or make narrow internal methods testable under `InternalsVisibleTo`. Add tests for: builder throws and `_runningFlag` clears; verification shortfall changes the user-visible result; stale `.tmp` delete failure aborts before final mutation; final replacement preserves/refreshes `.prev`.

## Summary

P1: none
P2: 2
P3: 2
VERDICT: ISSUES FOUND
