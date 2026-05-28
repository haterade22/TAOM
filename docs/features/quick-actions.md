# QuickActions

## Overview

Replaces the inventory screen's "Sell All" button with a multi-action menu offering four choices: Sell Damaged, Sell Low Value, Unequip All, and the vanilla bulk-sell. Adds a per-save persistent toggle for the inventory search box. Ported from the external 1.2.x `TransferbuttonMenu` module with the reflection-fallback chains removed (verified unnecessary against the current public API).

## Why This Exists

- **Vanilla behavior:** the inventory's "Sell All" button bulk-sells every transferable, non-locked item with no filtering. There's no in-game way to keep cheap items, drop only damaged gear, or strip a hero's equipment in one click.
- **TAOM requirement:** Middle-earth campaigns produce massive amounts of looted gear. Players need a fast way to (a) clear damaged trophies that consume inventory weight without tying up gold, (b) dump low-denar junk while keeping food/horses, and (c) reset a hero's loadout before re-equipping from a preset.
- **Without this feature:** players manually sell items one-at-a-time, or use the all-or-nothing vanilla "Sell All" that trashes locked-quality gear they wanted to keep.

## Architecture

### Design Challenge

The original 1.2.x module used heavy reflection (8 probes for the right-pane item list, 5 probes for `SPItemVM`'s sell method) because v1.2.x had non-public field/method names that drifted across patches. **Those members are all public in the current API** (`SPInventoryVM.RightItemListVM`, `SPItemVM.ProcessSellItem`, `IsSearchAvailable`, `ExecuteSellAllItems`) — verified via `ilspycmd` against the installed `TaleWorlds.CampaignSystem.ViewModelCollection.dll`. The TAOM port uses direct property access, with the reflection chain removed entirely.

A second concern was the "shared adapter for QuickActions + EquipPresets" requirement: both features access the same `SPInventoryVM`, and duplicating reflection across two features is the bug the original module had. This port introduces `IInventoryVMAdapter` that captures the active VM via a Postfix on `SPInventoryVM`'s constructor — a single source of truth that `EquipPresets` (feature #6) will reuse.

### Solution Approach

Three Harmony patches under `Patch34_QuickActions`:

1. **`Patch34_SPInventoryVMCapture`** — Postfix on `SPInventoryVM` constructor. Captures the new VM into `InventoryVMAdapter`'s `_active` field. Each inventory open creates a new VM, so the reference naturally turns over.
2. **`Patch34_SellAllItemsMenu`** — Prefix on `SPInventoryVM.ExecuteSellAllItems`. When `EnableQuickActions = true`, opens a 4-option `MultiSelectionInquiryData` and returns `false` to skip vanilla. The "Sell All (Vanilla)" option dispatches a manual replication of vanilla bulk-sell (iterates `RightItemListVM`, calls `SPItemVM.ProcessSellItem`) — calling `__instance.ExecuteSellAllItems()` would re-enter the patch and infinite-loop.
3. **`Patch34_SPInventoryVMSearchApply`** — Postfix on `SPInventoryVM.RefreshCallbacks` (called once per inventory open from `GauntletInventoryScreen.OnActivate`). Applies `IsSearchAvailable` from the `InventorySearchCampaignBehavior` to the live VM.

The `InventorySearchCampaignBehavior` holds the per-save bool via `SyncData("TAOM_IsInventorySearchAvailable")`. It seeds from MCM on new-game / on-load and reconciles every campaign tick — sub-second sync is unnecessary because the apply happens on inventory open, not on tick.

### Component Diagram

```
SPInventoryVM (vanilla)
    |  Patch34_SellAllItemsMenu (Prefix)
    |
    v
QuickActionsService (filter + sell + unequip)
   |          |              |
   v          v              v
IInventoryVMAdapter   IPlayerEquipmentAdapter   IQuickActionsAudioPlayer
   |                        |                            |
   v                        v                            v
SPInventoryVM         Hero.MainHero / ItemRoster   SoundEvent.PlaySound2D
SPItemVM.ProcessSellItem
```

## Configuration

### MCM: TAOM → Inventory / Quick Actions

14 settings across 4 groups. All settings live in `TaomSettings.cs` and are surfaced via `IQuickActionsSettingsProvider`.

| Group | Setting | Default | Purpose |
|-------|---------|---------|---------|
| General | EnableQuickActions | true | Master toggle. Off = vanilla "Sell All" runs. |
| General | EnableInventorySearch | true | Search box visibility. Per-save persisted. |
| Sell Damaged | DamagedQualityDropdown | Moderate (-20%) | Threshold preset (Pristine/Slight/Moderate/Heavy). |
| Sell Damaged | DamagedThreshold | -0.20 | Custom threshold (when UseCustomThreshold = true). |
| Sell Damaged | UseCustomThreshold | false | Switch between dropdown and custom value. |
| Sell Damaged | SellDamagedEquipped | false | Include equipped items. |
| Sell Damaged | ExcludeDamagedHorses | true | Skip mounts. |
| Sell Low Value | LowValueThreshold | 100 | Denars cutoff (≤ sells). |
| Sell Low Value | SellLowValueEquipped | false | Include equipped items. |
| Sell Low Value | ExcludeLowValueFood | true | Skip food. |
| Sell Low Value | ExcludeLowValueHorses | true | Skip mounts. |
| Sell Low Value | ExcludeLowValueTradeGoods | false | Skip trade goods (cloth, fur, salt). |
| Misc | QuickActionsShowConfirmation | true | Confirm dialog before bulk-selling. |
| Misc | QuickActionsPlaySounds | true | Play `event:/ui/transfer` on action. |
| Misc | QuickActionsDebug | false | HUD diagnostic messages. |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/QuickActions/IQuickActionsService.cs` | Service interface |
| `Main/Features/QuickActions/QuickActionsService.cs` | Filter + dispatch + audio + refresh |
| `Main/Features/QuickActions/IQuickActionsSettingsProvider.cs` | Settings shape |
| `Main/Features/QuickActions/QuickActionsSettingsProvider.cs` | Wraps `TaomSettings.Instance` |
| `Main/Features/QuickActions/Models/QuickActionType.cs` | enum: SellDamaged/SellLowValue/UnequipAll/OriginalSellAll |
| `Main/Features/QuickActions/Models/QuickActionResult.cs` | Status + counts struct |
| `Main/Features/QuickActions/Models/DamagedQualityPreset.cs` | Dropdown ↔ threshold mapping |
| `Main/Features/QuickActions/Audio/IQuickActionsAudioPlayer.cs` | Audio wrapper interface |
| `Main/Features/QuickActions/Audio/QuickActionsAudioPlayer.cs` | `SoundEvent.PlaySound2D` impl |
| `Main/Features/QuickActions/QuickActionsIoC.cs` | DryIoc registration |
| `Main/Features/QuickActions/Hooks/Patch34_SellAllItemsMenu.cs` | Prefix on `ExecuteSellAllItems` |
| `Main/Features/QuickActions/Hooks/Patch34_SPInventoryVMCapture.cs` | Captures active VM into adapter |
| `Main/Features/QuickActions/Hooks/Patch34_SPInventoryVMSearchApply.cs` | Applies search toggle on inventory open |
| `Main/Features/QuickActions/Hooks/InventorySearchCampaignBehavior.cs` | SyncData + MCM reconcile |
| `Main/Adapters/IInventoryVMAdapter.cs` + impl | Active-VM wrapper (load-bearing for EquipPresets) |
| `Main/Adapters/IInventoryItemAdapter.cs` + impl | Per-item wrapper (preserves `EquipmentElement`) |
| `Main/Adapters/IPlayerEquipmentAdapter.cs` + impl | Extended with `TryUnequipAllPlayerSlots` |

## Dependencies

- `IInventoryVMAdapter` (Adapters) — wraps `SPInventoryVM`. Captured via constructor Postfix.
- `IInventoryItemAdapter` (Adapters) — wraps `SPItemVM`. Carries opaque `UnderlyingVm` so the sell delegate gets the full `EquipmentElement` (preserves `ItemModifier`).
- `IPlayerEquipmentAdapter` (Adapters) — extended with per-slot strip + deposit using the `(EquipmentElement, int)` modifier-preserving overload of `ItemRoster.AddToCounts`.
- `IQuickActionsAudioPlayer` (Audio) — wraps `SoundEvent.PlaySound2D("event:/ui/transfer")`.
- `IModLogger` (Core/Logging) — diagnostic + error logging.

## Tests

- `TAOM.Tests/Features/QuickActions/QuickActionsServiceTests.cs` — 34 tests covering: skip-guard exhaustion (every filter exclusion, locked, non-transferable, equipped × 2 toggles), threshold matrix (custom override + presets + boundary), modifier-preservation (filter uses `ModifierPriceMultiplier`, not `ItemValue`), confirmation flow, audio invocation, refresh-on-success, no-inventory-active fallback, menu options enumeration.
- `TAOM.Tests/Features/QuickActions/InventorySearchCampaignBehaviorTests.cs` — 7 tests covering: default-true on construct, `OnTick` MCM reconciliation in both directions, `OnNewGameCreated` seed (true + false), `OnGameLoaded` reconcile when MCM disagrees.
- `TAOM.Tests/Features/QuickActions/DamagedQualityPresetTests.cs` — 9 tests covering: preset → threshold (4 presets), dropdown index → preset (4 valid + out-of-range fallback to Moderate).

Total: 50 QuickActions tests, all green.

## How To: Add a New Quick Action

1. Add a new value to `QuickActionType` enum.
2. Add the option in `QuickActionsService.GetMenuOptions()` and `OpenMenu()`'s inquiry-element list.
3. Add a routing case in `QuickActionsService.DispatchSelected()`.
4. Implement the service method (returns `QuickActionResult`). Iterate `_inventory.GetRightPaneItems()`, apply filters via `IInventoryItemAdapter` properties (do NOT cast `UnderlyingVm` back to `SPItemVM` — keeps ADR-007 intact).
5. Write skip-guard tests for every filter exclusion (one positive, one negative per setting flag). See `csharp-architecture.md` "Skip-Guard Exhaustion" rule.

## How To: Change a Sound

Update the `SellEvent` / `UnequipEvent` constants in `QuickActionsAudioPlayer.cs`. Vanilla event IDs are discoverable via `grep 'event:/ui' E:/Decompiled_Bannerlord/`. Common alternatives:
- `event:/ui/notification/coins_positive` — gold gained chime
- `event:/ui/inventory/take_all` — bulk transfer
- `event:/ui/transfer` — generic inventory transfer (current default)

## Performance

- Patch34 is a UI-driven entry point; "Sell All" is pressed at most a few times per market visit. Allocating a `MultiSelectionInquiryData` with 4 elements per click is fine; no caching needed.
- The filter loop iterates `RightItemListVM` (typically <100 items). No reflection on the hot path — direct property reads only. No allocation per item beyond the adapter wrappers (one `InventoryItemAdapter` per right-pane item per click).
- `InventoryVMAdapter` is a singleton; the active-VM reference is replaced on each inventory open via the constructor Postfix.

## GitHub Issue

- **Issue:** _to be created with closing commit_
- **Status:** Open (in-game verification pending)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
