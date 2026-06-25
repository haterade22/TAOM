# Codex Adversarial Review - Save/Load Hero Preview CTD Guard

Date: 2026-06-24  
Scope: issue #299, `BasicCharacterTableau.RefreshCharacterTableau` guard  
Target: installed Bannerlord v1.4.6 at `E:\Steam\steamapps\common\Mount & Blade II Bannerlord`

## Known Suspects

1. **COERCION SUFFICIENCY - CONFIRMED, medium-high confidence.**

   `_race` is the managed race selector for the native body build. Installed v1.4.6 `BasicCharacterTableau.RefreshCharacterTableau` parses `_skinMeshesMask`, `_bodyMeshType`, `_bodyDeformType`, and `_race` separately, then constructs:

   ```csharp
   new SkinGenerationParams((int)_skinMeshesMask, _underwearType,
       (int)_bodyMeshType, (int)_hairCoverType, (int)_beardCoverType,
       (int)_bodyDeformType, true, _faceDirtAmount, gender, _race, ...)
   ```

   Installed `SkinGenerationParams` stores `_race` as its own `int` field, distinct from visibility mask, body mesh type, and body deform type. Installed `SkinMask` is a visibility bitmask (`HeadVisible`, `BodyVisible`, etc.). Installed `ArmorComponent.BodyMeshTypes` / `BodyDeformTypes` are small equipment/body-shape categories (`Normal/Upperbody/Shoulders`, `Medium/Large/Skinny`), and the installed `AgentVisuals` path passes the same equipment-derived fields alongside a separate `RaceData`.

   Reasoning from field semantics: the crash story is a null morph-data dereference for the custom-race head selected by race. Coercing `_race` to 0 selects the human race mesh/morph tables; the other fields are subselectors/visibility flags within the selected race/body build and do not independently select the custom head morph table. I would not coerce `_bodyMeshType`, `_bodyDeformType`, or `_skinMeshesMask` unless in-game testing proves otherwise. The native function remains opaque, so the final arbiter is the in-game custom-race save-list test.

2. **HARMONY PRIVATE-FIELD REF-WRITE - CONFIRMED.**

   Installed v1.4.6 `BasicCharacterTableau` has `private int _race`; `DeserializeCharacterCode` sets `_race = int.Parse(...)`; `ResetProperties` sets `_race = 0`; `RefreshCharacterTableau` reads `_race` for `SkinGenerationParams`.

   TAOM patch evidence:

   - `Main/Features/HeroRace/Hooks/BasicCharacterTableau_RefreshCharacterTableau_Patch.cs:43` uses `Prefix(ref int ____race)`.
   - Four underscores are correct for a field literally named `_race`: Harmony's field parameter prefix plus the field's own leading underscore.
   - `ref` writes the field-backed injected argument before the original private method runs.
   - Sibling evidence: `Main/Features/HeroRace/Hooks/CharacterTableau_RefreshCharacterTableau_Patch.cs:38` already uses `int ____race` for the private `_race` field on `CharacterTableau`.

3. **PATCH CATEGORY - CONFIRMED for string/registration, but see finding C1 for timing.**

   - New patch category is `Patch2_RefreshTableau` at `Main/Features/HeroRace/Hooks/BasicCharacterTableau_RefreshCharacterTableau_Patch.cs:32`.
   - `SubModule.cs` already applies `_harmony.PatchCategory("Patch2_RefreshTableau")` at `Main/SubModule.cs:567`.
   - The string is not mismatched and the category is not absent.

4. **INIT-BEFORE-USE ORDERING - DISPUTED.**

   The guard object itself is initialized early:

   - `Main/SubModule.cs:95-99` calls `IoC.Configure()` during `OnSubModuleLoad`.
   - `Main/IoC.cs:72` calls `HeroRaceIoC.RegisterHeroRaceFeature(container)`.
   - `Main/Features/HeroRace/HeroRaceIoC.cs:25-27` registers/resolves `IBasicTableauRaceGuard` and calls `BasicCharacterTableau_RefreshCharacterTableau_Patch.Initialize(...)`.

   But the Harmony category is not applied until `OnGameInitializationFinished` at `Main/SubModule.cs:552-567`. Installed v1.4.6 engine decompile shows the main menu is reached without that callback:

   - `Module.OnApplicationTick` pushes the initial module screen when `LoadingFinished && GlobalGameStateManager.ActiveState == null`.
   - `Module.SetInitialModuleScreenAsRootScreen` calls `OnBeforeInitialModuleScreenSetAsRoot`, then pushes `InitialState`.
   - `MBGameManager.OnGameInitializationFinished` is the game-start/load callback that dispatches `item.OnGameInitializationFinished(game)`.

   Therefore a cold main-menu Load Game screen can instantiate/render `SaveLoadHeroTableauTextureProvider` before `Patch2_RefreshTableau` has ever been applied. This leaves the original crash path unguarded. See C1.

5. **BLAST RADIUS - CONFIRMED.**

   Decompiling installed `TaleWorlds.MountAndBlade.View.dll` and `TaleWorlds.MountAndBlade.GauntletUI.dll`, then searching for `BasicCharacterTableau`, found exactly one external construction site:

   - Installed `SaveLoadHeroTableauTextureProvider` has `private BasicCharacterTableau _tableau;` and constructor `_tableau = new BasicCharacterTableau();`.
   - Installed `CharacterTableauTextureProvider` constructs `new CharacterTableau()`, not `BasicCharacterTableau`.
   - The installed View assembly contains only the `BasicCharacterTableau` class itself and no other `new BasicCharacterTableau()` call.

   Scope is save/load preview only.

6. **SECOND SOURCE OF TRUTH - CONFIRMED clean.**

   Hardcoding `HumanBaseRace = 0` is correct here. Installed evidence:

   - `BasicCharacterTableau.ResetProperties` sets `_race = 0`.
   - `BasicCharacterObject.Deserialize` defaults `Race = 0` before reading any `race=` XML attribute.
   - `FaceGen.GetRaceOrDefault(string)` falls back to `0` when the FaceGen instance is absent.

   This guard is intentionally engine-contract based, not TAOM-config based. Consulting `IRaceManager` would create a failure mode where a broken or missing dynamic map could preserve an unsafe custom race in the native crash path. A shared constant would be cosmetic, not a correctness requirement.

## Additional Findings

The main issue is the #4 lifecycle dispute: the patch is initialized but not applied before the cold main-menu save-list render that it is meant to protect. I found no additional independent code defect beyond that timing bug.

One coverage observation: `BasicTableauRaceGuardTests` validate the pure allow-list, but no test or static guard pins that the Harmony category is applied before `InitialState`/save-list rendering.

## Test Review

`BasicTableauRaceGuardTests` are non-vacuous for the service:

- If `ResolveSafeRace` returned the input race, `ResolveSafeRace_DwarfRace_CoercesToHuman`, `ResolveSafeRace_ElfRace_CoercesToHuman`, `ResolveSafeRace_NegativeInvalidRace_CoercesToHuman`, and `ResolveSafeRace_UnknownLargeRace_CoercesToHuman` would fail.
- The human base preservation case is covered.
- The pure service does not need every custom race id because the implementation is an allow-list: all non-0 ids follow the same branch.

Missing coverage:

- No test exercises `BasicCharacterTableau_RefreshCharacterTableau_Patch.Prefix` with a fake guard to prove the ref assignment path.
- No test/static assertion covers lifecycle placement: the reviewed bug would pass the current tests because the service is correct while the Harmony category is too late.

Attempted command:

```powershell
dotnet test TAOM.Tests\TAOM.Tests.csproj --filter BasicTableauRaceGuardTests --no-restore
```

Result: not run to completion in this sandbox. First attempt failed on dotnet first-run sentinel creation under `C:\Users\CodexSandboxOffline`; rerun with workspace-local `DOTNET_CLI_HOME` then failed during MSBuild SDK lookup with denied access to `C:\Users\mikew\AppData\Local\Microsoft SDKs`.

## Findings Or Observations

### CRITICAL

[CRITICAL] Main/SubModule.cs:567 — Patch timing — `BasicCharacterTableau_RefreshCharacterTableau_Patch` is in `Patch2_RefreshTableau`, but that category is applied only in `OnGameInitializationFinished`; the original crash path is the cold main-menu Load Game preview, and installed engine flow reaches `InitialState`/`SaveLoadHeroTableauTextureProvider` before any game-init callback dispatches, so the prefix is not attached when the save-list thumbnail first renders — Split the save/load guard into its own category and apply it once before the initial module screen can render, e.g. from `OnBeforeInitialModuleScreenSetAsRoot` with a dedicated process-static guard. Do not apply the whole `Patch2_RefreshTableau` twice unless duplicate application is also guarded.

### LOW

[LOW] TAOM.Tests/Features/HeroRace/BasicTableauRaceGuardTests.cs:14 — Coverage gap — The tests prove the pure allow-list but cannot catch a dead/late Harmony category, which is the actual integration risk for this crash guard — Add a narrow prefix unit test for `ref ____race` behavior and a static/lifecycle test or review guard that pins the BasicCharacterTableau guard category to a pre-`InitialState` application point.

## Summary

CRITICAL: 1 | HIGH: 0 | MEDIUM: 0 | LOW: 1  
VERDICT: ISSUES FOUND
