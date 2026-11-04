# Telchar's Mark

## Overview

Telchar's Mark tints the rows of the inventory screen: green when an item beats what the shown character
has equipped in the slot it would go to (an empty slot counts as beaten), yellow when armour trades its
own slot's value for a better weighted total. A checkbox beside the filter row, "Show only better items",
hides every other row in both panes. Two MCM settings under `Inventory/Quick Actions` control it.

## Why This Exists

- **Vanilla behavior:** The inventory lists items and the equipped slots side by side, and the player
  compares tooltips one item at a time. A large loot screen or a full stash has hundreds of rows.
- **TAOM requirement:** Total-conversion gear has many near-identical pieces per slot across many
  cultures and races (see the Armory), so finding the one worth wearing by hand is slow.
- **Without this feature:** The player opens every tooltip, or sells and equips by guesswork.

## Architecture

### Design Challenge

The comparison is pure arithmetic, but it must read the engine's modifier-aware item numbers, follow the
outfit on screen (civilian, battle, stealth) and the character switch, and keep up with vanilla's
transfers. The vanilla filter flag (`SPItemVM.IsFiltered`) has one writer, a private method, so hiding
rows means running after it.

### Solution Approach

The engine-free half is `ItemUpgradeScorer` (verdicts) and `TelcharsMarkService` (`Rescore`,
`ShouldHide`), working on the `IUpgradeScreen` and `IUpgradeRow` seams in `Main/Adapters`. The engine half:

- **`UpgradeScreenMixin`** (UIExtenderEx, one per `SPInventoryVM`) owns the feature for one screen.
  UIExtenderEx builds it before the VM constructor returns, after `InitializeInventory` filled both lists,
  so the open-time rescore runs there once. It reads both MCM settings once, holds the per-screen adapter
  and the checkbox state, and exposes `TaomShowOnlyUpgrades`, `TaomUpgradeLensEnabled` and
  `TaomShowOnlyUpgradesHint`. With the feature off at open there is no adapter, so the postfixes do nothing
  and the checkbox is hidden.
- **`InventoryUpgradeScreen`** (adapter, bound to one VM) implements `IUpgradeScreen`: both panes' rows,
  the loadout read from the slot view models and `EquipmentMode`, and `ReapplyFilters`, which calls the
  private `UpdateFilteredStatusOfItem` through a cached open delegate (never the public `ExecuteFilter*`
  methods, which raise `InventoryFilterChangedEvent`).
- **`UpgradeRowMixin`** (one per `SPItemVM`) is the `IUpgradeRow`: its candidate is read fresh through
  `UpgradeCandidateReader`, and its verdict drives `TaomIsUpgrade` and `TaomIsSidegrade`.
- **`UpgradeStatsReader`** uses the modifier-aware `EquipmentElement` getters, so a "Sharp" or "Damaged"
  prefix changes the verdict as it changes the tooltip. **`UpgradeItemKindMap`** turns
  `ItemObject.ItemTypeEnum` into the slot kind and the engine `EquipmentMode` into `InventoryEquipmentMode`.
- **Prefab extensions:** `UpgradeTintPrefab` inserts the two tint widgets after the tutorial highlight
  inside the row's `Body`; `UpgradeToggleButtonPrefab` appends the checkbox as the last child of the
  center panel's `TopMenu` (`Index` int.MaxValue; not inside `CenterItems`, which vanilla indexes by
  position). The checkbox is vanilla's toggle idiom (`ButtonType="Toggle"`, `IsSelected`, `ToggleIndicator`).
- **Patch109 postfixes** rescore after vanilla's actions and apply the hide (see below).

The tint sprite is `BlankWhiteSquare_9` (category `ui_group1`, always loaded), colored `#4CC25A55` (green)
and `#E8C54A55` (yellow). The checkbox uses the vanilla brushes `SPOptions.Checkbox.Empty.Button` and
`SPOptions.Checkbox.Full.Button`.

### Scoring rules

- **Who is scored:** list rows that are equipable and usable by the shown character (vanilla's
  `CanCharacterUseItem`, which checks skill, the gender flags and unrideable mounts). In civilian mode only civilian items, in
  stealth mode only stealth items. Banners, goods, animals, books and `ChestArmor` get no verdict.
- **Armour** (head, body, legs, gloves, cape): the main value is the slot's own armour (a cape uses its
  body armour). Empty slot: green. Higher main value: green. Equal main value and a higher weighted total:
  green. Lower main value but a higher weighted total: yellow. The weighted total is
  head x 34 + body x 42 + legs x 12 + arms x 12.
- **Weapons and ammo** follow vanilla's one-click equip target: any empty weapon slot makes the item green
  (vanilla equips it there). With all four slots full, it is compared with the first slot holding the same
  item type; otherwise it gets no verdict. Weapons compare their best swing, thrust or missile damage over
  all usages; ammo compares damage, then stack size.
- **Shields** follow vanilla's retarget onto the first equipped shield: if a shield is equipped, the first
  one is compared (armour plus hit points), even when a slot is empty; otherwise an empty slot makes it green.
- **Horses** compare speed + charge + manoeuvre + 5% of hit points, without a harness. Empty slot: green.
- **Harnesses** need a horse equipped and a matching family (an unknown family is not a mismatch), then
  compare mount body armour. Empty harness slot with a horse: green.

### Component Diagram

```
MCM (EnableTelcharsMark, TelcharsMarkShowOnlyUpgradesByDefault)
        |
TelcharsMarkSettingsProvider      ItemUpgradeScorer
        |                               |
UpgradeScreenMixin (per screen) --- TelcharsMarkService
        |                               |  (IUpgradeScreen / IUpgradeRow)
InventoryUpgradeScreen          UpgradeRowMixin (per row) + UpgradeCandidateReader + UpgradeStatsReader
        |                               |
Patch109 postfixes (3)          Inventory / InventoryItemTuple prefab extensions
        |
SPInventoryVM / SPItemVM
```

## Configuration

Both settings live in `Main/Features/TaomSettings.cs`, group `Inventory/Quick Actions`, and are player-local
for co-op (`CoopSettingsRelevance` PlayerLocal). The screen mixin reads them when the inventory opens, so a
change takes effect at the next open.

### Current Values

| Setting | Default | Effect |
|---|---|---|
| `EnableTelcharsMark` ("Highlight better items") | on | Tints and the checkbox |
| `TelcharsMarkShowOnlyUpgradesByDefault` ("Show only better items by default") | off | The checkbox starts ticked on every open |

With the box ticked, green and yellow rows stay visible. Vanilla's move-all buttons (Take all, Give all,
Buy all, Sell all or Discard all, depending on the screen) and Quick Actions "Sell All (Vanilla)" then move
only the rows shown, as they do with vanilla's own type filters; Reset or Cancel undoes it before Done.
Quick Actions Sell Damaged and Sell Low Value ignore the hide.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/TelcharsMark/UpgradeTypes.cs` | Verdicts, kinds, modes, item stats, loadout, candidate |
| `Main/Features/TelcharsMark/ItemUpgradeScorer.cs` | Verdict for one item against the loadout |
| `Main/Features/TelcharsMark/TelcharsMarkService.cs` | Rescore and the hide rule |
| `Main/Features/TelcharsMark/ITelcharsMarkSettingsProvider.cs` + impl | The two MCM settings |
| `Main/Features/TelcharsMark/TelcharsMarkIoC.cs` | DryIoc registration |
| `Main/Features/TelcharsMark/TelcharsMarkLog.cs` | Error log, repeats suppressed |
| `Client/Features/TelcharsMark/UI/UpgradeScreenMixin.cs` | Owns the feature for one screen |
| `Client/Features/TelcharsMark/UI/UpgradeRowMixin.cs` | One row: candidate, verdict, tint flags |
| `Client/Features/TelcharsMark/UI/UpgradeTintPrefab.cs`, `UpgradeToggleButtonPrefab.cs` | Prefab extensions |
| `Client/Features/TelcharsMark/Hooks/Patch109_SPInventoryVMRefreshInformation.cs` | Rescore after `RefreshInformationValues` |
| `Client/Features/TelcharsMark/Hooks/Patch109_SPInventoryVMAfterTransfer.cs` | Rescore after a transfer no vanilla handler follows |
| `Client/Features/TelcharsMark/Hooks/Patch109_SPInventoryVMUpdateFiltered.cs` | Hide rows after `UpdateFilteredStatusOfItem` |
| `Main/Adapters/IUpgradeScreen.cs`, `IUpgradeRow.cs` | The adapter seams |
| `Main/Adapters/InventoryUpgradeScreen.cs` | `IUpgradeScreen` over one inventory VM |
| `Main/Adapters/UpgradeCandidateReader.cs`, `UpgradeStatsReader.cs` | Row and item reads |
| `Main/Adapters/UpgradeItemKindMap.cs` | Item type to slot kind, engine mode to TAOM mode |

## Dependencies

- `ITelcharsMarkSettingsProvider`, `TelcharsMarkService` and `IModLogger`, resolved once per screen.
- UIExtenderEx, registered with TAOM's other extensions by assembly scan. The extensions are not marked
  `[CoopSuppressedUi]`: they change nothing a co-op host owns.

## Tests

64 tests in `TAOM.Tests/Features/TelcharsMark/`:

- `ItemUpgradeScorerTests.cs` (44): every scoring rule above, one test per gate.
- `TelcharsMarkServiceTests.cs` (7): rescore, score-then-reapply order, one test per hide condition.
- `UpgradeItemKindMapTests.cs` (4): every `ItemTypeEnum` value and every engine `EquipmentModes` value is
  decided, so an engine update that adds one fails here. No game needed.
- `UpgradeStatsReaderTests.cs` (2, RequiresGame): an empty element and a horse with no horse data.
- `Patch109WiringTests.cs` (6): target and category on all three patches, the SubModule and IoC lines, the
  `item` parameter name, the checkbox appended last.
- `UpgradeScreenAdapterBindingTests.cs` (1) and the `UpdateFilteredStatusOfItem` row in
  `ReflectionSiteBindingTests`: the open delegate binds. The patch targets bind through
  `HarmonyPatchBindingTests`; the prefab XPaths through `PrefabExtensionBindingTests`.

## How to: decide a new item type after an engine update

1. `UpgradeItemKindMapTests.EveryItemType_IsDecidedHere` fails and names the new `ItemTypeEnum` value.
2. Decide its kind in `UpgradeItemKindMap.FromItemType` (`None` if it never sits in an equipment slot).
3. Add the value to the test's `Expected` table, and a scoring test if the kind is new.

## Performance

- `UpdateFilteredStatusOfItem` runs once per row on every filter click and search keystroke. The postfix
  returns after a field read when vanilla already hid the row, and after one weak-table lookup when the
  box is off; it allocates nothing.
- A rescore walks both panes and reads each row's stats; while the box is ticked it also re-runs the
  filter on every row. It runs once at open, once per vanilla action (the AfterTransfer postfix waits while
  vanilla's `IsRefreshed` is false), once per command for TAOM's own batches (Quick Actions Unequip All,
  EquipPresets: up to 24 small passes per click), and once per sold stack for Quick Actions Sell Damaged
  and Sell Low Value, because each sell runs vanilla's own handler (K stacks, K+1 rescores). The cost is
  not yet measured in game: multiply the open-rescore time below by these counts.
- UIExtenderEx copies a view model's binding tables for every mixed instance. Measured outside the game,
  that is about 5 us and 7.8 KB per `SPItemVM`, so a 1,000-row loot screen costs roughly 5 ms and 8 MB at
  open. The open rescore logs its time at debug level (`[TelcharsMark] open rescore`).

## Known limitations

- **Mouse only:** the checkbox sits outside the top menu's gamepad navigation scopes.
- **Companions and locked items:** vanilla refuses to equip a non-transferable item (Warehouse mode) on a
  companion, but no public member tells the screen who is shown, so such a row can still turn green.
- **The box starts from the MCM default on every open**; it is not remembered between opens.

## Owed in-game checks

- Tint and alpha are readable on both panes and on the "can't use" rows; row clicks, drag, hover tooltip
  and the buy and sell buttons still work.
- The checkbox renders above the tab-bar art beside the filter row, ticks, shows its hint, and its MCM
  default works.
- Ticking hides non-upgrades in both panes; unticking restores them; type filters and search compose.
- An empty-slot tint on a fresh companion; no tint on gear the shown character lacks the skill for.
- Outfit and character switch change the verdicts; a civilian-mode open is scored.
- Take all on 100+ stacks, a sell-all at a town, an EquipPresets load, Reset: tints are right after each.
- A large loot screen: the open time in the debug line.

## Changelog

- 2026-10-19: Telchar's Mark added: inventory upgrade and sidegrade tints, a "Show only better items"
  checkbox, two MCM settings.

## GitHub Issue

- **Issue:** [#875](https://github.com/haterade22/TAOM/issues/875), Inventory upgrade highlight and a
  show-only-better-items filter.
- **Status:** Open. Paid translation of the five `taom_tm_*` keys is owed on Mike's word.

---

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)

<!-- backlinks-end -->
