# Executive Summary

VERDICT: ISSUES FOUND.
CRITICAL: 2 | HIGH: 3 | MEDIUM: 3 | LOW: 1 | INFO: 2
Top risks: current source tree is not actually wired for EquipPresets; the load path bypasses vanilla InventoryLogic and can create/delete/stale-display equipment; presets cannot represent empty slots.
The "Patch33 uncommented" prior fix does not exist in the current `SubModule.cs`.
The MCM settings provider references `TaomSettings` properties that are absent from the current `TaomSettings.cs`.
All TaleWorlds API claims below were verified against installed v1.3.15 DLLs with `ilspycmd`, not `E:\Decompiled_Bannerlord`.

# 6 Known Suspects

| # | Suspect short name | Verdict | Evidence (file:line + vanilla cite) | Recommended fix |
|---|---|---|---|---|
| 1 | `PromptSaveName` hardcodes `includeMount=true` | PARTIAL | CONFIRM hardcode: `PresetsOverlayVM.cs:130-132` sets `includeMount = true` and passes it to `SaveCurrent`; `EquipmentPresetService.cs:64` persists it. Vanilla slot cite: `TaleWorlds.Core.EquipmentIndex` has `Horse = 10`, `HorseHarness = 11`. DISPUTE removing the save field: service/model already support false and save ids are allocated. | Keep `[SaveableProperty(5)] IncludesMount`, but add real UI choice or make the in-game save prompt disclose that mounts are always included in v1. |
| 2 | `TextObject.SetTextVariable(string,string)` chainability | DISPUTE | `PresetsOverlayVM.cs:113-118`, `185-189`, `258-263` chain calls. Installed DLL `TaleWorlds.Localization.dll`, `TextObject.SetTextVariable(string tag, string variable)` calls `SetTextVariableFromObject` and `return this;`; the int overload does the same. | No code fix. This is valid v1.3.15 API usage. |
| 3 | `ActiveHeroStringId` can key presets with null-hero character | DISPUTE | Adapter null-guards at `InventoryScreenAdapter.cs:45-48`. Vanilla `CharacterObject.IsHero => _heroObject != null`; `SPInventoryVM.UpdateCurrentCharacterIfPossible` only assigns `_currentCharacter` inside `if (character.IsHero)`. | No code fix for this suspect. Consider logging once if `_currentCharacter` is non-hero, but do not treat it as a confirmed leak path. |
| 4 | `OnGameLoaded` orphan pruning can drop live heroes during transient empty `AllAliveHeroes` | DISPUTE | `EquipmentPresetCampaignBehavior.cs:62-73` builds a set and service refuses to prune empty sets at `EquipmentPresetService.cs:139-143`. Vanilla `Hero.AllAliveHeroes => Campaign.Current.AliveHeroes`; `Campaign.AliveHeroes => CampaignObjectManager.AliveHeroes`. Captured/fugitive alive heroes are not filtered out by this TAOM code. | No suspect fix. Keep the empty-set guard. The misleading comments saying `Hero.IsActive` is the gate should be corrected, but the implementation uses `AllAliveHeroes`, not `IsActive`. |
| 5 | Modifier preservation chain | CONFIRM, with caveat | Save path: `EquipmentSlotAdapter.cs:46-51` reads `element.ItemModifier?.StringId`, `EquipmentPresetService.cs:167,176` writes `HoNPresetItemReference`, `HoNPresetItemReference.cs:31-32` persists property 3. Load path: `EquipmentPresetService.cs:194-204` validates and passes modifier id; `EquipmentSlotAdapter.cs:73-86` resolves `ItemModifier` and constructs `new EquipmentElement(item, modifier)`. Vanilla `EquipmentElement(ItemObject, ItemModifier, ItemObject, bool)` stores `ItemModifier`, and `Equipment.this[EquipmentIndex].set` writes the full element to `_itemSlots`. | No modifier-drop fix in this chain. The larger load-path bug is not modifier loss; it is bypassing vanilla InventoryLogic, reported below. |
| 6 | Overlay z-order 1000 vs vanilla and TAOM collisions | CONFIRM, with caveat | `Patch33_GauntletInventoryScreen.cs:31,61` uses z-order 1000. Vanilla `SandBox.GauntletUI.GauntletInventoryScreen.OnInitialize` creates `new GauntletLayer("InventoryScreen", 15, true)`. TAOM grep found other z-orders: 1, 50, 100, 200, 206; no 1000 collision. Caveat: `SubModule.cs:381-384` does not patch `Patch33_EquipPresets`, so the valid layer code never runs in current source. | Keep z-order 1000. Restore Patch33 wiring in `SubModule.cs`. |

# Findings Beyond The Known Suspects

## CRITICAL

### Current source tree cannot compile as written

Severity: CRITICAL

File(s)+line(s): `Main/Features/EquipPresets/EquipPresetsSettingsProvider.cs:9,22,28`; `Main/Features/TaomSettings.cs:237-314`; `Main/TAOM.csproj:63-80`

Vanilla API evidence: not a TaleWorlds behavior claim. Source evidence is sufficient: `TAOM.csproj` does not exclude `Features\EquipPresets\**`, and no `TaomSettings` declarations exist for `EnableEquipmentPresets`, `MaxPresetsPerCharacter`, or `EquipPresetsDebug`.

Why it matters: `EquipPresetsSettingsProvider` dereferences three properties that are absent from the only `TaomSettings` class in the source tree. If these files are part of the build, Roslyn must fail name resolution. If build/test is green elsewhere, this workspace is not the green source tree described in the prompt.

Recommended fix: add the three MCM settings exactly as planned under `Inventory/Equipment Presets` with `GroupOrder = 33`, then add a test that reflects `TaomSettings` and asserts those properties exist.

Confidence: HIGH. I attempted `dotnet build`, but the sandbox blocked SDK access before compilation (`Microsoft SDKs` access denied), so the conclusion is from source-level C# name resolution.

### Load applies equipment by direct slot mutation instead of vanilla InventoryLogic

Severity: CRITICAL

File(s)+line(s): `PresetsOverlayVM.cs:174-183`; `EquipmentPresetService.cs:181-204`; `EquipmentSlotAdapter.cs:70-87,97-108`; `InventoryScreenAdapter.cs:85-90`

Vanilla API evidence (installed DLL):

```csharp
// SPInventoryVM.EquipEquipment, TaleWorlds.CampaignSystem.ViewModelCollection.dll
List<TransferCommand> list = new List<TransferCommand>();
TransferCommand item = TransferCommand.Transfer(1, itemVM.InventorySide,
    GetEquipmentToInventorySide(_equipmentMode), sPItemVM.ItemRosterElement,
    sPItemVM.ItemType, TargetEquipmentType, _currentCharacter);
list.Add(item);
...
_inventoryLogic.AddTransferCommands(list);

// InventoryLogic.TransferItem, TaleWorlds.CampaignSystem.dll
if (IsEquipmentSide(transferCommand.ToSide) &&
    transferCommand.ToSideEquipment[(int)transferCommand.ToEquipmentIndex].Item != null)
{
    TransferCommand transferCommand2 = TransferCommand.Transfer(1, transferCommand.ToSide,
        InventorySide.PlayerInventory,
        new ItemRosterElement(transferCommand.ToSideEquipment[(int)transferCommand.ToEquipmentIndex], 1),
        transferCommand.ToEquipmentIndex, EquipmentIndex.None, transferCommand.Character);
    list.AddRange(TransferItem(ref transferCommand2));
}
...
_rosters[(int)transferCommand.FromSide].AddToCounts(transferCommand.ElementToTransfer.EquipmentElement, -1);
transferCommand.ToSideEquipment[(int)transferCommand.ToEquipmentIndex] = elementToTransfer.EquipmentElement;
```

Why it matters: EquipPresets never checks whether the item is in the player's inventory and never transfers the displaced equipped item back to inventory. It only checks that the `ItemObject` exists in `MBObjectManager`, then assigns `equipment[(EquipmentIndex)slotIndex] = element`. Concrete repro path: save preset with sword A, sell or move sword A away, equip sword B, load preset. The code creates sword A in the slot without consuming inventory and overwrites sword B without depositing it. `_screen.RefreshActiveVM()` only calls `SPInventoryVM.RefreshValues`; vanilla `RefreshValues` refreshes existing slot VMs and text, while vanilla transfer flow updates slot VMs in `AfterTransfer -> UpdateEquipment`. The open inventory UI can remain stale until reopen.

This also bypasses vanilla equip gates:

```csharp
// SPInventoryVM.IsItemEquipmentPossible
if (!CanCharacterUseItemBasedOnSkills(itemVM.ItemRosterElement)) return false;
if (!CanCharacterUserItemBasedOnUsability(itemVM.ItemRosterElement)) return false;
if (!Equipment.IsItemFitsToSlot((EquipmentIndex)TargetEquipmentIndex, itemVM.ItemRosterElement.EquipmentElement.Item)) return false;
if (TargetEquipmentType == EquipmentIndex.HorseHarness)
{
    if (string.IsNullOrEmpty(CharacterMountSlot.StringId)) return false;
    if (!ActiveEquipment[EquipmentIndex.ArmorItemEndSlot].IsEmpty &&
        ActiveEquipment[EquipmentIndex.ArmorItemEndSlot].Item.HorseComponent.Monster.FamilyType !=
        itemVM.ItemRosterElement.EquipmentElement.Item.ArmorComponent.FamilyType) return false;
}
```

Recommended fix: load through the active `SPInventoryVM`'s `_inventoryLogic` and `_currentCharacter`, building `TransferCommand`s from `PlayerInventory`/equipment sides to the target equipment side. Validate `InventoryLogic.CheckItemRosterHasElement` for inventory-sourced items, preserve the full `EquipmentElement`, and let `InventoryLogic.AfterTransfer` update UI and mount-harness legality. Treat absent inventory items as `MissingItems` instead of conjuring them.

Confidence: HIGH.

## HIGH

### EquipPresets is not wired into IoC, Harmony, or campaign persistence

Severity: HIGH

File(s)+line(s): `Main/IoC.cs:52-80`; `Main/SubModule.cs:336-344`; `Main/SubModule.cs:381-384`; `Main/Features/EquipPresets/EquipPresetsIoC.cs:9-16`; `Main/Features/EquipPresets/Hooks/EquipmentPresetCampaignBehavior.cs:33-45`

Vanilla API evidence:

```csharp
// CampaignBehaviorManager.AddBehavior, TaleWorlds.CampaignSystem.dll
public void AddBehavior(CampaignBehaviorBase campaignBehavior)
{
    _campaignBehaviors.Add(campaignBehavior);
    campaignBehavior.RegisterEvents();
}
```

Why it matters: `EquipPresetsIoC.RegisterEquipPresetsFeature(container)` is never called, `_harmony.PatchCategory("Patch33_EquipPresets")` is absent, and `EquipmentPresetCampaignBehavior` is never added to `CampaignGameStarter`. Result: no overlay patch, no `SPInventoryVM` capture patch, no IoC registrations, and no `SyncData("EquipPresets_HeroPresets")` persistence. This directly disputes Claude prior fix #1.

Recommended fix: register `EquipPresetsIoC` in `IoC.Configure`, patch `Patch33_EquipPresets` in `OnGameInitializationFinished`, and add `campaignStarter.AddBehavior(IoC.Resolve<EquipmentPresetCampaignBehavior>())` unconditionally so disabled mode still preserves SyncData.

Confidence: HIGH.

### Production save path cannot represent empty slots

Severity: HIGH

File(s)+line(s): `EquipmentSlotAdapter.cs:42-51`; `EquipmentPresetService.cs:160-178,181-193`; `EquipmentPresetServiceTests.cs:347-358`

Vanilla API evidence:

```csharp
// Equipment, TaleWorlds.Core.dll
public const int EquipmentSlotLength = 12;
public EquipmentElement this[int index]
{
    get { return _itemSlots[index]; }
    set { IsItemFitsToSlot((EquipmentIndex)index, value.Item); _itemSlots[index] = value; }
}
```

Why it matters: `Capture` skips every empty `EquipmentElement`. `LoadPresetWithReport` only iterates saved refs. Therefore a preset saved with no helmet, no shield, or no horse cannot clear those slots when loaded over a hero who currently has those items. The unit test for clearing empty refs seeds an impossible production state (`battleItems: new[] { (3, "", "") }`); production capture never emits that empty ref.

Concrete repro path: save a "no shield" preset while slot 1 is empty; equip a shield in slot 1; load the preset. Slot 1 remains shielded because there is no saved ref instructing `ClearSlot`.

Recommended fix: either capture all relevant slots with empty `ItemStringId` sentinels, or clear the included slot range before applying saved non-empty refs. Keep the `includeMount` flag respected: do not clear slots 10/11 when `IncludesMount=false`.

Confidence: HIGH.

### Save from civilian view silently captures hidden battle equipment too

Severity: HIGH

File(s)+line(s): `PresetsOverlayVM.cs:120-132`; `EquipmentPresetService.cs:160-178`; `SPInventoryVM` vanilla `EquipmentModes` and `ActiveEquipment` evidence below

Vanilla API evidence:

```csharp
// SPInventoryVM, TaleWorlds.CampaignSystem.ViewModelCollection.dll
public enum EquipmentModes { Civilian, Battle, Stealth }
private Equipment ActiveEquipment
{
    get
    {
        switch (EquipmentMode)
        {
            case 0: return _currentCharacter.FirstCivilianEquipment;
            case 1: return _currentCharacter.FirstBattleEquipment;
            case 2: return _currentCharacter.FirstStealthEquipment;
        }
    }
}
```

Why it matters: the save prompt says "Save current equipment as a new preset", but the implementation always captures battle equipment at `EquipmentPresetService.cs:163` and captures civilian equipment only when the screen is in civilian mode. A player saving a civilian outfit while viewing the civilian tab also snapshots whatever hidden battle kit was present at that moment. Loading that preset later mutates both battle and civilian slots. This is not the "current equipment" the UI text promises.

Recommended fix: make the save UI explicit with choices: current outfit only, battle, civilian, both, include mount. At minimum, change the prompt text to disclose that civilian-view saves include battle equipment too.

Confidence: HIGH.

## MEDIUM

### Equipment adapter can mutate shared dead-equipment singletons

Severity: MEDIUM

File(s)+line(s): `EquipmentSlotAdapter.cs:39-40,67-68,86,103-108`

Vanilla API evidence:

```csharp
// Hero, TaleWorlds.CampaignSystem.dll
public Equipment BattleEquipment => _battleEquipment ?? Campaign.Current.DeadBattleEquipment;
public Equipment CivilianEquipment => _civilianEquipment ?? Campaign.Current.DeadCivilianEquipment;
```

Why it matters: `Hero.BattleEquipment` and `Hero.CivilianEquipment` do not return null for uninitialized equipment; they return shared fallback equipment. The adapter checks only `equipment == null`, then writes to the returned object. If an active or future caller hits a hero with null backing equipment, EquipPresets corrupts `Campaign.Current.DeadBattleEquipment` or `DeadCivilianEquipment` globally.

Recommended fix: before capture/apply/clear, compare against `Campaign.Current?.DeadBattleEquipment` or `DeadCivilianEquipment` for the selected mode and fail the operation instead of mutating the singleton.

Confidence: MEDIUM. The vanilla fallback is verified; whether inventory-selectable active heroes can have null backing equipment is not proven.

### Slot validity result is ignored on direct setter path

Severity: MEDIUM

File(s)+line(s): `EquipmentSlotAdapter.cs:81-87`; save data `HoNPresetItemReference.cs:25-32`

Vanilla API evidence:

```csharp
// Equipment.this[int].set, TaleWorlds.Core.dll
set
{
    IsItemFitsToSlot((EquipmentIndex)index, value.Item);
    _itemSlots[index] = value;
}
```

Why it matters: vanilla's setter calls `IsItemFitsToSlot` but ignores the return value. Since EquipPresets persists user/version-sensitive `SlotIndex` + `ItemStringId`, a tampered save or item XML type change can put a helmet in a weapon slot or a harness in the wrong slot. Vanilla `SPInventoryVM.IsItemEquipmentPossible` rejects these cases before building transfer commands; EquipPresets bypasses that.

Recommended fix: explicitly call `Equipment.IsItemFitsToSlot((EquipmentIndex)slotIndex, item)` in the adapter and return `SlotApplyOutcome.Failed` (or a new structured invalid-slot outcome) before assignment.

Confidence: HIGH.

### Dead `SetItemLocked` surface remains after SlotLocked cleanup

Severity: MEDIUM

File(s)+line(s): `IInventoryScreenAdapter.cs:25-30`; `InventoryScreenAdapter.cs:63-83`; search excluding `bin/obj` found no EquipPresets consumer

Vanilla API evidence:

```csharp
// SPItemVM, TaleWorlds.CampaignSystem.ViewModelCollection.dll
public bool IsLocked
{
    get { return _isLocked; }
    set
    {
        if (value != _isLocked) { _isLocked = value; OnPropertyChangedWithValue(value, "IsLocked"); }
    }
}
```

Why it matters: Claude removed `SlotApplyOutcome.SlotLocked` and `PresetLoadResult.SkippedLockedSlots`, but left an unused interface method whose comments still claim "Used by Load". It also iterates `RightItemListVM` directly and catches enumeration failures instead of snapshotting. This is dead API surface and misleading future-maintainer documentation.

Recommended fix: delete `SetItemLocked` from `IInventoryScreenAdapter` and `InventoryScreenAdapter` until a real lock-aware load path exists. If it returns later, snapshot the list before mutation and preserve pre-existing locks.

Confidence: HIGH.

## LOW

### Save-state restore accepts null/corrupt nested collections without normalization

Severity: LOW

File(s)+line(s): `EquipmentPresetService.cs:32-35,37-40,83-90,103-105,181-187`; `PresetsOverlayVM.cs:142-146,258-263`

Vanilla API evidence:

```csharp
// SaveablePropertyAttribute, TaleWorlds.SaveSystem.dll
public SaveablePropertyAttribute(short localSaveId)
{
    LocalSaveId = localSaveId;
}
```

Why it matters: future save migrations or a corrupt save can deserialize a dictionary entry with a null list, null preset, or null `Items`/`CivilianItems`. The service assigns the dictionary directly and later dereferences lists/presets. This is not a normal v1 save path, but save-compat code should be robust because these ids are now permanent.

Recommended fix: normalize in `RestoreFromSerializableState`: drop null keys, replace null lists with empty lists, drop null presets, ensure `Items` and `CivilianItems` are non-null, and log counts when debug is enabled.

Confidence: MEDIUM.

## INFO

### Campaign event idempotence is only safe because the event dispatcher is per-campaign

Severity: INFO

File(s)+line(s): `EquipmentPresetCampaignBehavior.cs:27-31`; `EquipPresetsIoC.cs:16`

Vanilla API evidence:

```csharp
// MbEvent<T>.AddNonSerializedListener, TaleWorlds.CampaignSystem.dll
public void AddNonSerializedListener(object owner, Action<T> action)
{
    EventHandlerRec<T> eventHandlerRec = new EventHandlerRec<T>(owner, action);
    EventHandlerRec<T> nonSerializedListenerList = _nonSerializedListenerList;
    _nonSerializedListenerList = eventHandlerRec;
    eventHandlerRec.Next = nonSerializedListenerList;
}

// CampaignEventDispatcher.Instance
public static CampaignEventDispatcher Instance => Campaign.Current?.CampaignEventDispatcher;
```

Why it matters: `AddNonSerializedListener` has no dedupe. Across separate campaigns this should not accumulate because `CampaignEventDispatcher.Instance` is tied to `Campaign.Current`, but registering the same singleton behavior twice in one campaign would duplicate callbacks. Current `SubModule.cs` does not register EquipPresets at all; when fixed, register it exactly once.

Recommended fix: no immediate code change if behavior is added once per campaign. Add a regression test or review checklist item for one unconditional `AddBehavior` call.

Confidence: MEDIUM.

### Z-order 1000 does not collide with current TAOM layers

Severity: INFO

File(s)+line(s): `Patch33_GauntletInventoryScreen.cs:31,61`; grep results: Career 1/50, CompanionTactics 100/200, FiefManagement 206

Vanilla API evidence:

```csharp
// GauntletInventoryScreen.OnInitialize, SandBox.GauntletUI.dll
_gauntletLayer = new GauntletLayer("InventoryScreen", 15, true)
{
    IsFocusLayer = true
};
```

Why it matters: no change needed for z-order itself. This finding is included to close the required collision audit.

Recommended fix: none for z-order.

Confidence: HIGH.

# Disputes With Claude's 5 Prior Fixes

| Prior fix | Dispute | Evidence | What to do instead |
|---|---|---|---|
| 1. Patch33 PatchCategory uncommented in `SubModule.cs` | DISPUTE. The current file does not patch Patch33. | `SubModule.cs:381-384` patches Patch27, Patch29, Patch34, then moves on; no `Patch33_EquipPresets`. Search excluding `bin/obj` finds Patch33 only on the patch classes. | Add `_harmony.PatchCategory("Patch33_EquipPresets");` in `OnGameInitializationFinished` after view assembly initialization. |
| 2. IoC.Resolve cached via static `??=` in `Patch33_SPInventoryVMRefresh` | Not disputed, but incomplete in context. | `Patch33_SPInventoryVMRefresh.cs:29-45` does cache adapter/logger. However `IoC.cs:52-80` never registers EquipPresets, so the cache cannot resolve in the current source. | Keep cache, but wire `EquipPresetsIoC`. Prefer caching `IInventoryScreenAdapter` instead of concrete-casting to `InventoryScreenAdapter`. |
| 3. `IInventoryScreenAdapter.Clear()` exposed | Not disputed. | `IInventoryScreenAdapter.cs:36-39`; `InventoryScreenAdapter.cs:28-29`; `Patch33_GauntletInventoryScreen.cs:85-88`. | No additional fix beyond wiring the feature. |
| 4. Dead `SlotApplyOutcome.SlotLocked` + `SkippedLockedSlots` removed | PARTIAL DISPUTE. The enum/result fields are gone, but the dead lock API remains. | `Outcomes.cs:31-38` has no `SlotLocked`; `PresetLoadResult.cs:28-35` has no skipped list. But `IInventoryScreenAdapter.cs:25-30` and `InventoryScreenAdapter.cs:63-83` still expose unused `SetItemLocked`. | Delete `SetItemLocked` until lock-aware load is rebuilt. |
| 5. Modifier double-count race-path documented | Not disputed for double-counting. | `EquipmentPresetService.cs:194-215` validates once, passes null on pre-validation failure, and only adds the race-path modifier once if the adapter later returns `ModifierMissing`. | Keep. Fix the larger InventoryLogic bypass separately. |

# Verification Log

All decompilation was done with `ilspycmd` 9.1.0 against installed v1.3.15 DLLs.

| Type/method | DLL path | Confirmation relied on |
|---|---|---|
| `TaleWorlds.Core.Equipment.this[int]` and `this[EquipmentIndex]` | `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.Core.dll` | Slot length is 12; setter assigns full `EquipmentElement` to `_itemSlots`; `IsItemFitsToSlot` return is ignored. |
| `TaleWorlds.Core.EquipmentIndex` | same | Horse = 10, HorseHarness = 11, NumEquipmentSetSlots = 12. |
| `TaleWorlds.Core.EquipmentElement` ctor | same | `EquipmentElement(ItemObject item, ItemModifier itemModifier = null, ItemObject cosmeticItem = null, bool isQuestItem = false)` preserves `ItemModifier`. |
| `TaleWorlds.Localization.TextObject.SetTextVariable` | `.../TaleWorlds.Localization.dll` | string and int overloads return `this`. |
| `SandBox.GauntletUI.GauntletInventoryScreen.OnInitialize/OnFinalize` | `.../Modules/SandBox/bin/Win64_Shipping_Client/SandBox.GauntletUI.dll` | `OnInitialize` is `protected unsafe override`; vanilla layer is z-order 15; `OnFinalize` is `protected override`. |
| `TaleWorlds.Engine.GauntletUI.GauntletLayer` ctor | `.../TaleWorlds.Engine.GauntletUI.dll` | Public `GauntletLayer(string name, int localOrder, bool shouldClear = false)`. |
| `SPInventoryVM.RefreshValues` | `.../TaleWorlds.CampaignSystem.ViewModelCollection.dll` | Public override; refreshes text/slot VMs but does not rebuild equipment from direct mutations. |
| `SPInventoryVM._currentCharacter`, `_inventoryLogic`, `EquipmentMode`, `EquipmentModes` | same | Private fields exist; `EquipmentMode` maps 0 Civilian, 1 Battle, 2 Stealth. |
| `SPInventoryVM.EquipEquipment`, `UnequipEquipment`, `AfterTransfer`, `IsItemEquipmentPossible` | same | Vanilla equips through `TransferCommand` and `InventoryLogic`; validates skill, usability, slot fit, mount-harness compatibility; `AfterTransfer` updates UI and pair rules. |
| `SPItemVM.IsLocked` | same | Public datasource property with setter. |
| `TaleWorlds.CampaignSystem.Inventory.InventoryLogic.AddTransferCommand(s)`, `TransferItem`, `DoesTransferItemExist` | `.../TaleWorlds.CampaignSystem.dll` | Transfer path checks source inventory/equipment existence, moves counts, deposits replaced equipment, and fires `AfterTransfer`. |
| `TaleWorlds.CampaignSystem.Inventory.TransferCommand.Transfer` | same | Static factory accepts amount, from/to sides, `ItemRosterElement`, from/to equipment index, and `CharacterObject`. |
| `TaleWorlds.CampaignSystem.Roster.ItemRoster.AddToCounts` | same | `AddToCounts(EquipmentElement,int)` preserves modifier; bare `ItemObject` overload wraps `new EquipmentElement(item)`. |
| `TaleWorlds.CampaignSystem.Hero.BattleEquipment/CivilianEquipment/AllAliveHeroes/IsActive` | same | Battle falls back to `DeadBattleEquipment`; civilian falls back to `DeadCivilianEquipment`; `AllAliveHeroes => Campaign.Current.AliveHeroes`; `IsActive => HeroState == Active`. |
| `TaleWorlds.CampaignSystem.Campaign.AliveHeroes`, dead equipment properties | same | `AliveHeroes => CampaignObjectManager.AliveHeroes`; dead battle/civilian equipment are separate properties. |
| `TaleWorlds.CampaignSystem.CharacterObject.HeroObject/IsHero` | same | `IsHero => _heroObject != null`. |
| `TaleWorlds.SaveSystem.SaveableTypeDefiner.AddClassDefinition/ConstructContainerDefinition` | `.../TaleWorlds.SaveSystem.dll` | `AddClassDefinition(Type,int,IObjectResolver)` and `ConstructContainerDefinition(Type)` are protected. |
| `TaleWorlds.SaveSystem.SaveablePropertyAttribute(short)` | same | Constructor parameter is `short`; EquipPresets ids 1..6 and 1..3 are safe. |
| `TaleWorlds.CampaignSystem.MbEvent<T>.AddNonSerializedListener` | `.../TaleWorlds.CampaignSystem.dll` | No dedupe; prepends handler to nonserialized list. |
| `TaleWorlds.CampaignSystem.CampaignEventDispatcher.Instance` | same | Dispatcher is read from `Campaign.Current?.CampaignEventDispatcher`. |
| `TaleWorlds.CampaignSystem.CampaignBehaviors.CampaignBehaviorManager.AddBehavior/ClearBehaviors` | same | `AddBehavior` immediately calls `RegisterEvents`; `ClearBehaviors` only clears list. |

Key installed-DLL snippets:

```csharp
// TaleWorlds.Core.Equipment
public const int EquipmentSlotLength = 12;
public EquipmentElement this[int index]
{
    get { return _itemSlots[index]; }
    set
    {
        IsItemFitsToSlot((EquipmentIndex)index, value.Item);
        _itemSlots[index] = value;
    }
}
public EquipmentElement this[EquipmentIndex index]
{
    get { return _itemSlots[(int)index]; }
    set { this[(int)index] = value; }
}
```

```csharp
// TaleWorlds.Core.EquipmentElement
public EquipmentElement(ItemObject item, ItemModifier itemModifier = null,
    ItemObject cosmeticItem = null, bool isQuestItem = false)
{
    Item = item;
    ItemModifier = itemModifier;
    CosmeticItem = cosmeticItem;
    IsQuestItem = isQuestItem;
}
```

```csharp
// TaleWorlds.Localization.TextObject
public TextObject SetTextVariable(string tag, string variable)
{
    SetTextVariableFromObject(tag, variable);
    return this;
}
public TextObject SetTextVariable(string tag, int variable)
{
    SetTextVariableFromObject(tag, variable);
    return this;
}
```

```csharp
// SandBox.GauntletUI.GauntletInventoryScreen
protected unsafe override void OnInitialize()
{
    ((ScreenBase)this).OnInitialize();
    ...
    _gauntletLayer = new GauntletLayer("InventoryScreen", 15, true)
    {
        IsFocusLayer = true
    };
    ((ScreenBase)this).AddLayer((ScreenLayer)(object)_gauntletLayer);
    _gauntletMovie = _gauntletLayer.LoadMovie("Inventory", (ViewModel)(object)_dataSource);
}

protected override void OnFinalize()
{
    ((ScreenBase)this).OnFinalize();
    _gauntletMovie = null;
    _inventoryCategory.Unload();
    ((ViewModel)_dataSource).OnFinalize();
    _dataSource = null;
    _gauntletLayer = null;
}
```

```csharp
// SPInventoryVM fields/properties
public enum EquipmentModes { Civilian, Battle, Stealth }
private InventoryLogic _inventoryLogic;
private CharacterObject _currentCharacter;
public int EquipmentMode { get { return (int)_equipmentMode; } ... }
public override void RefreshValues()
{
    base.RefreshValues();
    ...
    CharacterHelmSlot.RefreshValues();
    ...
    PlayerInventorySortController?.RefreshValues();
}
```

```csharp
// Hero equipment fallback
public Equipment BattleEquipment => _battleEquipment ?? Campaign.Current.DeadBattleEquipment;
public Equipment CivilianEquipment => _civilianEquipment ?? Campaign.Current.DeadCivilianEquipment;
public static MBReadOnlyList<Hero> AllAliveHeroes => Campaign.Current.AliveHeroes;
```

# Open Questions / UNVERIFIED Claims

- I could not locate `C:\Users\mikew\.codex\memories\feedback_codex_caught_api_misread.md`; I reverified the `Hero.BattleEquipment` / `CivilianEquipment` fallback chain directly from the installed DLL instead.
- I could not complete a local build/test rerun because the sandbox blocked SDK access to `C:\Users\mikew\AppData\Local\Microsoft SDKs` before compilation. The prompt's "build green, 1623/1623" status is therefore accepted as external context, but it conflicts with the current source-level missing `TaomSettings` properties.
- I did not verify SaveSystem forward-compat decode behavior with an actual binary save. Attribute ids and container definitions were verified from installed DLLs and source only.

