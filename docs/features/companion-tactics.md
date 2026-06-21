# CompanionTactics

## Overview

Three independently-toggleable battle-tactics features bundled in one TAOM module:

1. **CompanionRoles** — equipment-based combat-role detector (11 roles); appends a role badge to companion tooltips on the party screen and OOB hero items.
2. **FormationPresets** — saveable named OOB hero-to-formation assignments; injects Save / Load / Auto-Assign buttons into the Order of Battle screen.
3. **BattleActionBar** — context-sensitive on-screen action bar that appears in field battles. 1–9 hotkeys toggle stance buttons (Hold Fire, Brace, Shield Wall, etc.). **Stances are display-only — they record state but do NOT change formation behavior** (the original developer's mod was UI-only here; the engine doesn't expose the firing-order / tighten-spacing APIs the original referenced).

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

`OrderOfBattleHeroItemVM.GetCaptainTooltip()` is **private** — not patchable via `[HarmonyPatch]` attribute binding. Manual `AccessTools.Method` wiring in `SubModule.cs` is required.

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
| `EnableFormationPresets` | **false** | Save/load OOB hero-to-formation assignments per campaign. **Off by default — WIP (loading a preset is not yet wired); opt in to try it.** |
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
| `Main/Features/CompanionTactics/FormationPresets/Models/HoNFormationPreset.cs` | `[SaveableField]` POCO; 5 fields (ids 1,2,4,5,6 — id 3 retired, was unserializable `DateTime`) |
| `Main/Features/CompanionTactics/FormationPresets/Models/FormationPresetSaveableTypeDefiner.cs` | BaseId 726900601, class 101 |
| `Main/Features/CompanionTactics/FormationPresets/OOBOverlayService.cs` | Cached-FieldInfo reflection on `_dataSource`, `_isActive`; `GauntletLayer` lifecycle |
| `Main/Features/CompanionTactics/FormationPresets/Hooks/FormationPresetCampaignBehavior.cs` | `SyncData` with try/catch — guards the LOAD/ref path only (NOT the off-thread save write); degrades to empty on BaseId collision |
| `Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_Mission_OnTick.cs` | **HOT PATH** — toggle check + lazy-cached service call, zero allocations |
| `Main/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs` | MissionView; field-battle-only `GauntletLayer` attach + 0.5s refresh + 1–9 hotkey input |
| `Main/Features/CompanionTactics/BattleActionBar/Hooks/Patch35_Formation_SetMovementOrder.cs` | Implements `CancelStanceOnMove`. Belongs to shared `Patch_MissionTime_SetMovementOrder` category (applied once from `OnMissionBehaviorInitialize` because `MovementOrder.cctor` reads `Mission.Current.CurrentTime`). |
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
- TaleWorlds types used: `Hero`, `Equipment`, `WeaponClass`, `EquipmentIndex`, `Formation`, `Mission`, `MissionMode`, `OrderOfBattleVM`, `OrderOfBattleHeroItemVM`, `MissionGauntletOrderOfBattleUIHandler`, `GauntletLayer`, `MissionScreen`, `MBBindingList<T>`, `MultiSelectionInquiryData`, `TextInquiryData`.

## Tests

84 tests across 8 files in `TAOM.Tests/Features/CompanionTactics/`:

- `Roles/CompanionRoleServiceTests.cs` — 25 tests; one per role + edge cases (no equipment; mounted+ranged → HorseArcher; mounted+melee → Cavalry; cache hit/miss; null hero / null equipment).
- `BattleActionBar/FormationCompositionAnalyzerTests.cs` — 10 tests; HasRanged / HasPolearm / HasShield / HasCavalry positive + negative; ratio thresholds.
- `BattleActionBar/BattleActionBarServiceTests.cs` — 10 tests; composition→buttons mapping; `EnableVolleyFire = false` removes Volley button; feature-disabled returns empty.
- `BattleActionBar/TroopStanceManagerTests.cs` — 8 tests; per-formationIndex isolation; ClearAllStances; SetStance toggle behavior.
- `FormationPresets/FormationPresetServiceTests.cs` — 14 tests; SaveResult.LimitReached path; missing-hero pruning on load; OnGameLoaded; OnMissionEnd state reset; SaveableType round-trip.
- `FormationPresets/HeroAutoAssignerTests.cs` — 7 tests; each role → expected formation slot scoring; ScoreHeroForFormation null safety.
- `FormationPresets/HoNFormationPresetSerializationTests.cs` — 5 tests; every `[SaveableField]` must be a save-serializable type (the DateTime save-corruption regression guard, allowlist fails closed on unknown types); every container field's exact closed type is allowlisted; ids unique; retired id 3 not reused; definer registers only the mod-specific container (no duplicate-of-engine registrations).
- `SharedMovementOrderPostfixTests.cs` — 5 tests; shared `Formation.SetMovementOrder` postfix dispatch (SmartCavalry + CancelStanceOnMove) ordering/guards.

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

- **FormationPresets is WIP and ships off by default (`EnableFormationPresets = false`).** Saving a preset works, but loading one back is not yet wired (the Load path is a stub). The toggle was flipped to off after a save-corruption CTD (see below); opt in via MCM "Battle Tactics/Formation Presets" to try the save side.
- **History — DateTime save-corruption CTD (fixed 2026-06-21).** `HoNFormationPreset` used to carry a `[SaveableField(3)] DateTime _createdAt`. `System.DateTime` is not a TaleWorlds-serializable type, so once a preset was persisted, **every** campaign save crashed: the engine left a null serialized buffer that NRE'd in `GameData.Write` on the async save thread → `AggregateException` CTD (crash bundle `taom_crash_20260621_200427_8754f009`). The field was vestigial; it was removed (id 3 retired) and pinned by `HoNFormationPresetSerializationTests`. **Player recovery:** because the save *write* failed, no post-preset save file ever completed, so a player's last valid save predates the preset — loading any existing save and continuing works (self-healing, no migration). The `try/catch` in `SyncData` did **not** and **cannot** catch this class of bug: byte serialization runs later on the `AsyncFileSaveDriver` background thread, outside that block. The fix belongs in the saveable model (keep every `[SaveableField]` serializable), not in a behavior-level catch.
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
- Failure modes: `FormationPresetCampaignBehavior.SyncData` wraps the call in try/catch, which guards the **LOAD/ref-population** path — on a deserialization error it logs a warning and resets `_savedPresets` to empty rather than crashing the load. It does **not** guard the **SAVE byte-write**, which the engine performs later on the `AsyncFileSaveDriver` background thread; an unserializable field there crashes regardless of this catch (the DateTime bug). Keep the model's fields serializable.

## Reviews

- `/deep-review CompanionTactics` (5 parallel agents): 2 confirmed bugs found and fixed (`GetRoleColor` missing OneHanded + Slinger cases; `BattleActionBarDebug` setting was unused). 1 false positive disposed of (`MultiSelectionInquiryData` parameter order — call site uses named args). See `docs/reviews/rca-companiontactics-2026-05-06.md`.
- Codex adversarial prompt is staged at `docs/reviews/codex-prompt-companiontactics-2026-05-06.md` for manual dispatch via `/codex:adversarial-review --background`. The autonomous codex:rescue dispatch in this session did not finalize (Codex runtime stall — not a CompanionTactics-specific failure).

## GitHub Issue

Not yet created. To create retroactively per CLAUDE.md "GitHub Issue & Knowledge Base Requirements":

```
gh issue create --label feature --title "feat(companion-tactics): port CompanionTactics three sub-features" --body "(see docs/features/companion-tactics.md)"
```

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
