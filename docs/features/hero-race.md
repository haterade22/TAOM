# Hero Race

## Overview
HeroRace ensures that non-human heroes (elves, dwarves, orcs, goblins, etc.) render with the correct skeletal monster, animations, and camera framing in character tableaux, inventory, and spawned scenes. It also persists each hero's race integer across save/load cycles to counteract Bannerlord resetting races on campaign reload.

## Why This Exists
- **Vanilla behavior:** `CharacterTableau`, `CharacterSpawner`, and `FaceGen` all assume race 0 (human). When a non-human race is set, the tableau refreshes using the human monster base, producing T-pose or wrong proportions. No race data is serialized in the vanilla save path for heroes.
- **TAOM requirement:** TAOM has 10+ custom races (dwarf, orc, goblin, elf, etc.) defined in `monsters.xml`. Each race needs its own `Monster` base when building `AgentVisuals`, and its own camera position offset so that dwarves and orcs are framed correctly in the inventory and character creation screens.
- **Without this feature:** Non-human heroes appear with human proportions in all UI views. Dwarf eye-height is at human level, making the camera clip through foreheads. Hero races revert to 0 on every campaign load, destroying any race-specific gameplay downstream.

## Architecture
### Design Challenge
`CharacterTableau` and `CharacterSpawner` are sealed TaleWorlds classes. Their `InitializeAgentVisuals` and `InitWithCharacter` methods are private, and their internal fields (`_agentVisuals`, `_agentEntity`, `_race`, etc.) are private. The methods must be fully reimplemented when a non-human race is involved.

`Monster` is also sealed, and its `StandingEyeHeight`/`CrouchEyeHeight` are auto-properties with private setters, requiring reflection to adjust.

Race is an `int` in `CharacterObject` and `Hero`, not a strong type. The mapping from `int` to race name (e.g., `2 -> "dwarf"`) lives in the game's `monsters.xml` via `IRaceManager`.

Save compatibility: Bannerlord's `SyncData` serializes `Hero` fields but not the `Race` property directly in all paths. Races must be captured before saving and restored after loading.

### Solution Approach
Four Harmony patches intercept the key rendering and race-assignment paths:

- `Patch3_SetRace` (`CharacterTableau_SetRace_Patch`) — Postfix on `CharacterTableau.SetRace`. After the race field is written, resets agent visuals and calls `InitializeAgentVisuals` so that the tableau rebuilds with the new monster base.
- `Patch4_CharacterSpawner` (`CharacterSpawner_InitWithCharacter_Patch`) — Prefix on `CharacterSpawner.InitWithCharacter`. When `characterCode.Race > 0`, fully replaces the method with `CharacterSpawnerService.InitWithCharacter`, which replicates the vanilla logic but calls `_faceGenAdapter.GetBaseMonsterFromRace(race)` and applies per-race position offsets from `RacePositionConfig`.
- `Patch5_FaceGen` (`FaceGen_GetBaseMonsterFromRace_Patch`) — Postfix on `FaceGen.GetBaseMonsterFromRace`. Delegates to `EyeHeightAdjustmentHook`, which lowers `StandingEyeHeight` and `CrouchEyeHeight` by 0.2 for the `dwarf` race via reflection.
- `CharacterTableau_RefreshCharacterTableau_Patch` — Postfix on `CharacterTableau.RefreshCharacterTableau`. Delegates to `CharacterTableauService.RefreshCharacterTableau`, which rebuilds agent visuals with race-aware position offsets read from `CharacterAvatarPatch.json`.

`RacePersistenceBehavior` (CampaignBehaviorBase) captures all hero races to a `Dictionary<string, int>` before save (`OnBeforeSaveEvent`) and restores them after session launch (`OnSessionLaunchedEvent`). The dictionary is serialized through `SyncData` under the key `_taom_heroRaceMap`.

Position offsets per race are stored in two JSON config files: `CharacterAvatarPatch.json` (inventory/avatar view) and `CharacterImagePatch.json` (character spawner). Each entry has `race`, `horizontal`, `vertical`, and `zoom` float offsets.

### Component Diagram
```
CharacterTableau.SetRace() [Postfix Patch3_SetRace]
    |-> resets _agentVisuals, calls InitializeAgentVisuals()

CharacterTableau.RefreshCharacterTableau() [Postfix]
    |-> CharacterTableauService.RefreshCharacterTableau(tableau)
            |-> IRaceManager.GetRaceNameFromId(_race)
            |-> RacePositionConfig["CharacterAvatarPatch"].Items[raceName]
            |-> applies position offset to charframe / mountframe
            |-> rebuilds AgentVisualsData with Race(race) and adjusted frame

CharacterSpawner.InitWithCharacter() [Prefix Patch4_CharacterSpawner, returns false]
    |-> CharacterSpawnerService.InitWithCharacter(spawner, characterCode)
            |-> IFaceGenAdapter.GetBaseMonsterFromRace(race)
            |-> RacePositionConfig["CharacterImagePatch"].Items[raceName]
            |-> builds AgentVisuals with correct monster base + position offset

FaceGen.GetBaseMonsterFromRace() [Postfix Patch5_FaceGen]
    |-> EyeHeightAdjustmentHook.OnGetBaseMonsterFromRace(ref result, race)
            |-> if raceName == "dwarf": adjusts StandingEyeHeight and CrouchEyeHeight via reflection

RacePersistenceBehavior (CampaignBehaviorBase)
    |-> OnBeforeSave -> RacePersistenceService.CaptureHeroRaces()
    |-> OnSessionLaunched -> RacePersistenceService.RestoreHeroRaces()
    |-> SyncData -> RacePersistenceService.SyncRaceData(dataStore)
                       serializes _taom_heroRaceMap
```

## Configuration
Two JSON files under the mod's config path (resolved via `IPathService.ConfigPath`):

| File | Purpose |
|------|---------|
| `CharacterAvatarPatch.json` | Per-race position offsets for inventory/avatar tableau (`CharacterTableauService`) |
| `CharacterImagePatch.json` | Per-race position offsets for character spawner scenes (`CharacterSpawnerService`) |

Each file contains a JSON array of objects with fields: `Race` (string, lowercase race name), `Horizontal` (float), `Vertical` (float), `Zoom` (float).

Special case: entries prefixed with `mount_` (e.g., `mount_dwarf`) are used to offset the mount position in mounted tableau views.

## Key Files
| File | Purpose |
|------|---------|
| `Main/Features/HeroRace/HeroRaceIoC.cs` | DryIoc registrations; also initializes `FaceGen_GetBaseMonsterFromRace_Patch` with the resolved hook |
| `Main/Features/HeroRace/CharacterTableauService.cs` | Rebuilds tableau agent visuals with race-aware camera offsets |
| `Main/Features/HeroRace/CharacterSpawnerService.cs` | Full reimplementation of `CharacterSpawner.InitWithCharacter` with race-aware monster base |
| `Main/Features/HeroRace/EyeHeightAdjustmentHook.cs` | Lowers dwarf eye height by 0.2 via reflection on the `Monster` struct |
| `Main/Features/HeroRace/RacePersistenceService.cs` | Captures and restores hero race integers across save/load |
| `Main/Features/HeroRace/RacePersistenceBehavior.cs` | CampaignBehaviorBase wiring for save/load events and SyncData |
| `Main/Features/HeroRace/RacePositionConfigurationService.cs` | Reads both config files and exposes race and mount position items by race name |
| `Main/Features/HeroRace/Configuration/RacePositionConfig.cs` | JSON config POCO + `LoadConfig`/`WriteConfig` helpers |
| `Main/Features/HeroRace/Hooks/CharacterTableau_SetRace_Patch.cs` | Patch3_SetRace postfix |
| `Main/Features/HeroRace/Hooks/CharacterTableau_RefreshCharacterTableau_Patch.cs` | RefreshCharacterTableau postfix |
| `Main/Features/HeroRace/Hooks/CharacterSpawner_InitWithCharacter_Patch.cs` | Patch4_CharacterSpawner prefix |
| `Main/Features/HeroRace/Hooks/FaceGen_GetBaseMonsterFromRace_Patch.cs` | Patch5_FaceGen postfix delegating to EyeHeightAdjustmentHook |
| `Main/Features/HeroRace/Hooks/ActionSetCode_GenerateActionSetNameWithSuffix_Patch.cs` | Action set suffix handling for non-human races |
| `TAOM.Tests/Features/HeroRace/EyeHeightAdjustmentHookTests.cs` | Dwarf eye-height adjustment logic |
| `TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs` | Capture/restore race dictionary |
| `TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs` | Event registration |
| `TAOM.Tests/Features/HeroRace/Configuration/RacePositionConfigTests.cs` | Config POCO loading |

## Dependencies
- `IRaceManager` — maps race int to race name string (from `TAOM.Core.Domain`)
- `IFaceGenAdapter` — wraps `FaceGen.GetBaseMonsterFromRace` (sealed type adapter)
- `IHeroRosterAdapter` — iterates all alive heroes and sets race values
- `IModLogger` — diagnostic logging

## Tests
- `EyeHeightAdjustmentHookTests.cs` — verifies that `OnGetBaseMonsterFromRace` modifies `StandingEyeHeight` and `CrouchEyeHeight` for race id mapping to "dwarf", and is a no-op for other races or race 0.
- `RacePersistenceServiceTests.cs` — verifies that `CaptureHeroRaces` skips race 0, that `RestoreHeroRaces` only calls `SetHeroRace` for heroes whose stored race differs from the current, and that `SyncRaceData` calls `dataStore.SyncData` with the correct key.
- `RacePersistenceBehaviorTests.cs` — verifies event registration bindings.
- `RacePositionConfigTests.cs` — verifies deserialization of config items and fallback to an empty config when the file is absent.

## How to Add a New Race's Position Offset
1. Determine the race name as registered in `monsters.xml` (must match `IRaceManager.GetRaceNameFromId` output, lowercase).
2. Edit `CharacterAvatarPatch.json` to add an entry: `{ "Race": "yourrace", "Horizontal": 0.0, "Vertical": 0.0, "Zoom": 0.0 }`.
3. If the race can be mounted, add a `mount_yourrace` entry for the mount offset.
4. Edit `CharacterImagePatch.json` similarly for spawner scenes.
5. Tune values in-game by equipping a hero of the race in inventory and adjusting until framing looks correct.
6. If the race requires eye-height adjustment (e.g., very short), extend `EyeHeightAdjustmentHook.OnGetBaseMonsterFromRace` with a new branch for the race name, write the test first.

## Wanderer Race Fix (2026-04-08)

Bannerlord's `BasicCharacterObject.Deserialize()` natively supports a `race=` XML attribute (lines 323-328), calling `FaceGen.GetRaceOrDefault(value)`. This means wanderer templates can declare their race directly in XML — no C# code needed.

**Changes applied to `taom_wanderers.xml`:**
- 30 elven wanderers (Rivendell 10, Mirkwood 10, Lothlorien 10): added `race="elf"`
- 10 Dol Guldur wanderers: fixed `race="orc"` to `race="dg_uruk"`, fixed `BodyProperty.fighter_empire` to `BodyProperty.fighter_dolguldur`
- 57 wanderers already had correct race attributes (Mordor, Gundabad, Isengard, Erebor)
- 83 human-culture wanderers correctly default to race 0 by omission

The existing `RacePersistenceService` automatically handles wanderer race persistence — when a wanderer is spawned from a template with `race="elf"`, the Hero inherits the race via `CharacterObject.CreateFrom()` / `FillFrom()`, and `CaptureHeroRaces()` captures it on save.

**Save compatibility:** Pre-existing wanderer heroes keep race=0 until they die and are replaced by new wanderers from updated templates. Natural wanderer turnover handles migration.

## GitHub Issue
- **Issue:** Unknown
- **Status:** Unknown

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/arena.md](./arena.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
