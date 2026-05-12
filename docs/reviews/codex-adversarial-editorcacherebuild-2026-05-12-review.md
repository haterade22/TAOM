# Codex Adversarial Review - EditorCacheRebuild

Date: 2026-05-12
Reviewer: Codex, independent pass
Verdict: ISSUES FOUND

## Quality Gates

- Decompiled v1.3.15 `NavigationCache<T>` via `ilspycmd`: DONE
- Verified `_settlementToSettlementDistanceWithLandRatio`: private field
- Verified `_fortificationNeighbors`: private field
- Verified `_navigationType`: protected property with private setter
- Verified vanilla `GenerateNeighborSettlementsCache()` starts with `_fortificationNeighbors.Clear()`: DONE
- Verified vanilla `SaveSettlementDistanceCacheEditor()` calls `Serialize(filePath)` after `GenerateCacheData()`: DONE
- Verified all known suspects: DONE
- Independent `dotnet test TAOM.Tests`: BLOCKED by sandbox. First run failed writing `C:\Users\CodexSandboxOffline\.dotnet`; retry with `DOTNET_CLI_HOME` inside the workspace then failed on `C:\Users\mikew\AppData\Local\Microsoft SDKs` access.

## Vanilla Contract Snippets

`NavigationCache<T>` v1.3.15 fields/properties:

```csharp
private Dictionary<NavigationCacheElement<T>, Dictionary<NavigationCacheElement<T>, (float, float)>> _settlementToSettlementDistanceWithLandRatio;
private Dictionary<T, MBReadOnlyList<T>> _fortificationNeighbors;
private Dictionary<int, NavigationCacheElement<T>> _closestSettlementsToFaceIndices;
public float MaximumDistanceBetweenTwoConnectedSettlements { get; protected set; }
protected MobileParty.NavigationType _navigationType { get; private set; }
```

`GenerateNeighborSettlementsCache()` starts with a clear:

```csharp
protected void GenerateNeighborSettlementsCache()
{
    _fortificationNeighbors.Clear();
    List<T> updatedSettlementsForNeighborDetection =
        GetUpdatedSettlementsForNeighborDetection(GetAllRegisteredSettlements());
    ...
    if (settlement2.IsFortification && CheckBeingNeighbor(updatedSettlementsForNeighborDetection, settlement, settlement2))
    {
        AddNeighbor(settlement, settlement2);
    }
}
```

`AddNeighbor(T,T)` dedupes existing neighbor pairs before appending:

```csharp
protected void AddNeighbor(T settlement1, T settlement2)
{
    bool flag = false;
    foreach (KeyValuePair<T, MBReadOnlyList<T>> fortificationNeighbor in _fortificationNeighbors)
    {
        if ((fortificationNeighbor.Key.StringId.Equals(settlement1.StringId) && fortificationNeighbor.Value.Contains(settlement2)) ||
            (fortificationNeighbor.Key.StringId.Equals(settlement2.StringId) && fortificationNeighbor.Value.Contains(settlement1)))
        {
            flag = true;
            break;
        }
    }
    if (!flag)
    {
        ...
        _fortificationNeighbors[settlement1] = mBList;
        ...
        _fortificationNeighbors[settlement2] = mBList;
    }
}
```

`NavigationCacheElement<T>.Sort(...)`:

```csharp
public static void Sort(ref NavigationCacheElement<T> settlement1, ref NavigationCacheElement<T> settlement2, out bool isPairChanged)
{
    isPairChanged = false;
    int num = string.Compare(settlement1.StringId, settlement2.StringId, StringComparison.Ordinal);
    if (num >= 0 && (num != 0 || !settlement1.IsPortUsed))
    {
        NavigationCacheElement<T> navigationCacheElement = settlement2;
        NavigationCacheElement<T> navigationCacheElement2 = settlement1;
        settlement1 = navigationCacheElement;
        settlement2 = navigationCacheElement2;
        isPairChanged = true;
    }
}
```

`SaveSettlementDistanceCacheEditor()` keeps `Serialize` reachable after the patched call:

```csharp
private void SaveSettlementDistanceCacheEditor()
{
    bool[] regionMapping = SandBoxHelpers.MapSceneHelper.GetRegionMapping(_partyNavigationModel);
    ((ScriptComponentBehavior)this).Scene.SetNavMeshRegionMap(regionMapping);
    List<NavigationType> list = new List<NavigationType> { (NavigationType)1 };
    ...
    foreach (NavigationType item in list)
    {
        int[] invalidTerrainTypesForNavigationType = _partyNavigationModel.GetInvalidTerrainTypesForNavigationType(item);
        try
        {
            XmlDocument settlementDocument = LoadXmlFile(SettlementsXmlPath);
            List<SettlementRecord> settlementRecords = LoadSettlementData(settlementDocument);
            ...
            SettlementPositionScriptNavigationCache settlementPositionScriptNavigationCache =
                new SettlementPositionScriptNavigationCache(settlementRecords, ((ScriptComponentBehavior)this).Scene,
                    _mapDistanceModel, _partyNavigationModel, item);
            ((NavigationCache<SettlementRecord>)settlementPositionScriptNavigationCache).GenerateCacheData();
            GetSettlementsDistanceCacheFileForCapability(GetMapModuleId(), item, out var filePath);
            ((NavigationCache<SettlementRecord>)settlementPositionScriptNavigationCache).Serialize(filePath);
        }
        catch
        {
        }
        finally
        {
            ...
        }
    }
}
```

## Findings

### Finding 1: Incremental moved-settlement rebuild writes duplicate distance keys

**Severity:** P1
**File(s):** `Main/Features/EditorCacheRebuild/CacheBuilderService.cs:93`, `Main/Features/EditorCacheRebuild/Phase1/SerialPhase1Builder.cs:39`, `Main/Features/EditorCacheRebuild/Phase1/ParallelPhase1Builder.cs:56`, `Main/Adapters/NavigationCacheAdapter.cs:249`

**What:** Incremental mode deserializes the final old cache, then recomputes pairs touching moved settlements into the same add-only vanilla distance dictionary. Any moved settlement already has distance entries in the deserialized cache, so the first recomputed existing pair hits `Dictionary.Add` on an existing key.

**Why:** TAOM sets `phase1Filter` after `adapter.DeserializeCache(...)`, then Phase 1 writes recomputed pairs using either `AddClosestEntrancePairBase` or `SetSettlementToSettlementDistanceWithLandRatio`. Vanilla's setter does not replace existing entries; it logs/asserts and still calls `value.Add(...)`.

**Evidence:**

TAOM source:

```csharp
adapter.DeserializeCache(GetFinalCachePath(checkpointDir, navTypeName));
phase1Filter = new ChangedSettlementsFilter(diff.AllChangedIds());
mode = "incremental";
...
phase1Result = phase1Filter != null
    ? phase1.RunFiltered(adapter, phase1Filter, ct)
    : phase1.Run(adapter, ct);
```

```csharp
adapter.WriteComputedPair(in result);
...
_setSettlementDistance.Invoke(_cacheInstance, args);
```

Vanilla:

```csharp
protected void SetSettlementToSettlementDistanceWithLandRatio(...)
{
    NavigationCacheElement<T>.Sort(ref settlement1, ref settlement2, out var _);
    ...
    if (value.TryGetValue(settlement2, out var _))
    {
        Debug.FailedAssert("Element already exists", ...);
    }
    value.Add(settlement2, (distance, landRatio));
}
```

**Fix:** Incremental cannot use vanilla add-only writes against a full old distance dictionary. Add adapter support to remove/replace all distance entries touching changed settlement IDs before Phase 1, or write through a replacement path that updates existing entries. Also add a unit test using a fake adapter that throws on duplicate writes for moved-settlement incremental mode.

### Finding 2: Incremental deserialize clobbers fresh Phase 0 and keeps stale Phase 2 neighbors

**Severity:** P1
**File(s):** `Main/Features/EditorCacheRebuild/CacheBuilderService.cs:75`, `Main/Features/EditorCacheRebuild/CacheBuilderService.cs:101`, `Main/Features/EditorCacheRebuild/Phase2/SerialPhase2Builder.cs:20`, `Main/Features/EditorCacheRebuild/Phase2/ParallelPhase2Builder.cs:28`, `Main/Adapters/INavigationCacheAdapter.cs:39`

**What:** The service runs Phase 0, then incremental mode deserializes the old final `.bin`, which overwrites the freshly computed closest-face cache and loads the old neighbor cache. Phase 2 then appends/dedupes new neighbors without clearing old ones.

**Why:** Vanilla `Deserialize` restores all three subcaches: distances, fortification neighbors, and closest face mappings. Vanilla `GenerateNeighborSettlementsCache()` clears neighbors before rebuilding them. TAOM Phase 2 exposes only `AddNeighbor`; there is no adapter method to clear `_fortificationNeighbors`, and no method to clear/rebuild `_closestSettlementsToFaceIndices` after `Deserialize`.

**Evidence:**

TAOM source:

```csharp
_logger.LogInfo("[CacheRebuild] Phase 0: GenerateClosestSettlementToFaceCache (vanilla helper)");
adapter.RunClosestSettlementCache();
...
adapter.DeserializeCache(GetFinalCachePath(checkpointDir, navTypeName));
```

```csharp
var fortifications = adapter.GetFortificationsForNeighborDetection();
...
if (adapter.CheckBeingNeighbor(fortifications, s1, s2))
{
    adapter.AddNeighbor(s1, s2);
}
```

Vanilla:

```csharp
public void Deserialize(string path)
{
    ...
    _fortificationNeighbors = new Dictionary<T, MBReadOnlyList<T>>(num3);
    ...
    AddNeighbor(cacheElement3, cacheElement4);
    ...
    _closestSettlementsToFaceIndices = new Dictionary<int, NavigationCacheElement<T>>(num4);
    ...
    SetClosestSettlementToFaceIndex(faceId, cacheElement6);
}
```

```csharp
protected void GenerateNeighborSettlementsCache()
{
    _fortificationNeighbors.Clear();
    ...
}
```

**Fix:** Do not deserialize the whole prior cache after Phase 0. Either deserialize only the distance dictionary, or add adapter operations to clear/rebuild `_closestSettlementsToFaceIndices` and clear `_fortificationNeighbors` before Phase 2. A correct incremental path should preserve unchanged distances, remove/replace changed distances, rebuild closest-face data for the current scene, and rebuild the complete neighbor cache from an empty dictionary.

### Finding 3: Patch37 fallback runs vanilla on a mutated cache instance

**Severity:** P2
**File(s):** `Main/Features/EditorCacheRebuild/Hooks/Patch37_CacheBuildOverride.cs:55`, `Main/Features/EditorCacheRebuild/Hooks/Patch37_CacheBuildOverride.cs:65`, `Main/Features/EditorCacheRebuild/CacheBuilderService.cs:75`

**What:** The catch block says it falls back to vanilla by returning `true`, but by that point `CacheBuilderService.Build()` may already have run Phase 0, deserialized an old cache, or partially written distances/neighbors. Vanilla `GenerateCacheData()` is not safe to rerun on a partially populated cache object because its face and distance setters use `Dictionary.Add`.

**Why:** The prefix mutates `__instance` before most possible service failures. If an exception escapes after Phase 0, returning `true` makes the original `GenerateCacheData()` execute on the same instance. Vanilla `GenerateClosestSettlementToFaceCache()` calls `SetClosestSettlementToFaceIndex`, which also add-only writes by face id.

**Evidence:**

TAOM source:

```csharp
var result = service.Build(adapter, CancellationToken.None);
...
catch (Exception ex)
{
    logger?.LogError($"[Patch37] EXCEPTION - falling back to vanilla cache build: ...");
    return true;
}
```

```csharp
adapter.RunClosestSettlementCache();
```

Vanilla:

```csharp
protected void SetClosestSettlementToFaceIndex(int faceId, NavigationCacheElement<T> settlement)
{
    _closestSettlementsToFaceIndices.Add(faceId, settlement);
}

protected void GenerateClosestSettlementToFaceCache()
{
    ...
    SetClosestSettlementToFaceIndex(i, new NavigationCacheElement<T>(closestSettlementToPosition, isPort));
}
```

**Fix:** Once the prefix mutates the cache, do not return `true` on failure. Either move all mutation after preflight checks, add adapter `ResetAllCaches()` and call it before returning `true`, or return `false` after logging a hard failure so vanilla does not rerun against corrupted state. The best fix is to instantiate a new vanilla cache for fallback, but the current prefix does not own the caller's local variable.

### Finding 4: Snapshot save uses `CampaignVec2.Face` in an editor path where `Campaign.Current` may be null

**Severity:** P2
**File(s):** `Main/Features/EditorCacheRebuild/Diff/SettlementSnapshotStore.cs:43`

**What:** `SettlementSnapshotStore.Save()` reads `s.GatePosition.Face.FaceIndex` and `s.PortPosition.Face.FaceIndex`. In editor mode, vanilla explicitly supports `Campaign.Current == null`; `CampaignVec2.Face` unconditionally dereferences `Campaign.Current.MapSceneWrapper`.

**Why:** The vanilla editor cache builder does not prove `Face` is safe. `SettlementPositionScriptNavigationCache.GetRealDistanceAndLandRatioBetweenSettlements()` converts `CampaignVec2` to raw `Vec2` and asks `Scene` for navmesh face indices directly. `CampaignVec2.ToVec2()` returns `_position` and does not touch `Face`.

**Evidence:**

TAOM source:

```csharp
GateFace = s.GatePosition.Face.FaceIndex,
GateX = s.GatePosition.ToVec2().x,
GateY = s.GatePosition.ToVec2().y,
PortFace = s.PortPosition.Face.FaceIndex,
```

Vanilla:

```csharp
public PathFaceRecord Face
{
    get
    {
        if (!_isPositionCacheValid)
        {
            _faceCache = Campaign.Current.MapSceneWrapper.GetFaceIndex(this);
            _isPositionCacheValid = true;
        }
        return _faceCache;
    }
}

public Vec2 ToVec2()
{
    return _position;
}
```

```csharp
private PartyNavigationModel GetPartyNavigationModel()
{
    if (Campaign.Current != null)
    {
        return Campaign.Current.Models.PartyNavigationModel;
    }
    ...
    return CreateBaseNavigationModel(naval: false);
}
```

**Fix:** Do not use `CampaignVec2.Face` in the snapshot store. Either snapshot only coordinates and flags, or add adapter support to resolve face indices through the editor `Scene` using the same `Scene.GetNavMeshFaceIndex(...)` pattern as vanilla.

### Finding 5: Float config validation accepts `NaN`/`Infinity`, which can disable the smoke-test gate

**Severity:** P3
**File(s):** `Main/Features/EditorCacheRebuild/CacheRebuildConfigProvider.cs:84`, `Main/Features/EditorCacheRebuild/CacheRebuildConfigProvider.cs:98`, `Main/Features/EditorCacheRebuild/Validation/SmokeTestGate.cs:91`

**What:** Range checks for float config values do not reject non-finite values. `NaN` passes both `< min` and `> max` checks. A `NaN` `smokeTestDistanceTolerance` makes `maxDelta > tolerance` false, so the parallel path can pass the smoke gate regardless of delta.

**Evidence:**

TAOM source:

```csharp
if (parsed.SmokeTestDistanceTolerance < 1e-8f || parsed.SmokeTestDistanceTolerance > 1e-2f)
{
    parsed.SmokeTestDistanceTolerance = defaults.SmokeTestDistanceTolerance;
}
```

```csharp
if (maxDelta > config.SmokeTestDistanceTolerance)
{
    return new SmokeTestResult(SmokeTestOutcome.Failed, ...);
}
```

Vanilla: N/A; this is TAOM config-validation logic.

**Fix:** Reject `float.IsNaN(...)` and `float.IsInfinity(...)` before range checks for every float config field.

### Finding 6: `SortedPathKey` matches vanilla sort, but degenerate self-pairs are untested and should be rejected

**Severity:** P3
**File(s):** `Main/Features/EditorCacheRebuild/Caching/SortedPathKey.cs:16`, `TAOM.Tests/Features/EditorCacheRebuild/Caching/SortedPathKeyTests.cs:25`

**What:** The fixed `SortedPathKey` condition is equivalent to vanilla for normal inputs, including same-id gate/port ordering. Tests do not cover `id1 == id2 && isPort1 == isPort2`. Vanilla would swap and set `isPairChanged=true`, but the pair remains semantically a self-pair. For a path-cache key, that should probably be rejected rather than canonicalized.

**Evidence:**

TAOM source:

```csharp
int cmp = string.Compare(id1, id2, StringComparison.Ordinal);
bool swap = cmp >= 0 && (cmp != 0 || !isPort1);
```

Existing tests cover same id with different port flags only:

```csharp
var key = new SortedPathKey("settlement_x", true, "settlement_x", false);
...
var key = new SortedPathKey("settlement_x", false, "settlement_x", true);
```

Vanilla:

```csharp
int num = string.Compare(settlement1.StringId, settlement2.StringId, StringComparison.Ordinal);
if (num >= 0 && (num != 0 || !settlement1.IsPortUsed))
{
    ...
    isPairChanged = true;
}
```

**Fix:** Add tests for `(same id, gate, gate)` and `(same id, port, port)`. Prefer throwing `ArgumentException` for identical endpoint keys unless the future path-cache consumer has a concrete self-path use case.

## Known Suspects

1. **Stale Phase 2 dict on resume / incremental:** PARTIALLY CONFIRMED. Vanilla clears `_fortificationNeighbors`. TAOM does not. Duplicate-output hypothesis is DISPUTED because `AddNeighbor` dedupes. Resume-stale hypothesis is DISPUTED because checkpoint is saved immediately after Phase 1, before Phase 2 neighbors are written. Incremental stale-neighbor correctness bug is CONFIRMED because final `.bin` deserialization loads old neighbors and TAOM never clears them before rebuilding.

2. **Other reflection field/property mismatches:** DISPUTED. v1.3.15 has `_settlementToSettlementDistanceWithLandRatio` and `_fortificationNeighbors` as private fields. `_navigationType` is a property, and TAOM correctly uses `GetProperty`.

3. **ThreadStatic argument pool reentrancy:** DISPUTED. Traced reflected targets do not call `GenerateCacheData`, fire Harmony callbacks, or re-enter `NavigationCacheAdapter`. They call vanilla/concrete methods directly (`GetCacheElement`, `GetRealDistance...`, `Set...`, `CheckBeingNeighbor`, `AddNeighbor`, `Sort`, `GenerateClosestSettlementToFaceCache`). The ThreadStatic argument pool is safe under this call graph.

4. **Patch37 `Prepare()` ordering with module load:** DISPUTED. `Module.LoadSubModules` loads assemblies for all active submodules before `InitializeSubModuleBases()` calls `OnSubModuleLoad()`. SandBox's `SubModule.xml` includes `SandBox.View.dll` in the `Sandbox` module, and TAOM depends on `Sandbox` with `LoadBeforeThis`. Therefore `Type.GetType("..., SandBox.View")` should succeed by TAOM `OnSubModuleLoad` in normal editor/singleplayer startup.

5. **Editor `CampaignVec2.Face` NRE risk:** CONFIRMED. See Finding 4.

6. **Patch37 + vanilla Serialize chain:** CONFIRMED. The Prefix only skips `GenerateCacheData`; vanilla `SaveSettlementDistanceCacheEditor()` continues to `Serialize(filePath)` after the patched call returns false.

7. **`SortedPathKey` sort order:** CONFIRMED equivalent for normal inputs. Degenerate identical endpoint inputs are not tested and should be rejected or explicitly documented; see Finding 6.

## Config Cross-Reference

Confirmed consumers:

- `enabled`, `forceVanilla`: `Patch37_CacheBuildOverride`
- `parallelism`: `CacheBuilderService`, `ParallelPhase1Builder`, `ParallelPhase2Builder`, `SmokeTestGate`
- `enableIncremental`, `incrementalMaxChanged`: `CacheBuilderService`
- `smokeTestPairs`, `smokeTestDistanceTolerance`: `CacheBuilderService`, `SmokeTestGate`
- `validationReportRelativePath`: `CacheBuilderService`
- `enableCheckpoint`, `checkpointRelativeDirectory`: `CacheBuilderService`
- `settlementSnapshotRelativePath`: `CacheBuilderService`

Reserved or orphan fields, consistent with the feature doc unless v1 intends them to work now:

- `checkpointEvery`
- `enablePathReuse`
- `enablePersistentPathCache`
- `incrementalSpatialRadius`
- `enableDebugQualityCheck`
- `enableUiOverlay`
- `phase1SkipReversePathfind`
- `logVerbosity` (validated/normalized, but not applied to logger filtering)

## Summary

P1: 2
P2: 2
P3: 2

VERDICT: ISSUES FOUND
