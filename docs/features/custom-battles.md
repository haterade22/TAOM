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

10 Harmony patches (category `Patch19_CustomBattles`) applied in `OnSubModuleLoad()` — before any CustomGame type loads:

1. **Prefix** on `CustomBattleData.Characters` getter — replaces vanilla commanders with TAOM lords
2. **Prefix** on `CustomBattleData.Factions` getter — replaces vanilla cultures with TAOM cultures
3. **Postfix** on `CustomBattleHelper.GetDefaultTroopOfFormationForFaction` — fills TAOM troops when vanilla returns null
4. **Postfix** on `BannerlordMissions.OpenCustomBattleMission` — injects team-fix behavior
5. **Postfix** on `BannerlordMissions.OpenSiegeMissionWithDeployment` — injects team-fix for siege (only for non-campaign missions)
6. **Postfix** on `CustomBattleSideVM` constructor — replaces `FactionSelectionGroup` with `TaomFactionSelectionVM` and explicitly fires the `OnCultureSelection` callback so the initial commander dropdown aligns with the visible faction (vanilla `SelectFaction(0)` doesn't fire the callback)
7. **Postfix** on `CustomBattleSideVM.OnCultureSelection(BasicCultureObject)` (private method, patched by name) — rebuilds `CharacterSelectionGroup.ItemList` filtered to the selected faction, capped at 3 commanders
8. **Postfix** on `CustomBattleSideVM.RefreshValues()` — defensive re-filter for refresh events (language/resolution change)
9. **Prefix** on `CustomBattleSideVM.OnCharacterSelection(SelectorVM<CharacterItemVM>)` — defensive null-guard on `selector.SelectedItem`. Vanilla derefs `selector.SelectedItem.Character` without a null check, but `SelectorVM<T>.SelectedIndex` setter fires `_onChange.Invoke(this)` even when `GetCurrentItem()` returns null (empty `ItemList` or out-of-range index). The Prefix returns `false` to skip the vanilla body when `SelectedItem == null`, eliminating an NRE that surfaced during in-game testing. ([CustomBattleSideVM_OnCharacterSelection_Patch.cs](../../Main/Features/CustomBattles/Hooks/CustomBattleSideVM_OnCharacterSelection_Patch.cs))
10. **Prefix** on `CustomBattleSideVM.UpdateCharacterVisual()` — sister null-guard for the OnCharacterSelection Prefix. Vanilla `RefreshValues()` calls `UpdateCharacterVisual()` unconditionally after `SelectedIndex = 1` (enemy side default), and `UpdateCharacterVisual` derefs `SelectedCharacter.Equipment[(EquipmentIndex)5]`. When the OnCharacterSelection Prefix skipped the body (preventing vanilla from setting `SelectedCharacter`), the next line in `RefreshValues` would NRE. The Prefix returns `false` when `__instance.SelectedCharacter == null`, letting `RefreshValues` continue past the visual update without crashing. (Codex Review 32 P2 — `UpdateCharacterVisual` was outside the OnCharacterSelection Prefix's blast radius despite being the same call chain.) ([CustomBattleSideVM_UpdateCharacterVisual_Patch.cs](../../Main/Features/CustomBattles/Hooks/CustomBattleSideVM_UpdateCharacterVisual_Patch.cs))

No UI patches needed — TAOM's `CustomBattleScreen.xml` GUI prefab automatically overrides vanilla via Gauntlet module load order (TAOM loads after the CustomBattle module).

### Commander filter+cap (per faction)

Vanilla `CustomBattleSideVM.RefreshValues()` adds every entry from `CustomBattleData.Characters` to `CharacterSelectionGroup.ItemList`. With TAOM's expanded lord pool, this means picking "Dunland" still showed every culture's commanders. Vanilla `OnCultureSelection` only updates banner colors — it does NOT re-filter the dropdown.

The fix is a singleton hook (`ISideCommanderFilter` / `SideCommanderFilter`) that resolves a culture's commanders via `CustomBattleService.GetCommanderIdsForFaction(factionId, takeMax)`. The service applies `OrderBy(Id, OrdinalIgnoreCase).Take(takeMax)` so the cap is deterministic across launches. Cap is `SideCommanderFilter.MaxCommandersPerCulture = 3`.

Both side-VM postfixes log a `LogWarning` to `rgl_log.txt` if a culture has zero matching commanders, so future `lords.xml` culture-tag misalignment surfaces in logs instead of silently regressing the dropdown to the unfiltered list.

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
- ID does not contain: `companion`, `child`, `tutorial`, `commander_`, `wanderer`, `notable`

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
| `Main/Features/CustomBattles/TaomFactionSelectionVM.cs` | Custom faction-selection VM with prev/next navigation |
| `Main/Features/CustomBattles/Hooks/CustomBattleSideVM_Constructor_Patch.cs` | Harmony postfix — swaps FactionSelectionGroup; fires initial OnCultureSelection callback |
| `Main/Features/CustomBattles/Hooks/CustomBattleSideVM_OnCultureSelection_Patch.cs` | Harmony postfix — filters commander dropdown on faction click (cap=3) |
| `Main/Features/CustomBattles/Hooks/CustomBattleSideVM_RefreshValues_Patch.cs` | Harmony postfix — defensive re-filter for refresh events |
| `Main/Features/CustomBattles/Hooks/ISideCommanderFilter.cs` | Hook interface — resolves commanders for a culture |
| `Main/Features/CustomBattles/Hooks/SideCommanderFilter.cs` | Hook impl — calls service with `MaxCommandersPerCulture = 3` |
| `Main/Features/CustomBattles/Hooks/CommanderSelectorRebuilder.cs` | Static helper — calls vanilla `SelectorVM<T>.Refresh(items, 0, onChange)` to safely rebuild the selector. Reads existing `_onChange` via cached `FieldInfo` so Refresh's overwrite preserves the wiring. (Issue #105 — replaced manual `Clear() + AddItem(*N) + reflection-on-_selectedIndex` approach to match the canonical safe rebuild pattern.) |
| `Main/Features/CustomBattles/Hooks/CustomBattleSideVM_OnCharacterSelection_Patch.cs` | Defensive Prefix on the private `OnCharacterSelection(SelectorVM<CharacterItemVM>)` — returns `false` when `selector?.SelectedItem == null`. Stops vanilla NRE that surfaced when `SelectedIndex` setter fires `_onChange.Invoke` with an empty `ItemList` (Issue #105 Bug 1). |
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
| `TAOM.Tests/Features/CustomBattles/CustomBattleServiceTests.cs` | 22 | Faction filtering, commander filtering, formation mapping, takeMax cap (4 dedicated tests: cap, deterministic order, fewer-than-cap, zero-cap), null/empty edge cases |
| `TAOM.Tests/Features/CustomBattles/CustomBattleCommandersHookTests.cs` | 3 | Resolution, null filtering, empty case |
| `TAOM.Tests/Features/CustomBattles/CustomBattleFactionsHookTests.cs` | 3 | Resolution, null filtering, empty case |
| `TAOM.Tests/Features/CustomBattles/CustomBattleTroopHookTests.cs` | 5 | Vanilla passthrough, TAOM resolution, null service/adapter results |
| `TAOM.Tests/Features/CustomBattles/SideCommanderFilterTests.cs` | 6 | Null/empty culture, cap=3 propagation, ID resolution, null-resolution filtering, empty result |

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
