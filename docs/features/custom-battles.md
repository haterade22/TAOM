# Custom Battles

## Overview

TAOM Custom Battle support replaces vanilla factions, commanders, and troops in the Custom Battle screen with TAOM-specific content. All TAOM cultures (Gondor, Mordor, Rohan, etc.) appear as selectable factions, their lords appear as commanders, and each culture's militia/elite troops are assigned to the correct formation slots. A team-fix MissionBehavior prevents friendly fire bugs in both field and siege custom battles.

## Why This Exists

- **Vanilla behavior:** Custom Battle lists vanilla factions (Empire, Sturgia, Aserai, etc.) and 24 hardcoded vanilla commanders. Troop formation defaults are hardcoded per vanilla culture.
- **TAOM requirement:** Total conversion mod needs TAOM cultures, TAOM lords as commanders, and TAOM troops in formations. Without this, Custom Battle is unusable for testing mod content.
- **Without this feature:** Players see vanilla faction names, commanders resolve to null (no `commander_1` etc. in TAOM data), and troops fall through to null causing crashes or empty armies.

## Architecture

### Design Challenge

`CustomBattleData.Characters` and `CustomBattleData.Factions` are static property getters on a **struct** that yield hardcoded vanilla IDs. `CustomBattleHelper.GetDefaultTroopOfFormationForFaction` uses a giant switch statement on vanilla culture string IDs. All three must be intercepted before the Custom Battle screen initializes.

### Solution Approach

5 Harmony patches (category `Patch19_CustomBattles`) applied in `OnSubModuleLoad()` — before any CustomGame type loads:

1. **Prefix** on `CustomBattleData.Characters` getter — replaces vanilla commanders with TAOM lords
2. **Prefix** on `CustomBattleData.Factions` getter — replaces vanilla cultures with TAOM cultures
3. **Postfix** on `CustomBattleHelper.GetDefaultTroopOfFormationForFaction` — fills TAOM troops when vanilla returns null
4. **Postfix** on `BannerlordMissions.OpenCustomBattleMission` — injects team-fix behavior
5. **Postfix** on `BannerlordMissions.OpenSiegeMissionWithDeployment` — injects team-fix for siege (only for non-campaign missions)

No UI patches needed — TAOM's `CustomBattleScreen.xml` GUI prefab automatically overrides vanilla via Gauntlet module load order (TAOM loads after the CustomBattle module).

### Component Diagram

```
CustomBattleData.Characters [HarmonyPrefix]
    |
CustomBattleData_Characters_Patch
    |
IOnGetCustomBattleCommanders (hook interface)
    |
CustomBattleCommandersHook
    |--- ICustomBattleService.GetCommanderIds()  (string IDs only)
    |--- IObjectManagerAdapter.GetBasicCharacter(id)  (resolves to TW type)
    |
    v
IEnumerable<BasicCharacterObject> returned to game
```

Same pattern for Factions and Troops.

## Configuration

No external configuration files. All data loaded dynamically from `Game.Current.ObjectManager` at runtime.

### Faction Selection Criteria
- `CanHaveSettlement = true`
- `IsBandit = false`
- Non-empty culture ID

### Commander Selection Criteria
- `IsHero = true`
- ID does not contain: `companion`, `child`, `tutorial`, `commander_`

### Formation-to-Troop Mapping
| Formation | Troop Source | Fallback |
|-----------|-------------|----------|
| Infantry (0) | `CultureObject.MeleeMilitiaTroop` | `BasicTroop` |
| Ranged (1) | `CultureObject.RangedMilitiaTroop` | none |
| Cavalry (2) | `CultureObject.EliteBasicTroop` | none |
| Horse Archer (3) | `CultureObject.RangedEliteMilitiaTroop` | none |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CustomBattles/ICustomBattleService.cs` | Service interface — faction/commander/troop queries |
| `Main/Features/CustomBattles/CustomBattleService.cs` | Service implementation with caching |
| `Main/Features/CustomBattles/CustomBattlesIoC.cs` | DryIoc registration + hook initialization |
| `Main/Features/CustomBattles/CustomBattleTeamFixBehavior.cs` | MissionBehavior ensuring teams are enemies |
| `Main/Features/CustomBattles/Hooks/IOnGetCustomBattleCommanders.cs` | Hook interface for commander replacement |
| `Main/Features/CustomBattles/Hooks/IOnGetCustomBattleFactions.cs` | Hook interface for faction replacement |
| `Main/Features/CustomBattles/Hooks/IOnGetDefaultTroopOfFormation.cs` | Hook interface for troop assignment |
| `Main/Features/CustomBattles/Hooks/CustomBattleCommandersHook.cs` | Hook impl — resolves commander IDs to objects |
| `Main/Features/CustomBattles/Hooks/CustomBattleFactionsHook.cs` | Hook impl — resolves faction IDs to objects |
| `Main/Features/CustomBattles/Hooks/CustomBattleTroopHook.cs` | Hook impl — resolves troop IDs to objects |
| `Main/Features/CustomBattles/Hooks/CustomBattleData_Characters_Patch.cs` | Harmony prefix — replaces Characters getter |
| `Main/Features/CustomBattles/Hooks/CustomBattleData_Factions_Patch.cs` | Harmony prefix — replaces Factions getter |
| `Main/Features/CustomBattles/Hooks/CustomBattleHelper_Troop_Patch.cs` | Harmony postfix — fills TAOM troops |
| `Main/Features/CustomBattles/Hooks/BannerlordMissions_CustomBattle_Patch.cs` | Harmony postfix — injects team fix |
| `Main/Features/CustomBattles/Hooks/BannerlordMissions_Siege_Patch.cs` | Harmony postfix — injects team fix for siege |
| `Main/Adapters/IObjectManagerAdapter.cs` | Adapter interface + CultureInfo/CharacterInfo DTOs |
| `Main/Adapters/ObjectManagerAdapter.cs` | ObjectManager bridge implementation |
| `Main/_Module/GUI/Prefabs/CustomBattle/` | 5 Gauntlet UI prefab XMLs (pre-existing) |

## Dependencies

- **CustomBattle module** — vanilla DLL providing `CustomBattleData`, `CustomBattleHelper`, `CustomBattleScreen`
- **Harmony** — patch framework for intercepting static property getters and method calls
- **DryIoc** — IoC container for service/hook registration
- **SubModule.xml** — declares `<DependedModule Id="CustomBattle" />` and registers troop trees, cultures (XSLT + custom), and lord characters for `CustomGame`/`EditorGame`. **Critical:** Lord/culture XMLs MUST be registered for `CustomGame` — without this, ObjectManager has no heroes in Custom Battle mode, causing NRE in `CustomBattleSideVM.OnCharacterSelection`

## Tests

| Test File | Methods | Coverage |
|-----------|---------|----------|
| `TAOM.Tests/Features/CustomBattles/CustomBattleServiceTests.cs` | 18 | Faction filtering, commander filtering, formation mapping, null/empty edge cases |
| `TAOM.Tests/Features/CustomBattles/CustomBattleCommandersHookTests.cs` | 3 | Resolution, null filtering, empty case |
| `TAOM.Tests/Features/CustomBattles/CustomBattleFactionsHookTests.cs` | 3 | Resolution, null filtering, empty case |
| `TAOM.Tests/Features/CustomBattles/CustomBattleTroopHookTests.cs` | 5 | Vanilla passthrough, TAOM resolution, null service/adapter results |

Patches and `CustomBattleTeamFixBehavior` are thin entry points — tested indirectly via in-game smoke tests per ADR-002.

## How-To

### How to add a new TAOM culture to Custom Battle

1. Create the troop tree XML in `Main/_Module/ModuleData/troops/troops_{culture}.xml`
2. Register it in `SubModule.xml` with `<GameType value="CustomGame"/>` and `<GameType value="EditorGame"/>`
3. Ensure the culture has `CanHaveSettlement="true"` in its `SPCultures` definition
4. The CustomBattleService will automatically pick it up — no code changes needed

### How to add a new commander

1. Define the lord/hero NPC in the culture's characters XML (e.g., `characters/npcs_{culture}.xml`)
2. Register the characters XML in `SubModule.xml` with `CustomGame`/`EditorGame` game types
3. Ensure the character has `is_hero="true"` and an ID that doesn't contain `companion`, `child`, `tutorial`, or `commander_`
4. The service will automatically include them

### How to change formation troop assignments

The formation mapping uses culture militia properties from `BasicCultureObject`/`CultureObject`. To change which troop appears for a formation:
- Modify the culture's `melee_militia_troop`, `ranged_militia_troop`, `elite_basic_troop`, or `ranged_elite_militia_troop` attributes in the culture XML definition.
