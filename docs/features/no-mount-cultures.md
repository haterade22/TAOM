# No-Mount Cultures (Character Creation)

## Overview

Certain TAOM cultures (currently Erebor/dwarves) intentionally omit horses from character creation equipment. This requires two Harmony patches to prevent vanilla's CC narrative stage from crashing when no horse is present in a culture's battle equipment roster.

## Why This Exists

- **Vanilla behavior:** `GetYouthMenuNarrativeMenuCharacterArgs` unconditionally reads `DefaultEquipment[Horse].Item.StringId` and spawns a horse actor in the narrative scene. All vanilla culture CC rosters include horse + harness slots.
- **TAOM requirement:** Dwarves don't ride horses (lore). Their CC equipment rosters have no horse slots.
- **Without this fix:** Two cascading crashes:
  1. `NullReferenceException` in `GetYouthMenuNarrativeMenuCharacterArgs` — `DefaultEquipment[Horse].Item` is null.
  2. `ArgumentNullException("key")` in `SpawnNonHumanNarrativeMenuCharacter` — horse scene character has uninitialized (null) item ID because `ModifyMenuCharacters` never set it.

## Architecture

### Design Challenge

`MBEquipmentRoster.DefaultEquipment` returns the first Battle-type EquipmentSet after `OrderEquipments()` sorts Battle sets to the front. Adding horse to only civilian sets doesn't help — `DefaultEquipment` always returns a Battle set. The vanilla narrative scene machinery has two separate crash points: the `GetYouthMenuNarrativeMenuCharacterArgs` method (sets the horse NarrativeMenuCharacterArgs) and `SpawnNonHumanNarrativeMenuCharacter` (spawns the horse 3D actor), which runs after `ModifyMenuCharacters`.

### Solution Approach

Two thin Harmony patches in `Patch20_NarrativeHorseGuard`:

1. **Prefix on `GetYouthMenuNarrativeMenuCharacterArgs`** (private method, `CharacterCreationCampaignBehavior`): Looks up the culture's CC equipment roster. If `DefaultEquipment[Horse].Item == null`, returns a `__result` containing only the player character entry — skipping the horse NarrativeMenuCharacterArgs entirely. Vanilla runs for horse-enabled cultures (returns `true`).

2. **Finalizer on `SpawnNonHumanNarrativeMenuCharacter`** (`SandBox.GauntletUI.CharacterCreation.CharacterCreationNarrativeStageView`): Suppresses `ArgumentNullException("key")` that occurs when the horse scene character's item ID was never set (because the horse entry was skipped in step 1). The horse actor is simply not spawned.

### Component Diagram

```
taom_char_creation_equipment.xml
  └── Erebor rosters: no Horse/HorseHarness slots

Patch20_NarrativeHorseGuard
  ├── CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch [Prefix]
  │     Checks DefaultEquipment[Horse] → null → skip horse NarrativeMenuCharacterArgs
  │     → vanilla handles horse-enabled cultures unchanged
  │
  └── CharacterCreationNarrativeStageView_SpawnNonHuman_Patch [Finalizer]
        Catches ArgumentNullException("key") from null horse item ID
        → horse actor simply not spawned
```

### Crash Flow (Without Patches)

```
OnNextStage
  → TrySwitchToNextMenu
      → ModifyMenuCharacters
          → GetYouthMenuNarrativeMenuCharacterArgs   ← Crash 1: NRE (null horse item)
  → RefreshMenu
      → OnMenuChanged
          → RefreshAgentVisuals
              → SpawnNonHumanNarrativeMenuCharacter   ← Crash 2: ArgumentNullException("key")
                  → MBObjectManager.GetObject<T>(null)
```

## Configuration

### Removing horse from a culture's CC rosters

In `Main/_Module/ModuleData/equipmentsets/taom_char_creation_equipment.xml`, ensure all non-civilian `<EquipmentSet>` blocks for `player_char_creation_{culture}_*` do **not** contain:

```xml
<Equipment slot="Horse" id="Item.sumpter_horse" />
<Equipment slot="HorseHarness" id="Item.light_harness" />
```

The patches detect horse absence at runtime — no code change needed to make a culture no-mount.

### Cultures with horses (must retain Horse/HorseHarness slots)

| Culture | Reason |
|---------|--------|
| Gondor | Riders of Gondor, mounted knights |
| Mordor | Nazgul steeds, mounted wargs |
| Rivendell | Elven horses |
| All others | Vanilla-compatible, horse-enabled |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs` | Both patch classes (Prefix + Finalizer) |
| `Main/SubModule.cs` | `PatchCategory("Patch20_NarrativeHorseGuard")` registration |
| `Main/_Module/ModuleData/equipmentsets/taom_char_creation_equipment.xml` | CC equipment rosters — Erebor has no Horse slots |

## Dependencies

- `HarmonyLib` — Harmony patching framework
- `TaleWorlds.CampaignSystem.CharacterCreationContent.NarrativeMenuCharacterArgs` — struct for scene character setup
- `SandBox.GauntletUI.CharacterCreation.CharacterCreationNarrativeStageView` — accessed via `AccessTools.TypeByName` (runtime, no compile-time reference needed)

## Tests

No unit tests — both patches are thin Harmony entry points with no extractable service logic. The entire logic is a null check (`DefaultEquipment[Horse].Item != null`) and an exception type/parameter check (`ArgumentNullException { ParamName == "key" }`).

**Trailer:** `Not-tested: Harmony patch invocation (requires live game)`

## How to Add a No-Mount Culture

1. Remove `<Equipment slot="Horse" ...>` and `<Equipment slot="HorseHarness" ...>` from all non-civilian `<EquipmentSet>` blocks in `taom_char_creation_equipment.xml` for the culture.
2. No C# changes needed — the patches detect horse absence from `DefaultEquipment[Horse].Item` at runtime.
3. Verify: `grep -c 'slot="Horse"' taom_char_creation_equipment.xml` — confirm only horse-enabled cultures have the slot.

## GitHub Issues

- **Issue #49** — [Arena practice crash for all 13 TAOM cultures](https://github.com/haterade22/TAOM/issues/49) — Closed
- **Issue #50** — [Dwarf character creation crashes and horse removal](https://github.com/haterade22/TAOM/issues/50) — Closed
