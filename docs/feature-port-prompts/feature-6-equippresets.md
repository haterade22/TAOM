# Feature Port Session: EquipPresets

You are porting feature #6 of 7 from the external-developer drop at `Downloads/Features_fixed/EquipPresets/` into TAOM's `Main/Features/EquipPresets/`. The other 6 features are tracked separately. Don't touch them.

## Prerequisites — read before writing any code

1. **The integration plan**: `C:/Users/mikew/.claude/plans/one-of-our-coders-steady-raccoon.md` — section "4. EquipPresets" has the planned file layout.

2. **This prompt** — end to end.

3. **Pattern templates**:
   - [Main/Features/SiegeDismount/](../../Main/Features/SiegeDismount/) — for the snapshot-token pattern (carrying full `EquipmentElement` through service boundary while keeping interface opaque)
   - [Main/Features/MixedFormations/](../../Main/Features/MixedFormations/) — for cache + lifecycle cleanup pattern
   - [Main/Adapters/PlayerMountAdapter.cs](../../Main/Adapters/PlayerMountAdapter.cs) + [PartyMountInventoryAdapter.cs](../../Main/Adapters/PartyMountInventoryAdapter.cs) — for `EquipmentElement`-overload usage (modifier preservation)
   - [docs/features/siege-dismount.md](../features/siege-dismount.md) — feature doc template

4. **Feature 5 (QuickActions) MUST be done first.** This feature **reuses `IInventoryVMAdapter`** introduced by feature 5. Verify [Main/Adapters/IInventoryVMAdapter.cs](../../Main/Adapters/IInventoryVMAdapter.cs) exists before starting; if not, run feature 5 first or coordinate with the user.

5. **The decompiled source you're porting**:
   `C:/Users/mikew/Downloads/Features_fixed/_decompiled/EquipPresets/EquipPresets.decompiled.cs`

   Read it end-to-end. Critical sections: `EquipmentPresetCampaignBehavior` (`SyncData("EquipPresets_HeroPresets")` — `Dictionary<heroStringId, List<HoNEquipmentPreset>>`), `PresetManager` (CRUD logic), `HoNEquipmentPreset` + `HoNPresetItemReference` (saveable data with `[SaveableProperty]`), `PresetSaveableTypeDefiner` (BaseId 726900501), `HoNEquipmentPresetPatches` (Postfix on `SPInventoryVM.RefreshValues` to cache active VM), `InventoryScreenOpenPatch` / `InventoryScreenClosePatch` (GauntletLayer for the "Presets" overlay button).

6. **GUI prefab**: the original ships `GUI/Prefabs/PresetsOverlay.xml` — **copy it verbatim to `Main/_Module/GUI/Prefabs/PresetsOverlay.xml`**. It uses only vanilla brushes, no custom sprites.

## Goal in one sentence

Add a "Presets" button overlay to the inventory screen that lets the player save / load / update / delete per-hero equipment presets, persisted across the campaign save.

## Architecture — what to build

### Files to create

```
Main/Features/EquipPresets/
├── IEquipmentPresetService.cs            ← Save/Load/Update/Delete/List + LoadPresetWithReport(presetId) returning PresetLoadResult
├── EquipmentPresetService.cs             ← state: per-hero preset dict; implements ISyncDataConsumer pattern
├── IEquipPresetsSettingsProvider.cs
├── EquipPresetsSettingsProvider.cs
├── Models/
│   ├── HoNEquipmentPreset.cs             ← [SaveableProperty]-decorated; Id, Name, Items, CivilianItems, IsCivilian
│   ├── HoNPresetItemReference.cs         ← [SaveableProperty]-decorated; ItemStringId, SlotIndex, ItemModifierStringId
│   └── PresetLoadResult.cs               ← struct: equipped count, missing items list, locked items list
├── PresetSaveableTypeDefiner.cs          ← BaseId=726900501; HoNEquipmentPreset id=102, HoNPresetItemReference id=101 — VERIFY UNIQUENESS in TAOM (see step 1 below)
├── EquipPresetsIoC.cs
├── UI/
│   ├── PresetsOverlayVM.cs                ← port of decompiled VM verbatim (ButtonText, ExecuteOpenPresets command)
│   └── (PresetsOverlay.xml is in Main/_Module/GUI/Prefabs/)
└── Hooks/
    ├── EquipmentPresetCampaignBehavior.cs ← SyncData; loads presets per session; OnGameLoaded handles missing-hero entries
    ├── Patch33_GauntletInventoryScreen.cs ← Postfix on OnInitialize creates GauntletLayer with "PresetsOverlay" prefab; Prefix on OnFinalize cleans up
    └── Patch33_SPInventoryVMRefresh.cs    ← Postfix on RefreshValues — caches the active VM so OpenPresetMenu can find it

Main/_Module/GUI/Prefabs/
└── PresetsOverlay.xml                     ← COPY VERBATIM from Downloads/Features_fixed/EquipPresets/GUI/Prefabs/PresetsOverlay.xml

TAOM.Tests/Features/EquipPresets/
├── EquipmentPresetServiceTests.cs         ← save/load/update/delete; missing item handling; locked item respect; modifier preservation round-trip
├── HoNEquipmentPresetTests.cs             ← saveable round-trip via mock IDataStore
└── PresetSaveableTypeDefinerTests.cs      ← unique ID verification across TAOM
```

### Adapter usage

| Adapter | Source | Why |
|---|---|---|
| `IInventoryVMAdapter` | EXISTING (from feature 5 QuickActions) | Reuse to read/write the active inventory VM. Both features share this surface. |
| `IInventoryItemAdapter` | EXISTING (from feature 5) | Same. |
| `IEquipmentSlotAdapter` | NEW (introduced by THIS feature) — exposes Hero.MainHero.BattleEquipment / CivilianEquipment slot read/write across all 11 EquipmentIndex values; uses `EquipmentElement` to preserve modifier. **Mirror the pattern from `PlayerMountAdapter` (SiegeDismount) — full `EquipmentElement` storage, NOT just `StringId`.** | EquipPresets reads/writes 11 slots per hero; SiegeDismount reads/writes only 2. The new adapter generalizes the pattern. |
| `IItemModifierLookupAdapter` | NEW — wraps `MBObjectManager.Instance.GetObject<ItemModifier>(stringId)` so a saved modifier can be restored on load. | The saveable model stores `ItemModifierStringId`; on load, look up the actual `ItemModifier` object and reconstruct the `EquipmentElement(item, modifier)`. |

### Harmony patches

Reserve **`Patch33_EquipPresets`**. Three patches under this category:

1. `SPInventoryVM.RefreshValues` (Postfix) — caches the current VM so `OpenPresetMenu` can find it.
2. `GauntletInventoryScreen.OnInitialize` (Postfix) — creates a `GauntletLayer` with z-order **1000** (NOT 100 as the original used — pick a high z-order to avoid conflicts with other mods/TAOM features), loads `"PresetsOverlay"` movie, sets datasource = `PresetsOverlayVM` instance.
3. `GauntletInventoryScreen.OnFinalize` (Prefix) — removes the layer, nulls the static cache.

Wire in `Main/SubModule.cs` `OnGameInitializationFinished`:
```csharp
_harmony.PatchCategory("Patch33_EquipPresets");
```

### CampaignBehavior wiring

`Main/SubModule.cs` `OnGameStart` after the QuickActions InventorySearchCampaignBehavior:
```csharp
campaignStarter.AddBehavior(new EquipmentPresetCampaignBehavior(IoC.Resolve<IEquipmentPresetService>(), IoC.Resolve<IModLogger>()));
```

### MCM settings — append to `Main/Features/TaomSettings.cs`

Group: `Inventory/Equipment Presets`, GroupOrder = 33.

```csharp
[SettingPropertyGroup("Inventory/Equipment Presets", GroupOrder = 33)]
[SettingPropertyBool("Enable Equipment Presets", Order = 0,
    HintText = "Master toggle. When off, the Presets overlay is not added to the inventory screen and existing presets are inert (preserved in save).")]
public bool EnableEquipmentPresets { get; set; } = true;

[SettingPropertyGroup("Inventory/Equipment Presets")]
[SettingPropertyInteger("Max Presets Per Character", 1, 20, Order = 1,
    HintText = "Maximum saved presets per hero. Default: 10.")]
public int MaxPresetsPerCharacter { get; set; } = 10;

[SettingPropertyGroup("Inventory/Equipment Presets")]
[SettingPropertyBool("Equipment Presets Debug Mode", Order = 2,
    HintText = "Show diagnostic [EquipPresets] messages on the in-game HUD. Off = file log only.")]
public bool EquipPresetsDebug { get; set; } = false;
```

### IoC registration

```csharp
using TAOM.Features.EquipPresets;
// ...
EquipPresetsIoC.RegisterEquipPresetsFeature(container);
```

Registers (Reuse.Singleton):
- `IEquipPresetsSettingsProvider → EquipPresetsSettingsProvider`
- `IEquipmentSlotAdapter → EquipmentSlotAdapter`
- `IItemModifierLookupAdapter → ItemModifierLookupAdapter`
- `IEquipmentPresetService → EquipmentPresetService`

## Cross-session memory rules that apply to THIS feature

| Memory | How it applies here |
|---|---|
| `feedback_substring_keyword_matches_external_data.md` | NOT APPLICABLE — feature uses no scene-name matching. |
| `feedback_adapter_modifier_preserving_overload.md` | **APPLIES STRONGLY.** This is the canonical use case. Every preset slot stores an `EquipmentElement` with `ItemModifier`. On save → serialize StringId + ItemModifierStringId. On load → look up both, reconstruct `new EquipmentElement(item, modifier)`. On equip → write the FULL EquipmentElement to the slot (NOT `new EquipmentElement(item)` which drops the modifier). Audit every code path that touches an EquipmentElement; verify the modifier survives. The adapter pattern from SiegeDismount's `PlayerMountAdapter` is the template — copy it. |
| `feedback_user_facing_promise_must_match_code.md` | **APPLIES.** Trace `MaxPresetsPerCharacter` to its enforcement (the service must reject Save when count is at max with a user-facing message; not silently overwrite). Trace `EnableEquipmentPresets` to gates everywhere. The original module had a `MaxPresetsPerCharacter` setting — verify it's actually enforced; if it's dead code, implement the enforcement. |

## Per-feature gotchas

1. **SaveableType ID uniqueness — VERIFY FIRST.** Original uses `BaseId 726900501`, item-ref id 101, preset id 102. Before writing the SaveableTypeDefiner:
   ```bash
   grep -rn "SaveableTypeDefiner\|BaseId" Main/ --include "*.cs"
   ```
   Confirm no existing TAOM SaveableTypeDefiner uses these IDs. If collision — pick fresh IDs in the same range (TAOM has plenty of room above 7269005xx).

2. **Save-load with missing items.** A preset saved before the player removed a mod will reference items that no longer exist. The service's `LoadPresetWithReport` MUST report missing items in `PresetLoadResult.MissingItems` and skip those slots. Show user a confirmation dialog: "X items in this preset no longer exist; equip the rest? [Yes/No]".

3. **Save-load with deleted heroes.** If a hero in the preset dict was killed/deleted between save sessions, the dict entry is orphaned. On `OnGameLoaded`, prune any preset whose `heroStringId` doesn't resolve to an active Hero. Log a `LogInfo` summary: "Pruned N orphaned preset bundles."

4. **Civilian vs Battle equipment.** Each hero has TWO separate equipment sets. The preset must record which type (`IsCivilian` flag) and apply to the correct slot set on load.

5. **`SPItemVM.IsLocked`.** Original sets this to keep equipped-on-hero items from being mutated mid-load. Preserve this; check `IsLocked` BEFORE writing to a slot.

6. **GauntletLayer z-order conflict (Codex flagged for SiegeDismount-class features).** Original used `100`. Use **`1000`+** to avoid TAOM's other layers. Document the chosen z-order in the feature doc.

7. **Patch interaction with TAOM Patch23 (BannerColorPersistence).** Patch23 patches `SPInventoryVM.UpdateCurrentCharacterIfPossible` (different method from `RefreshValues`); no direct collision. But both run on the same screen. Test rapid character swaps in-game during verification.

8. **`InventoryScreenOpenPatch` static state cleanup.** The original keeps a `_currentVM` and `_layer` static. On `OnFinalize` cleanup, null both. Add a `LogWarning` if `_currentVM` is non-null when a NEW `OnInitialize` fires (means previous teardown leaked).

9. **PresetsOverlay.xml binding paths.** The XML binds to `PresetsOverlayVM`'s `ButtonText` property and `ExecuteOpenPresets` command. Verify the VM exposes both with `[DataSourceProperty]` and a parameterless `void ExecuteOpenPresets()` method.

## Verification of v1.3.15 API surface

```bash
# SaveableTypeDefiner base class
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.SaveSystem.dll" -t "TaleWorlds.SaveSystem.SaveableTypeDefiner" 2>&1 | head -30

# IDataStore.SyncData
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.SaveSystem.dll" -t "TaleWorlds.SaveSystem.IDataStore" 2>&1

# GauntletInventoryScreen lifecycle
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/SandBox.GauntletUI.dll" -t "SandBox.GauntletUI.Inventory.GauntletInventoryScreen" 2>&1 | grep -E "OnInitialize|OnFinalize"

# Equipment slot indexer (lossless setter)
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.Core.dll" -t "TaleWorlds.Core.Equipment" 2>&1 | grep -A 4 "this\[EquipmentIndex"

# ItemModifier lookup
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.Core.dll" -t "TaleWorlds.Core.ItemModifier" 2>&1 | head -30
```

## Acceptance gates

- Build clean — 0 errors
- Tests: at least 35 tests covering: save/load/update/delete each preset; missing-item path (4+ scenarios); locked item respect; modifier preservation (assert both ItemModifier presence on round-trip AND that the price multiplier survives); MaxPresetsPerCharacter enforcement (positive AND negative); orphaned-hero pruning; SaveableType ID uniqueness; civilian vs battle equipment routing; ConditionalWeakTable / static cleanup
- Full suite stays green
- `docs/features/equip-presets.md` from TEMPLATE — cite the modifier-preservation audit, the SaveableType ID range, the GauntletLayer z-order choice
- CHANGELOG.md entry at top
- `/deep-review EquipPresets` and `/review-codex EquipPresets` — fix every confirmed finding
- New feedback memory if RCA produced one

**Do NOT commit** — leave dirty for in-game test.

## Verification — in-game golden path

1. Start a campaign with a few heroes and items in inventory.
2. MCM → TAOM → "Inventory / Equipment Presets" → confirm `Enable=true`, `MaxPresets=10`.
3. Open inventory screen.
4. Bottom of screen: a "Presets" button appears (overlay).
5. Click "Presets" → menu appears: Save New, Load, Update, Delete (last 3 only if presets exist).
6. Equip hero with specific items (incl. one with a quality prefix like "Sharp"). Save preset → name it "TestPreset".
7. Switch heroes via the inventory screen's character dropdown.
8. Equip the new hero with completely different items.
9. Switch back to the original hero — equipment should NOT auto-revert (preset is per-hero, not per-character-swap).
10. Click "Presets" → "Load" → "TestPreset" → equipment restored, INCLUDING the "Sharp" prefix on the previously-equipped item.
11. Modify equipment → click "Presets" → "Update" → "TestPreset" → confirm overwrite.
12. Save the campaign. Quit Bannerlord. Restart. Load the save. Open inventory → presets persist.
13. Disable round-trip: set `Enable Equipment Presets = false` → reload → "Presets" button absent, but the preset data remains in the save (verify by re-enabling and confirming presets reappear).

## Final report format

```
EquipPresets port complete.
- Files created: [count] (saveable types, service, adapters, hooks, UI VM, prefab copy, tests, doc)
- Files modified: TaomSettings.cs, IoC.cs, SubModule.cs (Patch33 + behavior registration)
- IInventoryVMAdapter reused from feature 5: [confirmed]
- New IEquipmentSlotAdapter introduced (modifier-preserving)
- SaveableType IDs: BaseId=726900501, HoNPresetItemReference=101, HoNEquipmentPreset=102 — uniqueness verified across Main/
- GauntletLayer z-order: [chosen value]
- ItemModifier round-trip verified: [yes / how]
- Tests: NN/NN EquipPresets tests pass; XXXX/XXXX total
- /deep-review verdict: [PASS / N findings fixed]
- /review-codex verdict: [PASS / N findings fixed]
- New feedback memories codified: [list]
- Awaiting in-game verification before commit.
```
