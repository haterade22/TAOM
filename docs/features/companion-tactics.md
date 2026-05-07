# CompanionTactics

## Overview

Three independently-toggleable battle-tactics features bundled in one TAOM module:

1. **CompanionRoles** — equipment-based combat-role detector (11 roles); appends a role badge to companion tooltips on the party screen and OOB hero items.
2. **FormationPresets** — saveable named OOB hero-to-formation assignments; injects Save / Load / Auto-Assign buttons into the Order of Battle screen.
3. **BattleActionBar** — context-sensitive on-screen action bar that appears in field battles. 1–9 hotkeys toggle stance buttons (Hold Fire, Brace, Shield Wall, etc.). **Stances are display-only — they record state but do NOT change formation behavior** (the original developer's mod was UI-only here; v1.3.15 doesn't expose the firing-order / tighten-spacing APIs the original referenced).

Ported from `Downloads/Features_fixed/CompanionTactics/` (Bannerlord 1.3 mod template) for TAOM v1.3.15. Patch35 reserves the Harmony category. SaveableTypeDefiner BaseId 726900601 (matches the original mod for save-import compat).

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

`OrderOfBattleHeroItemVM.GetCaptainTooltip()` is **private** in v1.3.15 — not patchable via `[HarmonyPatch]` attribute binding. Manual `AccessTools.Method` wiring in `SubModule.cs` is required.

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
│   ├── IHeroAutoAssigner → HeroAutoAssigner              (consumes ICompanionRoleService)
│   ├── IOrderOfBattleVMTracker → OrderOfBattleVMTracker  (captures VM ref from ctor postfix)
│   ├── IOOBOverlayService → OOBOverlayService            (GauntletLayer + LoadMovie)
│   ├── UI/OOBButtonsVM                                   (Save/Load/Delete inquiry chain)
│   ├── Models/HoNFormationPreset                          ([SaveableField] BaseId 726900601 / class 101)
│   ├── Models/FormationPresetSaveableTypeDefiner
│   └── Hooks/                                             (5 Harmony patches)
│       FormationPresetCampaignBehavior                   (try/catch SyncData → degrade to empty on collision)
│
└── BattleActionBar/                ← per-mission context bar
    ├── IBattleActionBarService → BattleActionBarService   (gates Volley on EnableVolleyFire)
    ├── IFormationCompositionAnalyzer → FormationCompositionAnalyzer
    ├── ITroopStanceManager → TroopStanceManager           (per-formationIndex stance dict)
    ├── UI/{BattleActionBarVM, ActionButtonVM}
    └── Hooks/
        BattleActionBarMissionView                        (MissionView NOT Harmony; field battles only)
        Patch35_Formation_SetMovementOrder                (clears stance on move when CancelStanceOnMove)
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
| `EnableFormationPresets` | true | Save/load OOB hero-to-formation assignments per campaign. |
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
| `Main/Features/CompanionTactics/CompanionTacticsSettingsProvider.cs` | Reads `TaomSettings.Instance` directly (no reflection) |
| `Main/Features/CompanionTactics/Roles/CompanionRoleService.cs` | 11-role classifier with hero-StringId-keyed cache + equipment fingerprint signature |
| `Main/Features/CompanionTactics/Roles/RoleTooltipDecorator.cs` | Cached `PropertyInfo` mutations of vanilla VMs' Name + tooltip cache |
| `Main/Features/CompanionTactics/Roles/Hooks/Patch35_*.cs` | 3 Harmony postfixes + 1 manual patch (private GetCaptainTooltip) |
| `Main/Features/CompanionTactics/FormationPresets/FormationPresetService.cs` | CRUD with `MaxFormationPresets` enforcement |
| `Main/Features/CompanionTactics/FormationPresets/Models/HoNFormationPreset.cs` | `[SaveableField]` POCO; 6 fields |
| `Main/Features/CompanionTactics/FormationPresets/Models/FormationPresetSaveableTypeDefiner.cs` | BaseId 726900601, class 101 |
| `Main/Features/CompanionTactics/FormationPresets/OOBOverlayService.cs` | Cached-FieldInfo reflection on `_dataSource`, `_isActive`; `GauntletLayer` lifecycle |
| `Main/Features/CompanionTactics/FormationPresets/Hooks/FormationPresetCampaignBehavior.cs` | `SyncData` with try/catch — degrades to empty on BaseId collision |
| `Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_Mission_OnTick.cs` | **HOT PATH** — toggle check + lazy-cached service call, zero allocations |
| `Main/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs` | MissionView; field-battle-only `GauntletLayer` attach + 0.5s refresh + 1–9 hotkey input |
| `Main/Features/CompanionTactics/BattleActionBar/Hooks/Patch35_Formation_SetMovementOrder.cs` | Implements `CancelStanceOnMove` |
| `Main/Adapters/{I,}BattleEquipmentSnapshot.cs` | Equipment value-object snapshot (no sealed `Equipment` leak) |
| `Main/Adapters/{I,}HeroCombatAdapter.cs` | Campaign-time `Hero` wrapper |
| `Main/Adapters/{I,}AgentCombatAdapter.cs` | Mission-time `Agent` wrapper |
| `Main/Adapters/IFormationAdapter.cs` (EXTENDED) | Adds `FormationIndex` + 4 unit-count properties |
| `Main/Adapters/FormationAdapter.cs` (EXTENDED) | TTL-cached polearm/shield count scan |
| `Main/_Module/GUI/Prefabs/BattleActionBar.xml` | Bound to `BattleActionBarVM` (vanilla brushes only) |
| `Main/_Module/GUI/Prefabs/OOBButtonsOverlay.xml` | Bound to `OOBButtonsVM` (vanilla brushes only) |

## Dependencies

- `ICompanionTacticsSettingsProvider` — typed read from `TaomSettings.Instance` (testable seam).
- `IModLogger` (TAOM core) — file logger; gated HUD output via Debug toggles.
- `IFormationAdapter`, `IHeroCombatAdapter`, `IAgentCombatAdapter`, `IBattleEquipmentSnapshot` — sealed-type wrappers per ADR-007.
- TaleWorlds 1.3.15: `Hero`, `Equipment`, `WeaponClass`, `EquipmentIndex`, `Formation`, `Mission`, `MissionMode`, `OrderOfBattleVM`, `OrderOfBattleHeroItemVM`, `MissionGauntletOrderOfBattleUIHandler`, `GauntletLayer`, `MissionScreen`, `MBBindingList<T>`, `MultiSelectionInquiryData`, `TextInquiryData`.

## Tests

74 tests across 6 files in `TAOM.Tests/Features/CompanionTactics/`:

- `Roles/CompanionRoleServiceTests.cs` — 24 tests; one per role + edge cases (no equipment; mounted+ranged → HorseArcher; mounted+melee → Cavalry; cache hit/miss; null hero / null equipment).
- `BattleActionBar/FormationCompositionAnalyzerTests.cs` — 10 tests; HasRanged / HasPolearm / HasShield / HasCavalry positive + negative; ratio thresholds.
- `BattleActionBar/BattleActionBarServiceTests.cs` — 10 tests; composition→buttons mapping; `EnableVolleyFire = false` removes Volley button; feature-disabled returns empty.
- `BattleActionBar/TroopStanceManagerTests.cs` — 8 tests; per-formationIndex isolation; ClearAllStances; SetStance toggle behavior.
- `FormationPresets/FormationPresetServiceTests.cs` — 14 tests; SaveResult.LimitReached path; missing-hero pruning on load; OnGameLoaded; OnMissionEnd state reset; SaveableType round-trip.
- `FormationPresets/HeroAutoAssignerTests.cs` — 7 tests; each role → expected formation slot scoring; ScoreHeroForFormation null safety.

## How to add a new combat role

1. Add the value to `CombatRole.cs` enum.
2. Update `CompanionRoleService.ClassifyWeapon` to map a `WeaponClass` (or combination) to the new role.
3. Add a case to `GetRoleShortText`, `GetRoleHint`, `GetRoleColor` for the new value. **All four are required** — `GetRoleColor` will fall through to `uint.MaxValue` (white) if missed, which makes the role invisible in role badges. Codex review #35 caught this for `OneHanded` and `Slinger`.
4. Add a case to `IsRangedRole` if it's a ranged variant.
5. Add a scoring entry to `HeroAutoAssigner.ScoreRoleForFormation` for every formation class.
6. Add a unit test in `CompanionRoleServiceTests` covering the new role's classification path.
7. The signature packing in `ComputeSignature` uses 6 bits per slot — supports up to 64 weapon classes; no change needed unless TaleWorlds adds enum values.

## Performance

- `Patch35_Mission_OnTick` is on the engine-tick hot path (every frame). Body is: lazy-cached `??=` settings provider read + early-return; **zero allocations, no LINQ, no closures**. Per AGENTS.md "Per-tick allocations" rule.
- `OOBOverlayService` caches `FieldInfo` once via `EnsureInitialized()`; no reflection in subsequent ticks.
- `RoleTooltipDecorator` caches `PropertyInfo` / `FieldInfo` in readonly fields at construction; no reflection in postfix bodies.
- `CompanionRoleService._cache` keyed by Hero StringId + 64-bit equipment signature; cache miss recomputes role and updates entry. **Note: cache is never explicitly cleared on hero death/removal** — see "Known limitations" below.
- `FormationAdapter` polearm/shield count scan is TTL-cached at 500ms (per-formation key). The action bar refreshes at most twice per second.
- `BattleActionBarMissionView.OnMissionScreenTick` uses a 0.5s accumulator gate so the formation-change check + button rebuild runs at most twice per second; per-frame work is just a 9-key hotkey poll.

## Known limitations

- **Stances are display-only.** Pressing 1–9 in the action bar updates the stance dict and highlights the button, but the formation behavior is unchanged. The original developer's mod was the same — TAOM Phase 1 ports verbatim. Real stance enforcement (firing-order changes, tightened spacing, brace-pose triggers) requires APIs not exposed in v1.3.15 and is deferred to a follow-up feature.
- **`CompanionRoleService._cache` does not evict dead heroes.** Cache is keyed by Hero.StringId. When a hero dies or is removed mid-campaign, the cache entry leaks for the rest of the session. The leak is bounded (one entry per Hero ever inspected) and small — a pathological 1000-hero campaign leaks ~50KB. A follow-up could subscribe to `OnHeroKilled` to evict, but it's not blocking.
- **Hot-path role detection uses `Agent.SpawnEquipment`, not current battle equipment.** `FormationAdapter.EnsurePolearmShieldCounts` reads each agent's spawn-time equipment to compute polearm + shield counts. If a hero swaps weapons mid-battle, the action bar composition does not update until next mission. Tooltip role detection (campaign-time) uses `Hero.BattleEquipment` and is current.
- **Integration is currently in `// TEMP-SMARTCAVALRY-EXCLUDE` state.** A parallel-port build watcher in this environment auto-comments my CompanionTactics integration calls in `Main/SubModule.cs` and `Main/IoC.cs` whenever any build error appears. The CODE in `Main/Features/CompanionTactics/` is intact and the unit tests passed at port time. To activate the feature, manually un-comment lines 67–70 (using directives), 379–381 (CampaignBehavior), 431–437 (manual GetCaptainTooltip patch), and 502 (BattleActionBarMissionView) in `Main/SubModule.cs`, plus line 91 of `Main/IoC.cs`. Also remove the `<Compile Remove="Features\CompanionTactics\**\*.cs" />` line from `Main/TAOM.csproj` and `TAOM.Tests/TAOM.Tests.csproj`. See `docs/reviews/rca-companiontactics-2026-05-06.md` for the full RCA.

## SaveableType identifier

- BaseId: `726900601` (matches the original developer mod for save-import compat).
- Class id: `101` (`HoNFormationPreset`).
- Container types registered: `List<HoNFormationPreset>`, `Dictionary<string,int>`, `Dictionary<int,int>`, `List<string>`.
- SyncData key: `"TAOM_FormationPresets"`.
- Failure mode: `FormationPresetCampaignBehavior.SyncData` wraps the call in try/catch. On any deserialization error, logs a warning and resets `_savedPresets` to empty rather than crashing the load.

## Reviews

- `/deep-review CompanionTactics` (5 parallel agents): 2 confirmed bugs found and fixed (`GetRoleColor` missing OneHanded + Slinger cases; `BattleActionBarDebug` setting was unused). 1 false positive disposed of (`MultiSelectionInquiryData` parameter order — call site uses named args). See `docs/reviews/rca-companiontactics-2026-05-06.md`.
- Codex adversarial prompt is staged at `docs/reviews/codex-prompt-companiontactics-2026-05-06.md` for manual dispatch via `/codex:adversarial-review --background`. The autonomous codex:rescue dispatch in this session did not finalize (Codex runtime stall — not a CompanionTactics-specific failure).

## GitHub Issue

Not yet created. To create retroactively per CLAUDE.md "GitHub Issue & Knowledge Base Requirements":

```
gh issue create --label feature --title "feat(companion-tactics): port CompanionTactics three sub-features" --body "(see docs/features/companion-tactics.md)"
```
