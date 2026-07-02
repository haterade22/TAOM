# Codex Adversarial Review - Patch55 BasicTableauRaceGuard Race Allow-List

Date: 2026-07-02
Scope: Patch55 BasicTableauRaceGuard refactor, name-based per-race allow-list for the Save/Load hero preview.

Full installed game methods were inspected locally from the requested installed DLL paths. I quote only the load-bearing decompiled lines here; the inspected bodies confirm the summarized flow.

## Verdict

P1: 0 | P2: 0 | P3: 0
VERDICT: CLEAN

The refactor is architecturally sound for the stated empirical allow-list model. The guard validates the current-session race id before name lookup, only passes through the exact current-session race name `uruk`, and fails closed to human on invalid ids or resolver exceptions. I found residual risk in the empirical asset-safety claim, but it is already the documented nature of this feature rather than a new defect introduced by this diff.

## Decompiled Vanilla Evidence

### BasicCharacterTableau

Source inspected:
`E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.View.dll`

Relevant decompiled lines from `TaleWorlds.MountAndBlade.View.Tableaus.BasicCharacterTableau`:

```csharp
private const int _expectedCharacterCodeVersion = 4;
private int _race;
```

`DeserializeCharacterCode` parses the visual code as pipe-delimited format version 4 and reads race as the fifth payload field:

```csharp
text.Split(new char[1] { '|' });
if (num == 4)
{
    _skeletonName = array[num++];
    _skinMeshesMask = int.Parse(array[num++]);
    _isFemale = bool.Parse(array[num++]);
    _race = int.Parse(array[num]);
}
```

There is no race-name, module-set, or race-table version field in the tableau visual code beyond the leading format version `4`.

`RefreshCharacterTableau` uses hardcoded human animation system data, then passes `_race` into `SkinGenerationParams` in the race slot:

```csharp
AnimationSystemData.GetHardcodedAnimationSystemDataForHumanSkeleton();
bool flag = _bodyProperties.Age >= 14f && _isFemale;
new SkinGenerationParams(..., flag ? 1 : 0, _race, false, false, 0);
MBAgentVisuals.FillEntityWithBodyMeshesWithoutAgentVisuals(...);
```

The constructor order was verified from installed `TaleWorlds.MountAndBlade.dll`: gender is immediately before race.

```csharp
public SkinGenerationParams(..., int gender, int race, bool useTranslucency, bool useTesselation, int faceCacheID)
{
    _gender = gender;
    _race = race;
}
```

### SaveLoadHeroTableauTextureProvider

Source inspected:
`E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.GauntletUI.dll`

Relevant decompiled lines from `TaleWorlds.MountAndBlade.GauntletUI.TextureProviders.SaveLoadHeroTableauTextureProvider`:

```csharp
private BasicCharacterTableau _tableau;

public SaveLoadHeroTableauTextureProvider()
{
    _tableau = new BasicCharacterTableau();
}
```

The `HeroVisualCode` setter forwards the string to `_tableau.DeserializeCharacterCode(...)`, and `Tick` calls `_tableau.OnTick(...)`. A string search/decompile pass over installed Native UI/View DLLs found this as the relevant instantiation path for the load-save hero preview.

### MainHeroSaveVisualSupplier

Source inspected:
`E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBox/bin/Win64_Shipping_Client/SandBox.View.dll`

Relevant decompiled lines from `SandBox.View.MainHeroSaveVisualSupplier.GetMainHeroVisualCode`:

```csharp
stringBuilder.Append("4|");
Monster baseMonsterFromRace = FaceGen.GetBaseMonsterFromRace(characterObject.Race);
stringBuilder.Append(baseMonsterFromRace.BaseBodyName + "|");
stringBuilder.Append(baseMonsterFromRace.SkinMeshesMask.ToString() + "|");
stringBuilder.Append(mainHero.IsFemale.ToString() + "|");
stringBuilder.Append(mainHero.CharacterObject.Race.ToString() ?? "");
```

This confirms save metadata stores the race as an int at save time.

### FaceGen And Native Load Timing

Installed `TaleWorlds.Core.FaceGen`:

```csharp
public static IEnumerable<string> GetRaceNames()
{
    return _instance?.GetRaceNames() ?? null;
}
```

Installed `TaleWorlds.MountAndBlade.FaceGen` constructor and methods:

```csharp
_raceNamesArray = MBAPI.IMBFaceGen.GetRaceIds().Split(';');
return (string[])_raceNamesArray.Clone();
```

```csharp
if (race < 0 || race >= _raceNamesArray.Length)
{
    return null;
}
return MBObjectManager.Instance.GetObject<Monster>(_raceNamesArray[race]);
```

Installed `TaleWorlds.MountAndBlade.CoreManaged.OnLoadCommonFinished`:

```csharp
internal static void OnLoadCommonFinished()
{
    FaceGen.CreateInstance();
}
```

`FaceGen.GetRaceNames()` is null-tolerant until the instance exists. The race names become available after native common load creates the instance.

## Changed Code Review

### BasicTableauRaceGuard

No findings.

`Main/Features/HeroRace/BasicTableauRaceGuard.cs` implements the correct fail-closed order:

```csharp
if (race == HumanBaseRace)
{
    return HumanBaseRace;
}

if (!_raceManager.IsValidRaceId(race))
{
    return HumanBaseRace;
}

var raceName = _raceManager.GetRaceNameFromId(race);
return TableauSafeRaceNames.Contains(raceName) ? race : HumanBaseRace;
```

The validate-before-lookup order is important and is present. A corrupt id cannot be converted through a fallback name into an allowed race.

The catch-all exception handler is appropriate at this boundary. Harmony prefix exceptions are not swallowed by Harmony by default, and this prefix runs in the cold-menu render path that the guard exists to protect. Failing closed to human is preferable to letting a resolver failure break the load UI.

### Patch Binding

No findings.

`Main/Features/HeroRace/Hooks/BasicCharacterTableau_RefreshCharacterTableau_Patch.cs` uses a void prefix and `ref int ____race`.

Harmony 2.4.2 documentation and source confirm the semantics:

```text
A prefix can return a boolean. If it returns false, it skips prefixes that alter the result and skips the original method.
```

Void prefixes do not skip the original.

Harmony field injection uses three leading underscores for fields and honors `ref` for writable injected fields. `____race` strips to `_race`, matching the verified installed field name and type. The new binding test pins that installed field as `int`.

### IoC And Patch Timing

No findings.

`Main/IoC.cs` registers core services before `HeroRaceIoC.RegisterHeroRaceFeature(...)`, so `IRaceManager` is registered before `IBasicTableauRaceGuard` is resolved for patch initialization.

`Main/SubModule.cs` applies category `Patch55_BasicTableauRaceGuard` in `OnBeforeInitialModuleScreenSetAsRoot`, guarded by `_basicTableauGuardApplied`. This is the correct lifecycle point for the cold main-menu load UI, and it preserves the #299 timing fix.

## Known Suspects

### S1. Cross-Session Race-Index Drift

DISPUTED as a new defect; residual risk is vanilla-equivalent and bounded by what the guard can know.

The save visual code stores an int. The load preview has only that int and the current session's race table. There is no saved race name or saved module-set fingerprint in the inspected vanilla code. Therefore the guard cannot know whether a save-time int originally meant `uruk`, `orc`, `dwarf`, or another custom race under a different module order.

Data flow:

1. Save time: `MainHeroSaveVisualSupplier.GetMainHeroVisualCode` appends `mainHero.CharacterObject.Race.ToString()`.
2. Load screen: `BasicCharacterTableau.DeserializeCharacterCode` parses that string into `_race`.
3. Prefix: TAOM validates `_race` against the current `IRaceManager` table.
4. Prefix: TAOM resolves the current name for that same current id and only allows it if the current name is `uruk`.
5. Vanilla original: `RefreshCharacterTableau` passes `_race` into `SkinGenerationParams`.

If the current module set maps the saved int to `uruk`, the guard passes current-session `uruk`. A proposed hardening of "only pass when the current uruk id equals the saved int" is exactly the implemented name lookup: resolving id -> current name and comparing to `uruk`.

If Armory is disabled and the id is out of range, the guard returns human. If a different mod inserts races before `uruk`, the guard uses the current table and either returns human or current `uruk`. This can misrepresent the save's original race, but vanilla would pass the raw int to the native path regardless.

### S2. Single-Sample Empirical Verification

DISPUTED as a code defect; residual empirical asset risk remains by design.

The `uruk` race exists in installed Armory `skins.xml` exactly as allow-listed:

```xml
<race id="uruk" name="Uruk">
```

Distinct not-allow-listed race ids also exist and are not string-equal to `uruk`:

```xml
<race id="uruk_hai" name="Uruk-hai">
<race id="pale_uruk" name="Uruk">
<race id="dg_uruk" name="Uruk">
```

A parsed audit of the `uruk` race found ten skin entries. Adult male and adult female both use `human_skeleton` and `sk_uruk_basemesh_a_head`; adult hair and beard lists are placeholders rather than broad custom mesh sets. Female adult uses the same head meta mesh.

This supports the current allow-list decision for the normal adult main-hero save shape. It does not prove every maturity/body-property combination and every possible cosmetic mesh is safe. The evidence that would fully close this residual risk is either a render-test matrix across adult male/female and relevant BodyProperties hair/beard selections, or an asset audit proving every mesh reachable by `uruk` has the morph data required by the native static build path.

Given the feature is explicitly empirical and unverified races still coerce to human, this is not a P1/P2 finding against the refactor.

### S3. RaceManager Init-Latch

DISPUTED for current call paths.

`RaceManager.EnsureInitialized` does latch after a null `FaceGen.GetRaceNames()` result, falling back to a human-only map. That is a real pre-existing shape, but I found no current method-call path that reaches it before `CoreManaged.OnLoadCommonFinished` creates the native FaceGen instance.

Current consumers are constructor-injected but do not call methods in constructors. The first normal Patch55 call is `BasicTableauRaceGuard.ResolveSafeRace(...)` from the load-screen tableau prefix, after the cold menu exists. Other consumers call in character creation, tournament, mission, persistence, or diagnostics flows. `EyeHeightAdjustmentHook` has an early native hook path, but it returns before calling `RaceManager` if `GetBaseMonsterFromRace(0)` is unavailable.

If a future caller invokes `IRaceManager` too early, the current failure mode for this guard is fail-closed human previews, not native CTD.

### S4. Catch-All Exception Handler

DISPUTED.

A narrower catch would preserve more programmer-error signal, but this class is a crash guard at a Harmony prefix boundary in the menu render path. Since Harmony does not catch prefix exceptions for us, `catch (Exception) -> HumanBaseRace` is justified here. A resolver bug should not break the load-game screen.

### S5. Harmony Prefix Semantics

DISPUTED.

Harmony semantics support the patch shape:

- A prefix must return `bool false` to skip the original.
- A void prefix cannot skip the original.
- Triple-underscore field injection targets private fields.
- `ref` injection writes the mutated field value before the original runs.

The installed target field is `private int _race;`, and the binding test pins it. The pre-refactor guard also rendered custom saves as human in-game, which is practical evidence that this field-ref write path is effective.

### S6. Test Fidelity

DISPUTED.

The unit tests mock `IRaceManager`, but I did not find a mock behavior that invalidates the tested production claim. The seemingly impossible case where `GetRaceNameFromId` could return `uruk` for an invalid id is specifically paired with an `IsValidRaceId` false result to assert validate-before-lookup. That is a useful negative test: production `RaceManager` will not be asked for the name on that path.

The tests cover human no-lookup, allowed `uruk`, case-insensitivity, unverified races, invalid ids, invalid high ids, and resolver exceptions. The new binding test covers the field-name/type drift risk.

## Feature-Specific Scenarios

### A. Uruk Save, Then Armory Disabled Or Race Order Changed

No findings.

The save contains the original integer race id only. On the load screen, TAOM resolves that integer against the current session's race table.

If the id is invalid in the current session, `IsValidRaceId` returns false and the preview is human. If the id is valid but resolves to a non-allow-listed current race, the preview is human. If the id resolves to current `uruk`, the preview is current `uruk`. That can be semantically wrong relative to the save-time module set, but vanilla has the same lack of save-time race identity and would pass the raw id onward.

### B. Corrupt Race Int 999

No findings.

`ResolveSafeRace(999)` calls `IsValidRaceId(999)` before name lookup. With the real `RaceManager`, out-of-range ids are invalid, so the result is human and `GetRaceNameFromId(999)` is not called. The unit tests pin this order.

### C. ESC-Menu In-Game Save/Load Screen

No findings.

The same Native `SaveLoadHeroTableauTextureProvider` and `BasicCharacterTableau` path is used. In campaign, `FaceGen` is definitely initialized; `RaceManager` may already be initialized by character creation or other consumers. The guard's behavior is therefore the same or more stable than cold menu.

### D. Female Uruk Save

No findings.

Vanilla computes:

```csharp
bool flag = _bodyProperties.Age >= 14f && _isFemale;
new SkinGenerationParams(..., flag ? 1 : 0, _race, ...);
```

So adult female changes the gender argument, not the race argument. Installed `skins.xml` gives adult female `uruk` the same skeleton and head meta mesh family observed for adult male. That supports the allow-list for adult female uruk under the same empirical model.

## Config Cross-Reference

No findings.

`TableauSafeRaceNames` contains exactly:

```csharp
"uruk"
```

Installed Armory `skins.xml` contains exact race id `uruk`. It also contains distinct race ids `uruk_hai`, `pale_uruk`, and `dg_uruk`; these are not allow-listed by an ordinal string comparison.

`Main/_Module/ModuleData/charactercreation/cultures.json` Mordor races list contains:

```json
"races": [
  "uruk",
  "orc",
  "human"
]
```

Only Mordor's `uruk` custom race is allow-listed for tableau pass-through. `orc` and `human` behave as intended: human is base id 0, and orc is coerced to human unless separately verified later.

A source sweep for hardcoded race integers found no new inconsistent custom-race int assumptions introduced by this refactor. Remaining relevant integer assumptions are the intended human base race `0` and pre-existing human fallback checks in HeroRace infrastructure.

## Test Execution

I attempted the targeted tests:

```powershell
dotnet test TAOM.Tests\TAOM.Tests.csproj --filter "FullyQualifiedName~BasicTableauRaceGuardTests|FullyQualifiedName~Patch55BasicTableauRaceGuardBindingTests" --no-restore
```

The test run could not complete in this sandbox. First, the .NET CLI tried to write under `C:\Users\CodexSandboxOffline\.dotnet`. After redirecting `DOTNET_CLI_HOME` into the repo, MSBuild still failed while probing `C:\Users\mikew\AppData\Local\Microsoft SDKs` for the Windows SDK.

This is an environment restriction, not a test failure.

## Findings

No findings.

P1: 0 | P2: 0 | P3: 0
VERDICT: CLEAN
