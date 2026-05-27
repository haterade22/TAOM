# Equipment Presets

## Overview

Adds a "Presets" overlay button to the inventory screen that lets the player save / load / update / delete per-hero equipment presets. Presets persist across the campaign save and preserve the full `EquipmentElement` (item + `ItemModifier`) so durability and quality prefixes (e.g. "Sharp") survive the round-trip.

Ported from external developer drop `Downloads/Features_fixed/EquipPresets/` into TAOM's adapter pattern + IoC + MCM conventions.

## Why This Exists

Vanilla Bannerlord lets you swap individual items but offers no per-hero preset system. Companions in TAOM often have several legitimate loadouts (siege, melee, scout, civilian); without presets, the player rebuilds each loadout by hand every time they swap modes.

- **Vanilla behavior:** No equipment-preset concept; each slot is mutated one item at a time.
- **TAOM requirement:** Per-hero named presets, `ItemModifier` preserved end-to-end, persisted across save/load.
- **Without this feature:** Players manually re-equip 11 slots × every mode change × every hero. Quality prefixes get re-rolled or lost in the manual workflow.

## Architecture

### Design Challenge

1. **Modifier preservation.** Bannerlord's `ItemRoster` and `Equipment` APIs both ship two parallel overloads: a bare `(ItemObject, int)` form that drops `ItemModifier`, and a richer `(EquipmentElement, int)` / indexer-setter form that preserves it. Codex review #34 (SiegeDismount, 2026-05-06) anchored "always use the modifier-preserving overload" as a non-negotiable. EquipPresets is the canonical use case — every slot stores an `EquipmentElement` and must round-trip the modifier.

2. **Adapter boundary (ADR-007).** Services never see sealed TaleWorlds types. The service layer only sees `string`-based StringIds (for `ItemObject` and `ItemModifier`); the full `EquipmentElement` lives inside the `EquipmentSlotAdapter`.

3. **Save format compatibility.** Mod-removed items (player removed an armory mod between save sessions) must be reported gracefully rather than crashing. `LoadPresetWithReport` returns a structured per-slot report with separate `MissingItems` / `MissingModifiers` lists.

### Solution Approach

Three Harmony patches under `Patch33_EquipPresets` plus one campaign behavior + GauntletLayer overlay.

- **`Patch33_GauntletInventoryScreen`** — Postfix on `OnInitialize` creates a `GauntletLayer` (z-order **1000**, above vanilla's 15) that hosts `PresetsOverlay.xml` + `PresetsOverlayVM`. Prefix on `OnFinalize` removes the layer and clears static state.
- **`Patch33_SPInventoryVMRefresh`** — Postfix on `SPInventoryVM.RefreshValues` captures the live VM into `IInventoryScreenAdapter` so the VM can read active-hero / equipment-mode and refresh after mutation. IoC-resolved adapter cached statically (per `harmony-patches.md` "Reflection in hot paths" rule).
- **`EquipmentPresetCampaignBehavior`** — `SyncData("EquipPresets_HeroPresets")` round-trips the `Dictionary<heroStringId, List<HoNEquipmentPreset>>`. `OnGameLoaded` prunes orphaned hero entries (heroes that died/disappeared between sessions).

### Component Diagram

```
TaomSettings (3 MCM properties: Enable, MaxPresets, Debug)
       |
       v
EquipPresetsSettingsProvider (validates MaxPresets ∈ [1,20])
       |
       v
EquipmentPresetService  ← (Reuse.Singleton)
   ├── IEquipmentSlotAdapter        — capture/apply slots, preserves ItemModifier
   ├── IItemModifierLookupAdapter   — validates modifier StringId (lookup-with-fallback rule)
   └── (consumed by)
         ├── EquipmentPresetCampaignBehavior  — SyncData + OnGameLoaded pruning
         ├── PresetsOverlayVM                 — Save/Load/Update/Delete dialogs
         └── (Patch33 hooks delegate to the VM and adapter)

Saveable types (registered via PresetSaveableTypeDefiner, BaseId 726900501):
   HoNEquipmentPreset  (id 102)   — Name, Items, CivilianItems, IncludesMount, IncludesCivilianEquipment
   HoNPresetItemReference (id 101) — SlotIndex, ItemStringId, ItemModifierStringId
```

## Configuration

### MCM Settings (`Main/Features/TaomSettings.cs`, `GroupOrder = 33`, group "Inventory/Equipment Presets")

| Setting | Type | Range / Default | Effect |
|---|---|---|---|
| `EnableEquipmentPresets` | bool | default `true` | Master toggle. Off → overlay button not added; existing presets remain in the save (re-enabling restores them). |
| `MaxPresetsPerCharacter` | int | `[1, 20]`, default `10` | Per-hero cap. Save fails with `MaxReached` once at limit. Range-clamped on load (Config Providers MUST Validate rule). |
| `EquipPresetsDebug` | bool | default `false` | Gates `LogDebug` output to file log. |

### SaveableType allocation

| Type | Local id | Full save id |
|---|---|---|
| `HoNPresetItemReference` | 101 | 726900602 |
| `HoNEquipmentPreset` | 102 | 726900603 |

`BaseId 726900501` was verified unique across TAOM at port time (no other `SaveableTypeDefiner` exists in the project — `CareerSystem` deliberately avoided one by using primitive-only SyncData). Future TAOM features choosing a SaveableTypeDefiner BaseId should pick a fresh range.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/EquipPresets/EquipmentPresetService.cs` | Core CRUD + LoadPresetWithReport + max-presets enforcement + orphan pruning |
| `Main/Features/EquipPresets/IEquipmentPresetService.cs` | Service interface (string-typed; no TaleWorlds types) |
| `Main/Features/EquipPresets/EquipPresetsSettingsProvider.cs` | MCM reader with [1,20] semantic clamp on `MaxPresetsPerCharacter` |
| `Main/Features/EquipPresets/EquipPresetsIoC.cs` | DryIoc registrations (Reuse.Singleton) |
| `Main/Features/EquipPresets/Models/HoNEquipmentPreset.cs` | `[SaveableProperty]`-decorated saveable record |
| `Main/Features/EquipPresets/Models/HoNPresetItemReference.cs` | Per-slot saveable record (slot, item id, modifier id) |
| `Main/Features/EquipPresets/Models/PresetSaveableTypeDefiner.cs` | BaseId 726900501; auto-discovered by SaveSystem |
| `Main/Features/EquipPresets/Models/PresetLoadResult.cs` | Per-load report (equipped count, missing items, missing modifiers) |
| `Main/Features/EquipPresets/Models/Outcomes.cs` | SaveOutcome / UpdateOutcome / DeleteOutcome / SlotApplyOutcome enums |
| `Main/Features/EquipPresets/Hooks/EquipmentPresetCampaignBehavior.cs` | SyncData + `OnGameLoaded` orphan pruning |
| `Main/Features/EquipPresets/Hooks/Patch33_GauntletInventoryScreen.cs` | Overlay layer creation + cleanup (z-order 1000) |
| `Main/Features/EquipPresets/Hooks/Patch33_SPInventoryVMRefresh.cs` | Active VM capture (cached IoC.Resolve) |
| `Main/Features/EquipPresets/UI/PresetsOverlayVM.cs` | Datasource for `PresetsOverlay.xml` (ButtonText + ExecuteOpenPresets + Save/Load/Update/Delete dialogs) |
| `Main/Adapters/IEquipmentSlotAdapter.cs` + `EquipmentSlotAdapter.cs` | 11-slot reader/writer, modifier-preserving via `equipment[EquipmentIndex] = new EquipmentElement(item, modifier)`. Hero lookup via `Campaign.Current.CampaignObjectManager.Find<Hero>(...)` — heroes do NOT live in `MBObjectManager` (only items/modifiers/templates do); using the wrong manager makes every `HeroExists` check fail and produces the user-visible `No active hero` save error. |
| `Main/Adapters/IItemModifierLookupAdapter.cs` + `ItemModifierLookupAdapter.cs` | Validates modifier StringId BEFORE the lookup is acted on |
| `Main/Adapters/IInventoryScreenAdapter.cs` + `InventoryScreenAdapter.cs` | Wraps active `SPInventoryVM` (active hero, equipment mode, item lock, refresh, clear) |
| `Main/_Module/GUI/Prefabs/PresetsOverlay.xml` | Vanilla-brushes-only overlay markup |

## Dependencies

- `IModLogger` (Core) — Diagnostic file log + optional HUD output
- `MBObjectManager` (TaleWorlds) — Wrapped behind `IItemModifierLookupAdapter` (items + modifiers only)
- `Campaign.CampaignObjectManager` (TaleWorlds) — Wrapped behind `IEquipmentSlotAdapter` for Hero-by-StringId lookups
- `SPInventoryVM` (TaleWorlds) — Wrapped behind `IInventoryScreenAdapter`
- `Hero.BattleEquipment` / `Hero.CivilianEquipment` (TaleWorlds) — Wrapped behind `IEquipmentSlotAdapter`

## Tests

- `TAOM.Tests/Features/EquipPresets/EquipmentPresetServiceTests.cs` — 37 tests covering CRUD, missing-item / missing-modifier paths, MaxPresets enforcement (positive + negative), civilian/battle routing, IncludesMount toggle, IncludesCivilian toggle, orphan pruning, validate-before-lookup rule, modifier round-trip, slot-clearing on empty itemId.
- `TAOM.Tests/Features/EquipPresets/HoNEquipmentPresetTests.cs` — 7 tests verifying default values, null-safety on item-ref ctor, AND `[SaveableProperty]` indexes (1..6 for the preset, 1..3 for the item-ref). Index numbers are part of the save format — these tests are the regression gate against accidental renumbering.
- `TAOM.Tests/Features/EquipPresets/PresetSaveableTypeDefinerTests.cs` — 3 tests: BaseId matches the constant, BaseId is unique across all TAOM SaveableTypeDefiners, ctor is exception-free.
- `TAOM.Tests/Features/EquipPresets/EquipPresetsSettingsProviderTests.cs` — 2 tests: range constants are coherent, default fits the [min, max] window.

## How to Add a New Setting

1. Add the property to `Main/Features/TaomSettings.cs` under the existing `Inventory/Equipment Presets` group.
2. Expose it on `IEquipPresetsSettingsProvider`.
3. Implement the read in `EquipPresetsSettingsProvider`. **Apply semantic clamping** (Config Providers MUST Validate rule): if the setting has range constraints, range-check it and fall back to a default rather than passing the raw value through.
4. Consume in the service via the existing constructor-injected provider.

## How to Extend the Overlay UI

1. Edit `Main/_Module/GUI/Prefabs/PresetsOverlay.xml`. **Use only vanilla brushes** (`ButtonBrush1`, `ButtonBrush1.Text`, etc.) — no custom sprites are registered for this feature.
2. Add the corresponding `[DataSourceProperty]` and parameterless command method to `PresetsOverlayVM`.
3. Bindings use `@PropertyName` for text and `Command.Click="MethodName"` for click handlers.

## Performance

- `Patch33_SPInventoryVMRefresh.Postfix` fires on every `RefreshValues` (often while inventory is open). The IoC-resolved `IInventoryScreenAdapter` and `IModLogger` are lazy-cached statically (`??=` pattern) per `.claude/rules/harmony-patches.md` "Reflection in hot paths" / "IoC.Resolve in Hot Paths" guidance.
- `InventoryScreenAdapter` caches the `SPInventoryVM._currentCharacter` `FieldInfo` as a static readonly field at type-init.
- Save/Load are user-triggered (rare); no per-frame allocation budget concerns there.

## Known Limitations / Design Decisions

- **`IncludesMount` UI toggle is hardcoded `true` at save time.** The saveable model exposes the flag for forward compatibility, but the current `PromptSaveName` flow always captures mount + harness. To exclude mount, a future UI revision can expose a checkbox via a `MultiSelectionInquiryData`.
- **Pre-existing item locks are not respected.** The original module reserved an `IsLocked` slot to skip mid-load mutation; in v1 we don't surface that path, since the adapter applies the slots in a single tight loop and vanilla doesn't auto-mutate equipment between consecutive `equipment[i] = …` assignments. If a future feature pre-locks items via `SPItemVM.IsLocked`, this feature will overwrite them.
- **`CosmeticItem` field is NOT preserved.** `EquipmentElement.CosmeticItem` is a public field but has no `[SaveableProperty]`, so it doesn't survive the save/load cycle even in vanilla. Out of scope for this feature.

## GitHub Issue

To be opened on session close.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
