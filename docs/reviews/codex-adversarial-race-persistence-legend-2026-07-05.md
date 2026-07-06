# Codex Adversarial Review — race-persistence-legend (#330)

- **Date:** 2026-07-05
- **Model:** gpt-5.5, `model_reasoning_effort = "xhigh"` (via `codex exec`)
- **Prompt:** `codex-adversarial-race-persistence-legend-2026-07-05.prompt.md`
- **Verdict:** **CLEAN — P1: 0 | P2: 0 | P3: 0.** All 6 seeded Known Suspects DISPUTED with decompiled evidence; all 8 lifecycle scenarios PASS.
- **Note:** Codex's sandbox could not run `dotnet test` (MSBuild SDK probe access denied — recurring sandbox limitation); the local suite is green (4,120 passed). The raw 1.9MB session log was discarded per house convention; the final review message is preserved verbatim below.

---

**VANILLA CODE**

Decompiled from `E:\Decompiled_Bannerlord\...` current tree.

```csharp
// CampaignBehaviorManager.cs
private void OnBeforeSave()
{
    _campaignBehaviorDataStore.ClearBehaviorData();
    foreach (CampaignBehaviorBase campaignBehavior in _campaignBehaviors)
    {
        _campaignBehaviorDataStore.SaveBehaviorData(campaignBehavior);
    }
}

public void LoadBehaviorData()
{
    foreach (CampaignBehaviorBase campaignBehavior in _campaignBehaviors)
    {
        _campaignBehaviorDataStore.LoadBehaviorData(campaignBehavior);
    }
    _campaignBehaviorDataStore.ClearBehaviorData();
}
```

```csharp
// CampaignBehaviorDataStore.cs
internal class BehaviorSaveData : IDataStore
{
    [SaveableField(0)]
    private Dictionary<string, object> _records = new Dictionary<string, object>();

    private readonly bool _isSaving;

    public bool IsSaving => _isSaving;
    public bool IsLoading => !_isSaving;

    public BehaviorSaveData(bool isSaving)
    {
        _isSaving = isSaving;
    }

    public bool SyncData<T>(string key, ref T data)
    {
        if (IsSaving)
        {
            _records.Add(key, data);
            return true;
        }
        if (_records.TryGetValue(key, out var value))
        {
            data = (T)value;
            return true;
        }
        return false;
    }
}

internal void SaveBehaviorData(CampaignBehaviorBase campaignBehavior)
{
    string stringId = campaignBehavior.StringId;
    BehaviorSaveData behaviorSaveData = new BehaviorSaveData(isSaving: true);
    campaignBehavior.SyncData(behaviorSaveData);
    ...
}

internal void LoadBehaviorData(CampaignBehaviorBase campaignBehavior)
{
    string stringId = campaignBehavior.StringId;
    if (_behaviorDict.TryGetValue(stringId, out var value))
    {
        campaignBehavior.SyncData(value);
        return;
    }
    ...
}
```

```csharp
// SaveHandler.cs
internal void SaveTick()
{
    ...
    case SaveSteps.Saving:
    {
        _saveStep++;
        CampaignEventDispatcher.Instance.OnBeforeSave();
        if (CampaignOptions.IsIronmanMode)
        {
            MBSaveLoad.SaveAsCurrentGame(GetSaveMetaData(), IronmanModSaveName, OnSaveCompleted);
            break;
        }
        SaveArgs saveArgs = SaveArgsQueue.Peek();
        switch (saveArgs.Mode)
        {
        case SaveArgs.SaveMode.SaveAs:
            MBSaveLoad.SaveAsCurrentGame(GetSaveMetaData(), saveArgs.Name, OnSaveCompleted);
            break;
        case SaveArgs.SaveMode.QuickSave:
            MBSaveLoad.QuickSaveCurrentGame(GetSaveMetaData(), OnSaveCompleted);
            break;
        case SaveArgs.SaveMode.AutoSave:
            MBSaveLoad.AutoSaveCurrentGame(GetSaveMetaData(), OnSaveCompleted);
            break;
        }
        break;
    }
    ...
}
```

```csharp
// TaleWorlds.Core.FaceGen.cs
public static string[] GetRaceNames()
{
    return _instance?.GetRaceNames() ?? null;
}
```

```csharp
// TaleWorlds.MountAndBlade.FaceGen.cs
private FaceGen()
{
    _raceNamesDictionary = new Dictionary<string, int>();
    _raceNamesArray = MBAPI.IMBFaceGen.GetRaceIds().Split(new char[1] { ';' });
    for (int i = 0; i < _raceNamesArray.Length; i++)
    {
        _raceNamesDictionary[_raceNamesArray[i]] = i;
    }
    _monstersDictionary = new Dictionary<string, Monster>();
    _monstersArray = new Monster[_raceNamesArray.Length];
}

public static void CreateInstance()
{
    TaleWorlds.Core.FaceGen.SetInstance(new FaceGen());
}

public string[] GetRaceNames()
{
    return (string[])_raceNamesArray.Clone();
}
```

```csharp
// CoreManaged.cs
[MBCallback(null, false)]
internal static void OnLoadCommonFinished()
{
    FaceGen.CreateInstance();
}
```

```csharp
// SaveableBasicTypeDefiner.cs
protected internal override void DefineBasicTypes()
{
    ...
    AddBasicTypeDefinition(typeof(string), 21, new StringSerializer());
}

protected internal override void DefineGenericStructDefinitions()
{
    ConstructGenericStructDefinition(typeof(KeyValuePair<string, int>));
    ...
}

protected internal override void DefineContainerDefinitions()
{
    ...
    ConstructContainerDefinition(typeof(Dictionary<string, int>));
    ConstructContainerDefinition(typeof(Dictionary<string, string>));
    ConstructContainerDefinition(typeof(Dictionary<string, object>));
    ...
}
```

**KNOWN SUSPECTS**

S1. DISPUTED. `BehaviorSaveData.SyncData` would throw on duplicate save keys because it uses `_records.Add`, but TAOM syncs `_taom_heroRaceMap` and `_taom_raceNameLegend` once each at [RacePersistenceService.cs:152](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceService.cs:152)-[153](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceService.cs:153). The only production caller is [RacePersistenceBehavior.cs:23](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceBehavior.cs:23)-[25](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceBehavior.cs:25), registered once at [SubModule.cs:421](C:/Users/mikew/source/repos/TAOM/Main/SubModule.cs:421)-[422](C:/Users/mikew/source/repos/TAOM/Main/SubModule.cs:422). Vanilla creates one fresh `BehaviorSaveData(true)` per behavior save.

S2. DISPUTED. Save-side `SyncData` uses `BehaviorSaveData(true)`; load-side `SyncData` is only through `LoadBehaviorData` before behavior events are registered on saved campaigns. `SaveHandler.SaveTick` calls `OnBeforeSave` immediately before writing, not with `IsLoading`. Clear-on-load at [RacePersistenceService.cs:147](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceService.cs:147)-[150](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceService.cs:150) is not reached during save-as-then-continue.

S3. DISPUTED. In TAOM, legend[0] is `human`, and `RaceManager` maps current FaceGen names from the engine array at [RaceManager.cs:50](C:/Users/mikew/source/repos/TAOM/Main/Core/Domain/RaceManager.cs:50)-[59](C:/Users/mikew/source/repos/TAOM/Main/Core/Domain/RaceManager.cs:59). If a future module set renames/removes race 0, the saved name no longer exists, so [RacePersistenceService.cs:104](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceService.cs:104)-[108](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceService.cs:108) skips and keeps XML race. That differs from legacy raw `0`, but it is correct name-based behavior for a removed race, not an in-range remap.

S4. DISPUTED for real game. The fallback paths exist at [RaceManager.cs:63](C:/Users/mikew/source/repos/TAOM/Main/Core/Domain/RaceManager.cs:63)-[77](C:/Users/mikew/source/repos/TAOM/Main/Core/Domain/RaceManager.cs:77), but production first capture is `OnBeforeSave`, long after `CoreManaged.OnLoadCommonFinished -> FaceGen.CreateInstance`. TAOM constructors resolve `IRaceManager` but do not initialize it; capture initializes lazily via [RacePersistenceService.cs:41](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceService.cs:41). If a harness/editor somehow wrote a one-entry degraded legend, non-human saved ints would skip+warn as out-of-range; I did not find a real game path that can ship that save.

S5. DISPUTED. Old builds querying only `_taom_heroRaceMap` tolerate the extra `_taom_raceNameLegend` record because vanilla `SyncData` only reads requested keys and has no strict-consumption check. Old behavior remains raw-int restore.

S6. DISPUTED. Tuples checked:
- `(saved=1, hero=1, legend human;dwarf, current dwarf=5)` restores to 5 via [RacePersistenceService.cs:100](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceService.cs:100)-[114](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceService.cs:114).
- `(saved=1, hero=1, legend human;dwarf, current dwarf=1)` no set, no warning.
- `(saved=7, hero=0, legend human;dwarf)` skip+warn as out-of-range.
- `(saved=2, hero=0, legend human;dwarf;elf, elf removed)` skip+warn and does not call fallback `GetRaceIdFromName`.
- legacy `(saved=0, hero=2, no legend)` restores raw 0; legacy `(saved=99, hero=0, invalid)` skips via [RacePersistenceService.cs:127](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceService.cs:127)-[131](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceService.cs:131).

**DEEP ANALYSIS**

A. PASS. New race inserted before dwarf: map has saved `1`, legend[1] is `dwarf`, current `GetRaceIdFromName("dwarf")` supplies the shifted id, then `SetHeroRace`.

B. PASS. Disabled third-party race mod: surviving names translate; vanished names fail `IsValidRaceName` and skip+warn, leaving current XML race.

C. PASS. Pre-#330 save has no legend; [RacePersistenceService.cs:85](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceService.cs:85) sets `legend = null`, then raw legacy path runs, including race-0 bypass and invalid-id guard.

D. PASS. Pre-TAOM save has neither key; clear-on-load leaves empty map, then [RacePersistenceService.cs:72](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceService.cs:72)-[77](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceService.cs:77) logs no saved data and restores nothing.

E. PASS. Same-process new-format campaign -> pre-#330 load: [RacePersistenceService.cs:147](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceService.cs:147)-[150](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceService.cs:150) clears old map+legend before absent legend leaves `""`; loaded map uses legacy only.

F. PASS. New campaign: [RacePersistenceBehavior.cs:18](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceBehavior.cs:18) resets on `OnNewGameCreatedEvent`; vanilla calls new-game-created before session start, so restore sees empty map.

G. PASS. Dead hero entries are ignored because [HeroRosterAdapter.cs:14](C:/Users/mikew/source/repos/TAOM/Main/Adapters/HeroRosterAdapter.cs:14)-[17](C:/Users/mikew/source/repos/TAOM/Main/Adapters/HeroRosterAdapter.cs:17) uses `Hero.AllAliveHeroes`; next capture rebuilds `_heroRaceMap` from scratch at [RacePersistenceService.cs:40](C:/Users/mikew/source/repos/TAOM/Main/Features/HeroRace/RacePersistenceService.cs:40).

H. PASS. Character creation writes the selected player race at [CharacterCreationContentService.cs:311](C:/Users/mikew/source/repos/TAOM/Main/Features/CharacterCreation/CharacterCreationContentService.cs:311)-[333](C:/Users/mikew/source/repos/TAOM/Main/Features/CharacterCreation/CharacterCreationContentService.cs:333); save capture includes all alive heroes and the legend; shifted load restores by `StringId` through the name path.

**CONFIG CROSS-REFERENCE**

`git diff --name-only` shows `CHANGELOG.md`, the three production/interface files, two test files, and `docs/features/hero-race.md`; no JSON/XML/XSLT config files changed. Grep for race-shaped `SyncData` found only `_taom_heroRaceMap` and `_taom_raceNameLegend` in `RacePersistenceService`; no second TAOM feature persists race ints.

**FINDINGS OR OBSERVATIONS**

No findings.

Focused tests could not run in this sandbox: `dotnet test ...` reached MSBuild but failed probing `C:\Users\mikew\AppData\Local\Microsoft SDKs` with access denied.

P1: 0 | P2: 0 | P3: 0
VERDICT: CLEAN
