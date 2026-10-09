# CompanionTactics

## Overview

Three independently-toggleable battle-tactics features bundled in one TAOM module:

1. **CompanionRoles** — equipment-based combat-role detector (11 roles); appends a role badge to companion tooltips on the party screen and OOB hero items.
2. **FormationPresets**: named Order of Battle layouts (formation types, captains and hero troops) that Save captures and Load applies through vanilla's own flows; injects an Assign Heroes (Auto-Assign) button and a Presets button into the Order of Battle screen.
3. **BattleActionBar** — context-sensitive on-screen action bar that appears in field battles. 1–9 hotkeys toggle stance buttons (Hold Fire, Brace, Shield Wall, etc.). **Stances are display-only — they record state but do NOT change formation behavior** (the original developer's mod was UI-only here; the engine doesn't expose the firing-order / tighten-spacing APIs the original referenced).

Ported from `Downloads/Features_fixed/CompanionTactics/` (Bannerlord 1.3 mod template) for TAOM v1.3.15. Patch35 reserves the Harmony category. SaveableTypeDefiner BaseId 726900601 (matches the original mod for save-import compat).

Formation presets (Save and Load) follow the behaviour of yotthani's HoN FormationPresetManager; nothing was copied, and the code is TAOM's own. His HoN code is MIT (the maintainer's statement of 2026-10-09), and CompanionTactics, which began as his HoN mod, carries his MIT notice in `Main/_Module/THIRD-PARTY-LICENSES.txt`. See [adopt-yotthani-2026-10-08.md](../reviews/adopt-yotthani-2026-10-08.md) and [provenance-register.md](../reference/provenance-register.md).

## Why This Exists

Bannerlord's vanilla Order of Battle and party screens give the player no signal about which troops or companions are best suited for which formation, no quick way to save a formation layout for re-use across battles, and no in-battle quick-control surface for issuing common stance commands.

- **Vanilla behavior:** Hover party screen → generic name + portrait; OOB → drag heroes manually each battle, no presets; in battle → use F1–F12 or click+drag for orders, no contextual buttons.
- **TAOM requirement:** A single feature module that adds (a) glanceable role badges, (b) per-campaign preset persistence with a refuse-on-overflow cap, (c) a contextual action bar in field battles for common stance commands.
- **Without this feature:** Players manually re-assign heroes every battle and have no in-battle stance UI; companion role at a glance is unavailable.

## Architecture

### Design Challenge

Three sub-features with different lifetimes and scopes:

| Sub-feature | Scope | TaleWorlds touch points |
|---|---|---|
| Roles | Campaign + UI (persistent cache) | `Hero.BattleEquipment`, `Hero.CharacterObject.IsRanged`, `WeaponClass`, `EquipmentIndex` |
| FormationPresets | Per-campaign (saveable) + battle UI | `OrderOfBattleVM`, `MissionGauntletOrderOfBattleUIHandler`, reflection on `_dataSource` + `_isActive` |
| BattleActionBar | Per-mission (transient) | `Mission`, `Formation`, `MissionMode`, `GauntletLayer`, `MBBindingList<T>` |

ADR-007 mandates services see only `IXxxAdapter`. Sealed `Hero` / `Agent` / `Equipment` cross the boundary only at adapter implementations + boundary classes (Harmony patches, MissionView, ViewModels, OOBOverlayService).

`OrderOfBattleHeroItemVM.GetCaptainTooltip()` is **private**, so attribute binding (`[HarmonyPatch]`) cannot patch it. Manual `AccessTools.Method` wiring in `ManualPatchApplicator.cs` is required.

`MissionGauntletOrderOfBattleUIHandler` exposes `_dataSource` (the OOB VM) and `_isActive` (open/closed flag) only as private fields — `OOBOverlayService` reflects them once at first call and falls into inert mode if they're missing on a future Bannerlord update.

### Solution Approach

```
TAOM.Features.CompanionTactics/
├── Roles/                          ← campaign-time role detection
│   ├── ICompanionRoleService → CompanionRoleService    (pure, equipment-only)
│   ├── IRoleTooltipDecorator → RoleTooltipDecorator    (mutates Vanilla VM tooltips)
│   └── Hooks/Patch35_*                                 (3 Harmony postfixes)
│
├── FormationPresets/               ← saveable preset CRUD + OOB UI overlay
│   ├── IFormationPresetService → FormationPresetService  (refuses save when at MaxFormationPresets)
│   ├── IHeroAutoAssigner → HeroAutoAssigner              (role scoring + PlanCaptains; consumes ICompanionRoleService)
│   ├── IOrderOfBattleVMTracker → OrderOfBattleVMTracker  (captures VM ref from ctor postfix)
│   ├── IOOBOverlayService → OOBOverlayService            (GauntletLayer + LoadMovie)
│   ├── IOOBCaptainAutoAssigner → OOBCaptainAutoAssigner  (boundary: applies PlanCaptains through vanilla's accept-captain path)
│   ├── IOOBPresetApplier → OOBPresetApplier              (boundary: Capture reads the live layout, Apply drives vanilla's class selector and accept paths)
│   ├── FormationPresetLayout                             (pure mapper: capture, class resolution, hero plan, class pass loop)
│   ├── UI/OOBButtonsVM                                   (Save/Load/Delete inquiry chain; Assign Heroes command)
│   ├── Models/HoNFormationPreset                          ([SaveableField] BaseId 726900601 / class 101)
│   ├── Models/FormationPresetSaveableTypeDefiner
│   └── Hooks/                                             (4 Harmony patches)
│       FormationPresetCampaignBehavior                   (try/catch SyncData → degrade to empty on collision)
│
└── BattleActionBar/                ← per-mission context bar
    ├── IBattleActionBarService → BattleActionBarService   (gates Volley on EnableVolleyFire)
    ├── IFormationCompositionAnalyzer → FormationCompositionAnalyzer
    ├── ITroopStanceManager → TroopStanceManager           (per-formationIndex stance dict)
    ├── UI/{BattleActionBarVM, ActionButtonVM}
    └── Hooks/
        BattleActionBarMissionView                        (MissionView NOT Harmony; field battles only)
        Patch35_Formation_SetMovementOrder                (clears stance on move when CancelStanceOnMove; lives in shared Patch_MissionTime_SetMovementOrder category — see below)
```

Adapters added/extended:
- `IBattleEquipmentSnapshot` (NEW) — value-object snapshot of `Equipment`'s 4 weapon slots + has-shield + has-mount.
- `IHeroCombatAdapter` (NEW) — campaign-time wrapper for `Hero` exposing `StringId`, `BattleEquipment` snapshot, `HasMount`, `HasShield`.
- `IAgentCombatAdapter` (NEW) — mission-time wrapper for `Agent` exposing `Index`, `IsRanged`, weapon-class, has-shield, has-mount.
- `IFormationAdapter` (EXTENDED) — added `FormationIndex`, `RangedUnitCount`, `CavalryUnitCount`, `PolearmUnitCount`, `ShieldUnitCount` (last two TTL-cached at 500ms in `FormationAdapter`).

### Component Diagram

```
TaomSettings (MCM, GroupOrder 27/28/29)
        |
   ICompanionTacticsSettingsProvider
        |
   ┌────┼─────┬────────────┐
   v    v     v            v
Roles  Presets  ActionBar
 |       |        |
 |    HoNFormationPreset (SaveableField BaseId 726900601)
 |       |        |
 v       v        v
Patch35_*  FormationPresetCampaignBehavior  BattleActionBarMissionView
   |          |                                 |
   v          v                                 v
TaleWorlds VMs   IDataStore.SyncData       GauntletLayer + LoadMovie
                                           ("BattleActionBar.xml")
```

### Auto-Assign (Assign Heroes button)

The overlay's Assign Heroes button places the player's team heroes as captains of the open formations that suit their equipment. Candidates are every hero vanilla lists on this screen except the player: companions, family members and any other hero vanilla puts on the player's team. `OOBButtonsVM.ExecuteAssignCharacters` hands the live `OrderOfBattleVM` to `OOBCaptainAutoAssigner`, which builds the candidate and slot lists and asks `HeroAutoAssigner.PlanCaptains` for the matching.

- **General only.** When `OrderOfBattleVM.IsPlayerGeneral` is false the button reports "Only the general of this battle can assign heroes." and changes nothing.
- **Open slots:** every formation with a troop class set (`HasFormation`) and no captain, in formation-index order. A formation whose class is `Unset` is never filled.
- **Candidates:** heroes in `UnassignedHeroes` and in each formation's `HeroTroops`. The player's own hero is never placed, a hero already leading a formation is never moved, and disabled items and agents that do not resolve to a campaign `Hero` are skipped. A candidate's role comes from its agent's spawn equipment (`Agent.SpawnEquipment`, falling back to `Hero.BattleEquipment`), not the campaign gear: in a siege assault vanilla spawns every agent without a horse, so a companion who owns one is placed by weapons on a foot formation, the only classes a siege offers.
- **Matching:** a global greedy on the `HeroAutoAssigner` score. Every (hero, slot) pair scoring above 0 is sorted by score, ties going to the lower formation index and then the earlier hero; a pair is taken when neither its hero nor its slot is used yet. A hero with no recognised role (`Unknown`) scores 0 on every class Auto-Assign fills (1 to 6) and is not placed.
- **Applying:** each pick runs vanilla's own click path (clear the selection, `OrderOfBattleHeroItemVM.OnHeroSelection`, then `OrderOfBattleFormationItemVM.ExecuteAcceptCaptain`), so the result equals a manual pick and vanilla keeps every side effect. A pick counts only when the slot's `Captain` is the hero afterwards; a miss logs a `[FormationPresets] Auto-Assign: vanilla did not accept` warning. Captains already placed are kept. No reflection.
- **Messages:** "Captains assigned: {COUNT}." or "No hero suits an open captain slot." (localized, keys `taom_oob_autoassign_*`).
- **Visibility:** the overlay attaches only while `EnableFormationPresets` is on (default off) and a campaign is running.
- **Co-op:** no gate. The button is local player input through vanilla's public OOB handlers, exactly like a manual assignment. Those handlers change live mission state (`agent.Formation`, `Formation.Captain`, the formation banner). Whether BannerlordCoop syncs OOB choices is unverified; this change adds nothing a manual assignment does not already do.
- **Save data:** TAOM adds none (no `[SaveableField]`, no `SyncData`, no MCM setting). Vanilla still persists the final layout, auto-assigned captains included: `SPOrderOfBattleVM.SaveConfiguration` writes it to `OrderOfBattleCampaignBehavior` when deployment ends, as it does for a manual layout, and restores it next battle.
- **Formation class numbering** is vanilla `TaleWorlds.Core.DeploymentFormationClass`: 0=Unset, 1=Infantry, 2=Ranged, 3=Cavalry, 4=HorseArcher, 5=InfantryAndRanged, 6=CavalryAndHorseArcher. Class 5 prefers melee heroes and accepts ranged ones; class 6 prefers cavalry and accepts horse archers.

### Presets (Save and Load)

A preset is a named copy of the Order of Battle layout: the formation type of each formation, which hero captains it, and which heroes are its hero troops. The Presets button opens a menu with Save, Load and Delete. `OOBButtonsVM` hands the live `OrderOfBattleVM` to `OOBPresetApplier`; the mapping itself is the pure `FormationPresetLayout`, which takes plain snapshots and no TaleWorlds types.

- **General only.** When `IsPlayerGeneral` is false the Presets button shows one message ("Only the general of this battle can use presets.") and opens no menu, because vanilla's accept-captain path does extra work for a player who is not the general.
- **Campaign only.** The overlay attaches only while `EnableFormationPresets` is on and a campaign is running. Only the campaign behavior persists the preset store (`SyncData`) or resets it (a new game), so a preset saved in Custom Battle would be lost; and Custom Battle agents are `BasicCharacterObject` (`TaleWorlds.MountAndBlade.CustomBattle.dll` references no `TaleWorlds.CampaignSystem`), so Auto-Assign finds no hero there either.
- **Save:** `OOBPresetApplier.Capture` reads every formation (`Formation.Index`, `GetOrderOfBattleClass()`, the captain and the hero troops) and `FormationPresetLayout.Capture` fills the preset. `FormationClasses[index]` is the class, or -1 when it is Unset (vanilla's own `SaveConfiguration` decides "unset" from the class alone). A captain is stored in `HeroFormationAssignments` and `CaptainHeroIds`; a hero troop only in `HeroFormationAssignments`. The player's own hero is included: the general's own spot is part of a layout. Braces in the typed name are removed, because the text processor reads them as markup. The message shows the counts ("Formation types: {CLASSES}, captains: {CAPTAINS}, hero troops: {TROOPS}"). The model is unchanged, so presets saved by the earlier name-only build still load (they apply nothing).
- **Load, classes first.** Each saved class greater than 0 is resolved against the classes the formation offers: the saved class when offered, otherwise vanilla's siege mapping (HorseArcher to Ranged, Cavalry to Infantry, CavalryAndHorseArcher to InfantryAndRanged) when that is offered, otherwise skipped. A formation that already has the class counts as set. Otherwise the applier sets `FormationClassSelector.SelectedIndex`, the same path as the player's dropdown pick. It never selects Unset (that would unassign the captain and the hero troops). The UI refuses a class change while the formation is the only formation set to a class this battle has troops of (`IsAdjustable`), so the changes run in passes: a pass applies what it can, and the loop repeats while the last pass applied at least one. A class step no pass could apply is counted as skipped, and logged as one debug line under `FormationPresetsDebug`; a change vanilla accepts but does not carry out is logged as a `[FormationPresets] Load` warning.
- **What vanilla does with troops on a class change.** The pick moves troops only as it does for the player's own pick: all troops of the class come in only when no other formation has the class; a class that another formation already has starts at 0 percent. A preset stores no troop shares (class weights) and no filters.
- **Load, then heroes.** Heroes go only into formations whose saved class is in place: a formation whose class step was blocked or could not be mapped takes no heroes (they would land in a formation of the wrong type), and each of those heroes is counted as skipped. Captains first, then hero troops, one hero at a time through vanilla's click path (clear the selection, select the hero with `OrderOfBattleHeroItemVM.OnHeroSelection`, then the formation's `ExecuteAcceptCaptain` or `ExecuteAcceptHeroTroops`), each verified afterwards (`Captain` is the hero, or `HeroTroops` contains it). A hero already in the right place needs no step. A hero the preset does not mention gets no step of its own, but a saved captain displaces the formation's current captain to the unassigned list, as a manual pick does. A hero who is not in this battle (dead or absent) is skipped on every load.
- **Message:** the loaded line shows the counts of parts that match the preset after the load (already in place or placed now). When some saved parts did not fit, a second line shows "Saved assignments that do not fit this battle: {COUNT}."
- **Vanilla's own memory:** vanilla already saves the last layout per battle type when deployment ends and reloads it when the screen opens. Presets are named layouts on top of that, and after a Load vanilla stores the loaded layout as the last one.
- **Strings:** every overlay string is a `taom_oob_*` key in `taom_module_strings.xml`; OK and Cancel are vanilla's own `str_ok` and `str_cancel` keys. The 12 language files get their rows from the next translation run (#782).
- **Co-op:** `EnableFormationPresets` counts toward the co-op settings check (only `FormationPresetsDebug` is exempt, in `CoopSettingsRelevance`). Presets are local UI and campaign data.
- **In-game checklist before the toggle goes on by default:**
  - Save a populated preset, save the campaign, reload it, check the button reads "Presets (N)", then Load the preset.
  - Check the Save counts against what the screen shows.
  - Check the next battle opens with the loaded layout (vanilla's last-layout store).
  - Swap two single-class formations: expect them counted as skipped and no hero misplaced.
  - Load a preset with a shared class: the second formation starts at 0 percent.
  - Load a field-battle preset in a siege: mounted classes map to foot classes.
  - Open Custom Battle with the toggle on: no overlay.
  - In a large battle, load a preset that moves every hero and watch for a hitch on OK.
  - Turning the feature on by default later means renaming the setting: a flipped default never reaches saved MCM settings.

## Configuration

### MCM Settings (TaomSettings.cs, GroupOrder 27/28/29)

> NOTE: GroupOrder 22/23/24 was originally planned but `SmartCavalryAI` parallel port consumed 22; we use 27/28/29 to land after FiefManagement (26).

| Setting | Default | Description |
|---|---|---|
| **Companion Roles (Group 27)** | | |
| `EnableCompanionRoleTooltips` | true | Append role prefix `[BOW]`/`[INF]`/etc. to party-screen tooltips. |
| `EnableOOBRoleDisplay` | true | Show role indicators on OOB hero items + captain tooltips. |
| `CompanionRolesDebug` | false | HUD diagnostics. |
| **Formation Presets (Group 28)** | | |
| `EnableFormationPresets` | **false** | Presets save and load the formation types, captains and hero troops of the Order of Battle, per campaign. **Off by default until it is checked in game; opt in to try it.** |
| `MaxFormationPresets` | 10 (1–20) | Save attempts beyond this are refused with a warning ("Preset limit reached. Delete one before saving."). |
| `FormationPresetsDebug` | false | HUD diagnostics. |
| **Battle Action Bar (Group 29)** | | |
| `EnableBattleActionBar` | true | Show contextual action bar in field battles only (NOT siege). |
| `CancelStanceOnMove` | true | Clear stance state when formation receives a movement order (postfix on `Formation.SetMovementOrder`). |
| `EnableVolleyFire` | true | Include Volley Fire as a ranged-action button. |
| `BattleActionBarDebug` | false | HUD diagnostics — logs composition flags per refresh. |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CompanionTactics/CompanionTacticsIoC.cs` | DryIoc registrations (all `Reuse.Singleton`) |
| `Main/Features/CompanionTactics/CompanionTacticsSettingsProvider.cs` | Caches `TaomSettings.Instance` on its first non-null read and reads through it, so MCM edits apply live (no reflection) |
| `Main/Features/CompanionTactics/Roles/CompanionRoleService.cs` | 11-role classifier with hero-StringId-keyed cache + equipment fingerprint signature |
| `Main/Features/CompanionTactics/Roles/RoleTooltipDecorator.cs` | Cached `PropertyInfo` mutations of vanilla VMs' Name + tooltip cache |
| `Main/Features/CompanionTactics/Roles/Hooks/Patch35_*.cs` | 3 Harmony postfixes + 1 manual patch (private GetCaptainTooltip) |
| `Main/Features/CompanionTactics/FormationPresets/FormationPresetService.cs` | CRUD with `MaxFormationPresets` enforcement |
| `Main/Features/CompanionTactics/FormationPresets/Models/HoNFormationPreset.cs` | `[SaveableField]` POCO; 5 fields (ids 1,2,4,5,6 — id 3 retired, was unserializable `DateTime`) |
| `Main/Features/CompanionTactics/FormationPresets/Models/FormationPresetSaveableTypeDefiner.cs` | BaseId 726900601, class 101 |
| `Main/Features/CompanionTactics/FormationPresets/OOBCaptainAutoAssigner.cs` | Auto-Assign boundary: builds open slots and candidates from the live `OrderOfBattleVM`, applies `PlanCaptains` through vanilla's select-then-accept path. Recheck on every engine bump |
| `Main/Features/CompanionTactics/FormationPresets/FormationPresetLayout.cs` | Pure mapper and its plain records: `Capture`, `CountOf`, `ResolveClass` (siege mapping), `PlanClasses`, `RunClassPasses`, `PlanHeroes`, and `PlanLoad`, which runs the three in order and plans heroes only for formations whose class step applied |
| `Main/Features/CompanionTactics/FormationPresets/OOBPresetApplier.cs` | Presets boundary: `Capture` reads the live `OrderOfBattleVM`; `Apply` drives the class selector and the accept paths with a verify after each step. Recheck on every engine bump |
| `Main/Features/CompanionTactics/FormationPresets/IOOBPresetApplier.cs` | Seam the overlay VM tests fake |
| `Main/Features/CompanionTactics/FormationPresets/Models/PresetApplyResult.cs` | Load outcome: classes set, captains placed, hero troops placed, skipped |
| `Main/Features/CompanionTactics/FormationPresets/OOBOverlayService.cs` | Cached-FieldInfo reflection on `_dataSource`, `_isActive`; `GauntletLayer` lifecycle |
| `Main/Features/CompanionTactics/FormationPresets/Hooks/FormationPresetCampaignBehavior.cs` | `SyncData` with try/catch — guards the LOAD/ref path only (NOT the off-thread save write); degrades to empty on BaseId collision |
| `Main/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs` | MissionView; field-battle-only `GauntletLayer` attach + 0.5s refresh + 1–9 hotkey input |
| `Main/Features/CompanionTactics/BattleActionBar/Hooks/Patch35_Formation_SetMovementOrder.cs` | Implements `CancelStanceOnMove`. Belongs to shared `Patch_MissionTime_SetMovementOrder` category (applied once from `OnMissionBehaviorInitialize` because `MovementOrder.cctor` reads `Mission.Current.CurrentTime`). |
| `Main/Adapters/{I,}BattleEquipmentSnapshot.cs` | Equipment value-object snapshot (no sealed `Equipment` leak) |
| `Main/Adapters/{I,}HeroCombatAdapter.cs` | `Hero` wrapper; classifies from `BattleEquipment`, or from a given `Equipment` (Auto-Assign passes the agent's spawn equipment) |
| `Main/Adapters/{I,}AgentCombatAdapter.cs` | Mission-time `Agent` wrapper |
| `Main/Adapters/IFormationAdapter.cs` (EXTENDED) | Adds `FormationIndex` + 4 unit-count properties |
| `Main/Adapters/FormationAdapter.cs` (EXTENDED) | TTL-cached polearm/shield count scan |
| `Main/_Module/GUI/Prefabs/BattleActionBar.xml` | Bound to `BattleActionBarVM` (vanilla brushes only) |
| `Main/_Module/GUI/Prefabs/OOBButtonsOverlay.xml` | Bound to `OOBButtonsVM` (vanilla brushes only) |

## Dependencies

- `ICompanionTacticsSettingsProvider`: typed read through a cached `TaomSettings.Instance` (testable seam).
- `IModLogger` (TAOM core) — file logger; gated HUD output via Debug toggles.
- `IFormationAdapter`, `IHeroCombatAdapter`, `IAgentCombatAdapter`, `IBattleEquipmentSnapshot` — sealed-type wrappers per ADR-007.
- TaleWorlds types used: `Hero`, `Equipment`, `WeaponClass`, `EquipmentIndex`, `Formation`, `Mission`, `MissionMode`, `OrderOfBattleVM`, `OrderOfBattleHeroItemVM`, `OrderOfBattleFormationItemVM`, `DeploymentFormationClass`, `Agent`, `CharacterObject`, `MissionGauntletOrderOfBattleUIHandler`, `GauntletLayer`, `MissionScreen`, `MBBindingList<T>`, `SelectorVM<OrderOfBattleFormationClassSelectorItemVM>`, `OrderOfBattleFormationClassSelectorItemVM`, `MultiSelectionInquiryData`, `TextInquiryData`.

## Tests

182 tests across 15 files in `TAOM.Tests/Features/CompanionTactics/`, plus `TAOM.Tests/Adapters/HeroCombatAdapterTests.cs` (2 tests; the equipment overload Auto-Assign uses). The counts are from a TRX run of the folder:

- `Roles/CompanionRoleServiceTests.cs` — 25 tests; one per role + edge cases (no equipment; mounted+ranged → HorseArcher; mounted+melee → Cavalry; cache hit/miss; null hero / null equipment).
- `BattleActionBar/FormationCompositionAnalyzerTests.cs` — 10 tests; HasRanged / HasPolearm / HasShield / HasCavalry positive + negative; ratio thresholds.
- `BattleActionBar/BattleActionBarServiceTests.cs` — 10 tests; composition→buttons mapping; `EnableVolleyFire = false` removes Volley button; feature-disabled returns empty.
- `BattleActionBar/TroopStanceManagerTests.cs`: 9 tests; per-formationIndex isolation; ClearAllStances; SetStance toggle behavior.
- `FormationPresets/FormationPresetServiceTests.cs`: 14 tests; save, get, delete, the name-in-use, empty-name and limit refusals, an update not counting against the limit, `OnGameLoaded` (replace and null list) and `GetPresetsForSaving` returning a copy.
- `FormationPresets/HeroAutoAssignerTests.cs`: 19 tests; role scoring per class plus the `PlanCaptains` cells (empty and null inputs, matching class, tie-break, unknown role, Unset class, global-over-local choice, one use per hero and slot, null hero, the two 50-point mixed-slot fits).
- `FormationPresets/OOBButtonsVMTests.cs`: 22 tests; the Assign Heroes command and the Presets menu: delegation, the no-screen message, each result's text, the button texts, the not-general message with no menu, vanilla's Ok and Cancel, Load (counts, the skipped line), Delete, and Save (capture then save with the prompt texts, the counts, braces removed from the name, an empty name, each refusal). Needs the game assemblies (`RequiresGame`).
- `FormationPresets/FormationPresetLayoutTests.cs`: 46 tests; every branch of the pure mapper: Capture (unset class, captain, troops, main hero, split formations), `CountOf`, `ResolveClass` (offered, the siege mapping for 3, 4 and 6, unmappable, 0 and -1), `PlanClasses`, `PlanHeroes` (missing hero, missing formation, captain and troop split), `RunClassPasses` (one pass, a later pass, a backwards chain, none succeed, some stuck) and `PlanLoad` (a blocked or unmappable class step takes no heroes; an applied or already matching one does).
- `FormationPresets/OOBPresetApplierTests.cs`: 3 tests; the null-VM, null-preset and not-general early returns apply nothing.
- `FormationPresets/OOBPresetApplierBindingTests.cs`: 7 tests; pins the vanilla members the applier reads and calls (plus `HasFormation`, which Auto-Assign reads), the `DeploymentFormationClass` numbers, that `OnFormationAcceptCaptain` clears the old assignment before assigning, and that `InitializeFormationCallbacks` wires the selection and both accept callbacks.
- `FormationPresets/OOBCaptainAutoAssignerTests.cs`: 2 tests; the null-VM and not-general early returns never reach the planner.
- `CompanionTacticsWiringTests.cs`: 1 test; the overlay's DI graph (with `IOOBCaptainAutoAssigner` and `IOOBPresetApplier`) resolves.
- `CompanionTacticsSettingsProviderTests.cs`: 3 tests; the documented defaults without MCM, every fallback equal to the MCM compiled default, and reads going through the cached settings so live MCM edits apply.
- `FormationPresets/HoNFormationPresetSerializationTests.cs` — 5 tests; every `[SaveableField]` must be a save-serializable type (the DateTime save-corruption regression guard, allowlist fails closed on unknown types); every container field's exact closed type is allowlisted; ids unique; retired id 3 not reused; definer registers only the mod-specific container (no duplicate-of-engine registrations).
- `SharedMovementOrderPostfixTests.cs` — 6 tests; shared `Formation.SetMovementOrder` postfix dispatch (SmartCavalry + CancelStanceOnMove) ordering/guards.

## How to add a new combat role

1. Add the value to `CombatRole.cs` enum.
2. Update `CompanionRoleService.ClassifyWeapon` to map a `WeaponClass` (or combination) to the new role.
3. Add a case to `GetRoleShortText`, `GetRoleHint`, `GetRoleColor` for the new value. **All four are required** — `GetRoleColor` will fall through to `uint.MaxValue` (white) if missed, which makes the role invisible in role badges. Codex review #35 caught this for `OneHanded` and `Slinger`.
4. Add a case to `IsRangedRole` if it's a ranged variant.
5. Add a scoring entry to `HeroAutoAssigner.ScoreRoleForFormation` for every formation class.
6. Add a unit test in `CompanionRoleServiceTests` covering the new role's classification path.
7. The signature packing in `ComputeSignature` uses 6 bits per slot — supports up to 64 weapon classes; no change needed unless TaleWorlds adds enum values.

## Performance

- `OOBOverlayService` caches `FieldInfo` once via `EnsureInitialized()`; no reflection in subsequent ticks.
- `RoleTooltipDecorator` caches `PropertyInfo` / `FieldInfo` in readonly fields at construction; no reflection in postfix bodies.
- `CompanionRoleService._cache` keyed by Hero StringId + 64-bit equipment signature; cache miss recomputes role and updates entry. **Note: cache is never explicitly cleared on hero death/removal** — see "Known limitations" below.
- `FormationAdapter` polearm/shield count scan is TTL-cached at 500ms (per-formation key). The action bar refreshes at most twice per second.
- `BattleActionBarMissionView.OnMissionScreenTick` uses a 0.5s accumulator gate so the formation-change check + button rebuild runs at most twice per second; per-frame work is just a 9-key hotkey poll.

## Known limitations

- **FormationPresets ships off by default (`EnableFormationPresets = false`) until Load has been checked in game.** Save captures the formation types, captains and hero troops, and Load applies them through vanilla's own flows (see "Presets (Save and Load)"). The toggle was flipped to off after a save-corruption CTD (see below). Opt in via MCM "Battle Tactics/Formation Presets". The apply path drives vanilla's OOB handlers, so it needs an in-game check on each engine bump; the binding tests pin the surface it uses.
- **Presets store no troop shares and no filters.** Class weights and the troop filters are not saved, so a Load restores formation types, captains and hero troops only (#787).
- **A swap or rotation between formations cannot apply when each is the only formation of its class.** Vanilla refuses it (`IsAdjustable`); by hand it needs a spare formation. The swap is counted as skipped (#787).
- **A dead or absent hero's assignment counts as skipped on every load.** The saved assignment stays in the preset.
- **Presets are campaign only.** The overlay does not attach in Custom Battle.
- **History — DateTime save-corruption CTD (fixed 2026-06-21).** `HoNFormationPreset` used to carry a `[SaveableField(3)] DateTime _createdAt`. `System.DateTime` is not a TaleWorlds-serializable type, so once a preset was persisted, **every** campaign save crashed: the engine left a null serialized buffer that NRE'd in `GameData.Write` on the async save thread → `AggregateException` CTD (crash bundle `taom_crash_20260621_200427_8754f009`). The field was vestigial; it was removed (id 3 retired) and pinned by `HoNFormationPresetSerializationTests`. **Player recovery:** because the save *write* failed, no post-preset save file ever completed, so a player's last valid save predates the preset — loading any existing save and continuing works (self-healing, no migration). The `try/catch` in `SyncData` did **not** and **cannot** catch this class of bug: byte serialization runs later on the `AsyncFileSaveDriver` background thread, outside that block. The fix belongs in the saveable model (keep every `[SaveableField]` serializable), not in a behavior-level catch.
- Auto-Assign places captains only. It never adds hero-troops; a hero already placed as a hero-troop is still a candidate, so it can be made captain of another formation and leaves the first one. It does not use presets, and needs an in-game check on each engine bump because it drives vanilla's OOB handlers (`OrderOfBattleHeroItemVM.OnHeroSelection`, `OrderOfBattleFormationItemVM.ExecuteAcceptCaptain`).
- **The OOB role badge still reads campaign gear.** `RoleTooltipDecorator` classifies from `Hero.BattleEquipment`, so in a siege assault it can show `[CAV]` for a companion on foot whom Auto-Assign places on a foot formation. Outside sieges the two agree.
- **Stances are display-only.** Pressing 1–9 in the action bar updates the stance dict and highlights the button, but the formation behavior is unchanged. The original developer's mod was the same — TAOM Phase 1 ports verbatim. Real stance enforcement (firing-order changes, tightened spacing, brace-pose triggers) requires APIs not exposed by the engine and is deferred to a follow-up feature.
- **`CompanionRoleService._cache` does not evict dead heroes.** Cache is keyed by Hero.StringId. When a hero dies or is removed mid-campaign, the cache entry leaks for the rest of the session. The leak is bounded (one entry per Hero ever inspected) and small — a pathological 1000-hero campaign leaks ~50KB. A follow-up could subscribe to `OnHeroKilled` to evict, but it's not blocking.
- **Hot-path role detection uses `Agent.SpawnEquipment`, not current battle equipment.** `FormationAdapter.EnsurePolearmShieldCounts` reads each agent's spawn-time equipment to compute polearm + shield counts. If a hero swaps weapons mid-battle, the action bar composition does not update until next mission. Tooltip role detection (campaign-time) uses `Hero.BattleEquipment` and is current.
- **Integration was previously in a `// TEMP-SMARTCAVALRY-EXCLUDE` state** during the 2026-05-06 parallel-port session. **Restored in commit `0cc457f` (2026-05-07).** The feature is now fully wired (`Main/SubModule.cs` + `Main/IoC.cs`) and built into the mod. See `docs/reviews/rca-companiontactics-2026-05-06.md` for the historical RCA on the build-watcher cascade that caused the temporary exclusion.

## SaveableType identifier

- BaseId: `726900601` (matches the original developer mod for save-import compat).
- Class id: `101` (`HoNFormationPreset`).
- `HoNFormationPreset` `[SaveableField]` ids: `1` (`_id`), `2` (`_name`), `4` (`_heroFormationAssignments`), `5` (`_captainHeroIds`), `6` (`_formationClasses`). **Id `3` is retired** (was an unserializable `DateTime _createdAt` that crashed every save — see "Known limitations"). The gap is deliberate; do not reuse id 3 for a non-equivalent field. **Every `[SaveableField]` must be a basic/registered serializable type** — pinned by `HoNFormationPresetSerializationTests`.
- Container types registered by the TAOM definer: `List<HoNFormationPreset>` only (the SyncData payload). The member containers `Dictionary<string,int>`, `Dictionary<int,int>`, `List<string>` are NOT re-registered — the engine's `SaveableBasicTypeDefiner` already provides them, and re-registering hits `Debug.FailedAssert("duplicate definition")` in `SaveableTypeDefiner.ConstructContainerDefinition` (verified via ilspycmd on the installed DLL, 2026-06-21).
- SyncData key: `"TAOM_FormationPresets"`.
- What the fields hold: `_formationClasses` maps `Formation.Index` (0 to 7) to a `DeploymentFormationClass` value (1 to 6) or -1 for unset; `_heroFormationAssignments` maps a hero `StringId` to a formation index; `_captainHeroIds` is a subset of its keys. Presets saved by the old name-only build have empty containers and apply nothing. HoN stored the same values (yotthani's `FormationPresetManager.cs:55-56` writes `(int)GetOrderOfBattleClass()`, and -1 for unset).
- Failure modes: `FormationPresetCampaignBehavior.SyncData` wraps the call in try/catch, which guards the **LOAD/ref-population** path — on a deserialization error it logs a warning and resets `_savedPresets` to empty rather than crashing the load. It does **not** guard the **SAVE byte-write**, which the engine performs later on the `AsyncFileSaveDriver` background thread; an unserializable field there crashes regardless of this catch (the DateTime bug). Keep the model's fields serializable.

## Reviews

- `/deep-review CompanionTactics` (5 parallel agents): 2 confirmed bugs found and fixed (`GetRoleColor` missing OneHanded + Slinger cases; `BattleActionBarDebug` setting was unused). 1 false positive disposed of (`MultiSelectionInquiryData` parameter order — call site uses named args). See `docs/reviews/rca-companiontactics-2026-05-06.md`.
- Codex adversarial prompt is staged at `docs/reviews/codex-prompt-companiontactics-2026-05-06.md` for manual dispatch via `/codex:adversarial-review --background`. The autonomous codex:rescue dispatch in this session did not finalize (Codex runtime stall — not a CompanionTactics-specific failure).

## Changelog

- 2026-10-09 (#779): formation presets work. Save captures the formation types, captains and hero troops of the Order of Battle; Load applies them through vanilla's class selector and click path, with a verify after each step, and the overlay strings are localized (`taom_oob_*`). Campaign only; the toggle stays off until an in-game check.
- 2026-09-24 (plan 022): the Assign Heroes button places captains through `HeroAutoAssigner.PlanCaptains` and vanilla's accept-captain path (it was a placeholder message). Review follow-up: roles come from the agent's spawn equipment, so horse owners are placed in sieges. No issue filed yet; in-game check owed.
- 2026-09-13 (#595): `TroopStanceManager` takes a lock. Patch35 clears a stance from `Formation.SetMovementOrder`,
  which the engine calls on its asynchronous agent tick for the PLAYER's team whenever its formations are
  AI-controlled (`Team.Tick`'s retreat branch, `Formation.Tick`'s substitute orders), so the existing team filter
  was not a thread filter; the action bar reads and writes on the main thread.
- 2026-06-21 — Fixed the Formation Preset save-corruption CTD: removed the unserializable `[SaveableField(3)] DateTime _createdAt` (id 3 retired), gated `EnableFormationPresets` off by default as WIP, and added `HoNFormationPresetSerializationTests` as a regression guard (#292).
- 2026-05-25 — Wired `SetInputRestrictions()` on the OOB overlay and battle-action-bar `GauntletLayer`s so their buttons register with the MissionScreen input dispatcher (mouse clicks were silently dropped); fixed the latent twin in `BattleActionBarMissionView` (#225).
- 2026-05-13 — Surfaced a player-facing `InformationManager` message on `SyncData` failure (was internal-log only) and added an explicit `Reset()` to `IFormationPresetService` instead of overloading the load path (#139).
- 2026-05-13 — Filtered `Patch35_Formation_SetMovementOrder` to the player team only, fixing a cross-thread `Dictionary` race where async AI-tick movement orders mutated the stance dict (#149).

## GitHub Issue

- #779 (open): formation presets, Save and Load.
- #117: superseded by #779.
- #787: follow-up for troop shares and filters, and for swaps between single-class formations.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
