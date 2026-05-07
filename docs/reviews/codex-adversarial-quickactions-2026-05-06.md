# Codex Adversarial Review: QuickActions

## Top-Line Summary

CRITICAL: 0 | HIGH: 2 | MEDIUM: 1 | LOW: 0 | INFO: 1

VERDICT: ISSUES FOUND

Verified-clean areas: 5 of 6 Known Suspects disputed as user-visible bugs; 7 of 7 scenarios walked; all requested vanilla methods/properties decompiled from v1.3.15 DLLs via `ilspycmd`; config cross-reference skipped because QuickActions has no XML/JSON config.

Review scope: `Main/Features/QuickActions/**/*.cs`, QuickActions adapters, `TaomSettings.cs`, `IoC.cs`, `SubModule.cs`, and `TAOM.Tests/Features/QuickActions/*.cs`.

Tests/build: not run. This was a review-only task and no QuickActions source files were modified.

## Findings

### Finding 1

SEVERITY: HIGH

LOCATION: `Main/Features/QuickActions/Hooks/Patch34_SellAllItemsMenu.cs:37`

CLAIM: The menu option labeled `Sell All (Vanilla)` does not execute vanilla sell-all behavior. It hand-rolls a per-row `SPItemVM.ProcessSellItem(item, true)` loop and therefore drops vanilla `TransferAll(false)` logic for settlement gold affordability, capacity budgeting, warehouse handling, sorted transfer order, full-stack transfer amounts, and zero-count cleanup.

EVIDENCE:

TAOM snippet:

```csharp
// Main/Features/QuickActions/Hooks/Patch34_SellAllItemsMenu.cs:37-55
var del = SPItemVM.ProcessSellItem;
if (del == null) return;
var list = __instance.RightItemListVM;
if (list == null) return;
var snapshot = new System.Collections.Generic.List<SPItemVM>(list);
foreach (var item in snapshot)
{
    if (item == null) continue;
    if (item.IsFiltered) continue;
    if (item.IsLocked) continue;
    if (!item.IsTransferable) continue;
    del.Invoke(item, true);
}
```

Vanilla v1.3.15 evidence:

```csharp
// SPInventoryVM.cs:4890-4893
public void ExecuteSellAllItems()
{
    TransferAll(isBuy: false);
}

// SPInventoryVM.cs:4733-4754
private void TransferAll(bool isBuy)
{
    IsRefreshed = false;
    List<TransferCommand> list = new List<TransferCommand>(LeftItemListVM.Count);
    MBBindingList<SPItemVM> mBBindingList = new MBBindingList<SPItemVM>();
    foreach (SPItemVM item3 in isBuy ? LeftItemListVM : RightItemListVM)
    {
        if (item3 != null && !item3.IsFiltered && item3 != null && !item3.IsLocked && item3 != null && item3.IsTransferable)
        {
            mBBindingList.Add(item3);
        }
    }
    MobileParty mobileParty = (isBuy ? MobileParty.MainParty : _inventoryLogic.OtherParty?.MobileParty);
    bool flag = _inventoryLogic.OtherParty?.IsSettlement ?? false;
    InventoryCapacityModel inventoryCapacityModel = Campaign.Current.Models.InventoryCapacityModel;
    mBBindingList.Sort(new RosterElementComparer(inventoryCapacityModel, mobileParty, flag));
    InventoryLogic.InventorySide fromSide = ((!isBuy) ? InventoryLogic.InventorySide.PlayerInventory : InventoryLogic.InventorySide.OtherInventory);
    InventoryLogic.InventorySide inventorySide = (isBuy ? InventoryLogic.InventorySide.PlayerInventory : InventoryLogic.InventorySide.OtherInventory);
    if (flag && !isBuy)
    {
        TransferAllForSettlement(mBBindingList, fromSide, inventorySide, list);
    }
```

```csharp
// SPInventoryVM.cs:4827-4868
private void TransferAllForSettlement(MBBindingList<SPItemVM> list, InventoryLogic.InventorySide fromSide, InventoryLogic.InventorySide toSide, List<TransferCommand> commands)
{
    float num = LeftInventoryOwnerGold;
    float num2 = float.MaxValue;
    float num3 = 0f;
    foreach (SPItemVM item2 in list)
    {
        int itemCost = item2.ItemCost;
        if ((float)itemCost < num2)
        {
            num2 = itemCost;
        }
    }
    bool flag = num < num2;
    int num4 = list.Count - 1;
    while (0 <= num4)
    {
        SPItemVM sPItemVM = list[num4];
        int amount = sPItemVM.ItemRosterElement.Amount;
        if (!flag)
        {
            for (int i = 0; i < amount; i++)
            {
                float num5 = sPItemVM.ItemCost;
                num3 += num5;
                if (num3 < num)
                {
                    _inventoryLogic.AddTransferCommands(new List<TransferCommand> { TransferCommand.Transfer(1, fromSide, toSide, sPItemVM.ItemRosterElement, EquipmentIndex.None, EquipmentIndex.None, _currentCharacter) });
                    continue;
                }
                num3 -= num5;
                break;
            }
        }
        else
        {
            TransferCommand item = TransferCommand.Transfer(amount, fromSide, toSide, sPItemVM.ItemRosterElement, EquipmentIndex.None, EquipmentIndex.None, _currentCharacter);
            commands.Add(item);
        }
        num4--;
    }
}
```

`InventoryLogic.AddTransferCommand` does not re-apply the omitted settlement/capacity budget; it immediately processes the command:

```csharp
// InventoryLogic.cs:834-845
public void AddTransferCommand(TransferCommand command)
{
    ProcessTransferCommand(command);
}

public void AddTransferCommands(IEnumerable<TransferCommand> commands)
{
    foreach (TransferCommand command in commands)
    {
        ProcessTransferCommand(command);
    }
}
```

FIX: Do not hand-roll the `Sell All (Vanilla)` option. Use a Harmony-safe bypass such as a thread-static `BypassQuickActions` flag where the menu action calls `__instance.ExecuteSellAllItems()` and the Prefix returns `true` when the flag is set, or use a verified ReversePatch/original-call pattern. The option named vanilla must reach `SPInventoryVM.TransferAll(false)`.

### Finding 2

SEVERITY: HIGH

LOCATION: `Main/Adapters/InventoryVMAdapter.cs:75`

CLAIM: QuickActions custom sells (`Sell Damaged`, `Sell Low Value`) sell only one unit from each matching stack and under-report sold count/gold for stacked inventory rows. The adapter invokes `SPItemVM.ProcessSellItem(spItem, true)`, and vanilla uses the row's `TransactionCount` for `cameFromTradeData=true`; `SPItemVM` initializes `TransactionCount = 1`.

EVIDENCE:

TAOM snippets:

```csharp
// Main/Adapters/InventoryVMAdapter.cs:58-76
public bool TrySellItem(IInventoryItemAdapter item)
{
    if (_active == null) return false;
    if (item?.UnderlyingVm is not SPItemVM spItem) return false;
    var del = SPItemVM.ProcessSellItem;
    ...
    del.Invoke(spItem, true);
    return true;
}
```

```csharp
// Main/Features/QuickActions/QuickActionsService.cs:158-165
foreach (var item in items)
{
    if (!IsDamagedSellTarget(item, threshold)) continue;
    if (_inventory.TrySellItem(item))
    {
        sold++;
        gold += item.ItemValue;
    }
}
```

Vanilla v1.3.15 evidence:

```csharp
// SPItemVM.cs:465-468
ItemRosterElement = new ItemRosterElement(newItem.EquipmentElement, newItem.Amount);
base.ItemCost = itemCost;
ItemCount = newItem.Amount;
TransactionCount = 1;
```

```csharp
// SPInventoryVM.cs:3534-3555
private void ProcessSellItem(SPItemVM item, bool cameFromTradeData)
{
    if (!item.IsTransferable)
    {
        return;
    }
    if (InventoryLogic.IsEquipmentSide(item.InventorySide))
    {
        TransactionCount = 1;
    }
    else if (IsEntireStackModifierActive && !cameFromTradeData)
    {
        TransactionCount = _inventoryLogic.FindItemFromSide(InventoryLogic.InventorySide.PlayerInventory, item.ItemRosterElement.EquipmentElement)?.Amount ?? 0;
    }
    else if (IsFiveStackModifierActive && !cameFromTradeData)
    {
        TransactionCount = 5;
    }
    else
    {
        TransactionCount = item.TransactionCount;
    }
```

```csharp
// SPInventoryVM.cs:4666-4680
private void SellItem(SPItemVM item)
{
    InventoryLogic.InventorySide inventorySide = item.InventorySide;
    int b = item.ItemCount;
    ...
    TransferCommand command = TransferCommand.Transfer(TaleWorlds.Library.MathF.Min(TransactionCount, b), inventorySide, InventoryLogic.InventorySide.OtherInventory, item.ItemRosterElement, item.ItemType, TargetEquipmentType, _currentCharacter);
    _inventoryLogic.AddTransferCommand(command);
}
```

Vanilla full-stack sell-all uses `ItemRosterElement.Amount`, not one default transaction:

```csharp
// SPInventoryVM.cs:4800-4816
SPItemVM sPItemVM2 = mBBindingList[num4];
int num5 = sPItemVM2.ItemRosterElement.Amount;
...
if (num5 > 0)
{
    TransferCommand item2 = TransferCommand.Transfer(num5, fromSide, inventorySide, sPItemVM2.ItemRosterElement, EquipmentIndex.None, EquipmentIndex.None, _currentCharacter);
    list.Add(item2);
}
```

The tests do not catch this because they mock `IInventoryVMAdapter.TrySellItem` as a boolean and do not expose stack amount (`TAOM.Tests/Features/QuickActions/QuickActionsServiceTests.cs:49`).

FIX: Extend `IInventoryItemAdapter` with stack amount and sell amount support, then sell the full matching amount. A minimal VM-based fix is to set the `SPItemVM.TransactionCount` to the desired stack amount before invoking with `cameFromTradeData:true`; a stronger fix is to route custom bulk actions through `InventoryLogic.TransferCommand` with the same constraints vanilla uses for settlement/capacity behavior.

### Finding 3

SEVERITY: MEDIUM

LOCATION: `Main/Features/QuickActions/QuickActionsService.cs:215`

CLAIM: `UnequipAll` mutates hero equipment and `PartyBase.MainParty.ItemRoster` directly, then calls `SPInventoryVM.RefreshValues()`. Vanilla `RefreshValues()` does not rebuild item rows or equipment slot VMs from the underlying rosters/equipment. The active inventory UI can remain stale after `Unequip All`, especially when a stripped item did not already have a right-pane row.

EVIDENCE:

TAOM snippets:

```csharp
// Main/Adapters/PlayerEquipmentAdapter.cs:39-43
var element = equipment[i];
if (element.IsEmpty) continue;
roster.AddToCounts(element, 1);
equipment[i] = EquipmentElement.Invalid;
```

```csharp
// Main/Features/QuickActions/QuickActionsService.cs:214-216
// Refresh so the inventory display picks up the items deposited into the roster.
try { _inventory.RefreshDisplay(); }
catch (Exception ex) { _logger.LogError($"[QuickActions] post-unequip refresh failed: {ex.Message}"); }
```

```csharp
// Main/Adapters/InventoryVMAdapter.cs:85-91
public void RefreshDisplay()
{
    if (_active == null) return;
    try
    {
        _active.RefreshValues();
    }
```

Vanilla v1.3.15 evidence:

`RefreshValues()` only refreshes labels, hints, slot VM text, and sort controllers. It does not clear/repopulate `RightItemListVM` or call `InitializeInventory()`:

```csharp
// SPInventoryVM.cs:3182-3265
public override void RefreshValues()
{
    base.RefreshValues();
    RightInventoryOwnerName = PartyBase.MainParty.Name.ToString();
    ...
    CharacterHelmSlot.RefreshValues();
    CharacterCloakSlot.RefreshValues();
    CharacterTorsoSlot.RefreshValues();
    CharacterGloveSlot.RefreshValues();
    CharacterBootSlot.RefreshValues();
    CharacterMountSlot.RefreshValues();
    CharacterMountArmorSlot.RefreshValues();
    CharacterWeapon1Slot.RefreshValues();
    CharacterWeapon2Slot.RefreshValues();
    CharacterWeapon3Slot.RefreshValues();
    CharacterWeapon4Slot.RefreshValues();
    CharacterBannerSlot.RefreshValues();
    PlayerInventorySortController?.RefreshValues();
    OtherInventorySortController?.RefreshValues();
}
```

The method that rebuilds visible item rows is private `InitializeInventory()`:

```csharp
// SPInventoryVM.cs:4488-4517
RightItemListVM.Clear();
LeftItemListVM.Clear();
...
ItemRosterElement itemRosterElement = array[num2];
SPItemVM sPItemVM = new SPItemVM(_inventoryLogic, MainCharacter.IsFemale, CanCharacterUseItemBasedOnSkills(itemRosterElement), _usageType, itemRosterElement, InventoryLogic.InventorySide.PlayerInventory, _inventoryLogic.GetCostOfItemRosterElement(itemRosterElement, InventoryLogic.InventorySide.PlayerInventory), null);
UpdateFilteredStatusOfItem(sPItemVM);
sPItemVM.IsLocked = sPItemVM.InventorySide == InventoryLogic.InventorySide.PlayerInventory && IsItemLocked(itemRosterElement);
RightItemListVM.Add(sPItemVM);
...
RefreshInformationValues();
IsRefreshed = true;
```

Vanilla live UI updates happen through `InventoryLogic.AfterTransfer`, which direct `PartyBase.MainParty.ItemRoster.AddToCounts` bypasses:

```csharp
// SPInventoryVM.cs:3087
_inventoryLogic.AfterTransfer += AfterTransfer;

// SPInventoryVM.cs:3838-3893
private void AfterTransfer(InventoryLogic inventoryLogic, List<TransferCommandResult> results)
{
    ...
    if (transferCommandResult.ResultSide == InventoryLogic.InventorySide.OtherInventory || transferCommandResult.ResultSide == InventoryLogic.InventorySide.PlayerInventory)
    {
        MBBindingList<SPItemVM> mBBindingList = ((transferCommandResult.ResultSide == InventoryLogic.InventorySide.OtherInventory) ? LeftItemListVM : RightItemListVM);
        ...
        if (flag || transferCommandResult.EffectedNumber <= 0 || _inventoryLogic == null)
        {
            continue;
        }
        ...
        mBBindingList.Add(newItem);
```

Equipment slot rows are also normally updated inside `AfterTransfer`:

```csharp
// SPInventoryVM.cs:3900-3909
else if (InventoryLogic.IsEquipmentSide(transferCommandResult.ResultSide))
{
    SPItemVM sPItemVM3 = null;
    if (transferCommandResult.FinalNumber > 0)
    {
        sPItemVM3 = new SPItemVM(...);
        sPItemVM3.IsNew = true;
    }
    UpdateEquipment(transferCommandResult.ResultSideEquipment, sPItemVM3, transferCommandResult.EffectedEquipmentIndex);
    _isCharacterEquipmentDirty = true;
}
```

FIX: Do not direct-mutate the campaign roster/equipment while an `SPInventoryVM` is active. Use `InventoryLogic.TransferCommand` from each equipment side to `PlayerInventory` so vanilla `AfterTransfer` updates both equipment slots and item rows, or add a dedicated, verified VM adapter method that forces the same rebuild path as `InitializeInventory()` plus equipment refresh.

### Observation 1

SEVERITY: INFO

LOCATION: `Main/Features/QuickActions/Hooks/InventorySearchCampaignBehavior.cs:62`

CLAIM: The comment says `CampaignEvents.TickEvent` fires hourly in-game, but v1.3.15 dispatches it from `CampaignEvents.Tick(float dt)`. This is not a behavior bug because the reconciliation is idempotent and cheap; it is a documentation mismatch for future maintainers.

EVIDENCE:

TAOM snippet:

```csharp
// Main/Features/QuickActions/Hooks/InventorySearchCampaignBehavior.cs:61-63
// Idempotent reconciliation: if the user toggled MCM mid-campaign, keep the per-save
// bool aligned. CampaignEvents.TickEvent fires hourly in-game; we don't need sub-second
// sync because the patch applies on inventory-open, not on tick.
```

Vanilla v1.3.15 evidence:

```csharp
// CampaignEvents.cs:2048-2050
public override void Tick(float dt)
{
    Instance._tickEvent.Invoke(dt);
}
```

FIX: Update the comment to say TickEvent fires on campaign tick/frame cadence, and that the handler is intentionally idempotent.

## Vanilla Code

All decompilations below were produced from v1.3.15 DLLs under `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/` using `ilspycmd -t <type>`. No required method failed to decompile.

### `SPInventoryVM.ExecuteSellAllItems`

DLL: `TaleWorlds.CampaignSystem.ViewModelCollection.dll`

```csharp
// SPInventoryVM.cs:4890-4893
public void ExecuteSellAllItems()
{
    TransferAll(isBuy: false);
}
```

### `SPInventoryVM.TransferAll(bool isBuy)`

DLL: `TaleWorlds.CampaignSystem.ViewModelCollection.dll`

```csharp
// SPInventoryVM.cs:4733-4825
private void TransferAll(bool isBuy)
{
    IsRefreshed = false;
    List<TransferCommand> list = new List<TransferCommand>(LeftItemListVM.Count);
    MBBindingList<SPItemVM> mBBindingList = new MBBindingList<SPItemVM>();
    foreach (SPItemVM item3 in isBuy ? LeftItemListVM : RightItemListVM)
    {
        if (item3 != null && !item3.IsFiltered && item3 != null && !item3.IsLocked && item3 != null && item3.IsTransferable)
        {
            mBBindingList.Add(item3);
        }
    }
    MobileParty mobileParty = (isBuy ? MobileParty.MainParty : _inventoryLogic.OtherParty?.MobileParty);
    bool flag = _inventoryLogic.OtherParty?.IsSettlement ?? false;
    InventoryCapacityModel inventoryCapacityModel = Campaign.Current.Models.InventoryCapacityModel;
    mBBindingList.Sort(new RosterElementComparer(inventoryCapacityModel, mobileParty, flag));
    InventoryLogic.InventorySide fromSide = ((!isBuy) ? InventoryLogic.InventorySide.PlayerInventory : InventoryLogic.InventorySide.OtherInventory);
    InventoryLogic.InventorySide inventorySide = (isBuy ? InventoryLogic.InventorySide.PlayerInventory : InventoryLogic.InventorySide.OtherInventory);
    if (flag && !isBuy)
    {
        TransferAllForSettlement(mBBindingList, fromSide, inventorySide, list);
    }
    else
    {
        bool flag2 = (InventoryScreenHelper.GetActiveInventoryState()?.InventoryMode ?? InventoryScreenHelper.InventoryMode.Default) == InventoryScreenHelper.InventoryMode.Warehouse;
        float num = 0f;
        float num2 = 0f;
        if (mBBindingList.Count > 0)
        {
            if (mobileParty != null)
            {
                num2 = inventoryCapacityModel.GetItemEffectiveWeight(mBBindingList[0].ItemRosterElement.EquipmentElement, mobileParty, mobileParty.IsCurrentlyAtSea, out var _);
            }
            else if (flag2)
            {
                num2 = mBBindingList[0].ItemRosterElement.EquipmentElement.GetEquipmentElementWeight();
            }
        }
        float capacityBudget = GetCapacityBudget(mobileParty, isBuy);
        bool flag3 = capacityBudget < num2;
        bool flag4 = _inventoryLogic.CanInventoryCapacityIncrease(inventorySide);
        if (!flag3 && flag4)
        {
            List<TransferCommand> list2 = new List<TransferCommand>(0);
            int num3;
            for (num3 = 0; num3 < mBBindingList.Count; num3++)
            {
                SPItemVM sPItemVM = mBBindingList[num3];
                if (!_inventoryLogic.GetCanItemIncreaseInventoryCapacity(sPItemVM.ItemRosterElement.EquipmentElement.Item))
                {
                    break;
                }
                TransferCommand item = TransferCommand.Transfer(sPItemVM.ItemRosterElement.Amount, fromSide, inventorySide, sPItemVM.ItemRosterElement, EquipmentIndex.None, EquipmentIndex.None, _currentCharacter);
                list2.Add(item);
                mBBindingList.Remove(sPItemVM);
                num3--;
            }
            if (list2.Count > 0)
            {
                _inventoryLogic.AddTransferCommands(list2);
                list2.Clear();
                capacityBudget = GetCapacityBudget(mobileParty, isBuy);
            }
        }
        int num4 = mBBindingList.Count - 1;
        while (0 <= num4)
        {
            SPItemVM sPItemVM2 = mBBindingList[num4];
            int num5 = sPItemVM2.ItemRosterElement.Amount;
            if (!flag3)
            {
                TextObject description2;
                float num6 = (flag2 ? sPItemVM2.ItemRosterElement.EquipmentElement.GetEquipmentElementWeight() : inventoryCapacityModel.GetItemEffectiveWeight(sPItemVM2.ItemRosterElement.EquipmentElement, mobileParty, mobileParty.IsCurrentlyAtSea, out description2));
                float num7 = num + num6 * (float)num5;
                if (num5 > 0 && num7 > capacityBudget)
                {
                    num5 = MBMath.ClampInt(num5, 0, TaleWorlds.Library.MathF.Floor((capacityBudget - num) / num6));
                }
                num += (float)num5 * num6;
            }
            if (num5 > 0)
            {
                TransferCommand item2 = TransferCommand.Transfer(num5, fromSide, inventorySide, sPItemVM2.ItemRosterElement, EquipmentIndex.None, EquipmentIndex.None, _currentCharacter);
                list.Add(item2);
            }
            num4--;
        }
    }
    _inventoryLogic.AddTransferCommands(list);
    RefreshInformationValues();
    ExecuteRemoveZeroCounts();
    IsRefreshed = true;
}
```

### `SPInventoryVM.TransferAllForSettlement`

DLL: `TaleWorlds.CampaignSystem.ViewModelCollection.dll`

```csharp
// SPInventoryVM.cs:4827-4868
private void TransferAllForSettlement(MBBindingList<SPItemVM> list, InventoryLogic.InventorySide fromSide, InventoryLogic.InventorySide toSide, List<TransferCommand> commands)
{
    float num = LeftInventoryOwnerGold;
    float num2 = float.MaxValue;
    float num3 = 0f;
    foreach (SPItemVM item2 in list)
    {
        int itemCost = item2.ItemCost;
        if ((float)itemCost < num2)
        {
            num2 = itemCost;
        }
    }
    bool flag = num < num2;
    int num4 = list.Count - 1;
    while (0 <= num4)
    {
        SPItemVM sPItemVM = list[num4];
        int amount = sPItemVM.ItemRosterElement.Amount;
        if (!flag)
        {
            for (int i = 0; i < amount; i++)
            {
                float num5 = sPItemVM.ItemCost;
                num3 += num5;
                if (num3 < num)
                {
                    _inventoryLogic.AddTransferCommands(new List<TransferCommand> { TransferCommand.Transfer(1, fromSide, toSide, sPItemVM.ItemRosterElement, EquipmentIndex.None, EquipmentIndex.None, _currentCharacter) });
                    continue;
                }
                num3 -= num5;
                break;
            }
        }
        else
        {
            TransferCommand item = TransferCommand.Transfer(amount, fromSide, toSide, sPItemVM.ItemRosterElement, EquipmentIndex.None, EquipmentIndex.None, _currentCharacter);
            commands.Add(item);
        }
        num4--;
    }
}
```

### `SPInventoryVM.RefreshCallbacks()`

DLL: `TaleWorlds.CampaignSystem.ViewModelCollection.dll`

```csharp
// SPInventoryVM.cs:3306-3319
public void RefreshCallbacks()
{
    ItemVM.ProcessEquipItem = ProcessEquipItem;
    ItemVM.ProcessUnequipItem = ProcessUnequipItem;
    ItemVM.ProcessPreviewItem = ProcessPreviewItem;
    ItemVM.ProcessBuyItem = ProcessBuyItem;
    SPItemVM.ProcessLockItem = ProcessLockItem;
    SPItemVM.ProcessSellItem = ProcessSellItem;
    ItemVM.ProcessItemSelect = ProcessItemSelect;
    ItemVM.ProcessItemTooltip = ProcessItemTooltip;
    SPItemVM.ProcessItemSlaughter = ProcessItemSlaughter;
    SPItemVM.ProcessItemDonate = ProcessItemDonate;
    SPItemVM.OnFocus = OnItemFocus;
}
```

Lifecycle evidence: `RefreshCallbacks()` is called in the constructor before `InitializeInventory()` and again from `GauntletInventoryScreen.OnActivate()` after `LoadMovie`. The prompt's "exactly once per inventory open after binding" premise is false, but the attach point still works because the second call re-applies after binding.

```csharp
// SPInventoryVM.cs:3114-3124
RefreshCallbacks();
...
if (_inventoryLogic != null)
{
    UpdateRightCharacter();
    UpdateLeftCharacter();
    InitializeInventory();
}
```

```csharp
// GauntletInventoryScreen.cs:145, 163-170
_gauntletMovie = _gauntletLayer.LoadMovie("Inventory", (ViewModel)(object)_dataSource);

protected override void OnActivate()
{
    ((ScreenBase)this).OnActivate();
    SPInventoryVM dataSource = _dataSource;
    if (dataSource != null)
    {
        dataSource.RefreshCallbacks();
    }
}
```

### `SPInventoryVM.OnFinalize()`

DLL: `TaleWorlds.CampaignSystem.ViewModelCollection.dll`

```csharp
// SPInventoryVM.cs:3267-3304
public override void OnFinalize()
{
    ItemVM.ProcessEquipItem = null;
    ItemVM.ProcessUnequipItem = null;
    ItemVM.ProcessPreviewItem = null;
    ItemVM.ProcessBuyItem = null;
    SPItemVM.ProcessSellItem = null;
    ItemVM.ProcessItemSelect = null;
    ItemVM.ProcessItemTooltip = null;
    SPItemVM.ProcessItemSlaughter = null;
    SPItemVM.ProcessItemDonate = null;
    SPItemVM.OnFocus = null;
    InventoryTradeVM.RemoveZeroCounts -= ExecuteRemoveZeroCounts;
    Game.Current.EventManager.UnregisterEvent<TutorialNotificationElementChangeEvent>(OnTutorialNotificationElementIDChange);
    ItemPreview.OnFinalize();
    ItemPreview = null;
    CancelInputKey.OnFinalize();
    DoneInputKey.OnFinalize();
    ResetInputKey.OnFinalize();
    PreviousCharacterInputKey.OnFinalize();
    NextCharacterInputKey.OnFinalize();
    BuyAllInputKey.OnFinalize();
    SellAllInputKey.OnFinalize();
    ItemVM.ProcessEquipItem = null;
    ItemVM.ProcessUnequipItem = null;
    ItemVM.ProcessPreviewItem = null;
    ItemVM.ProcessBuyItem = null;
    SPItemVM.ProcessLockItem = null;
    SPItemVM.ProcessSellItem = null;
    ItemVM.ProcessItemSelect = null;
    ItemVM.ProcessItemTooltip = null;
    SPItemVM.ProcessItemSlaughter = null;
    SPItemVM.ProcessItemDonate = null;
    SPItemVM.OnFocus = null;
    MainCharacter.OnFinalize();
    _inventoryLogic = null;
    base.OnFinalize();
}
```

Inventory screen close calls this VM method:

```csharp
// GauntletInventoryScreen.cs:178-184
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

### `SPInventoryVM` 3-arg constructor

DLL: `TaleWorlds.CampaignSystem.ViewModelCollection.dll`

```csharp
// SPInventoryVM.cs:3060-3164
public SPInventoryVM(InventoryLogic inventoryLogic, bool isInCivilianModeByDefault, Func<WeaponComponentData, ItemObject.ItemUsageSetFlags> getItemUsageSetFlags)
{
    IsSearchAvailable = true;
    _usageType = InventoryScreenHelper.GetActiveInventoryState()?.InventoryMode ?? InventoryScreenHelper.InventoryMode.Default;
    _inventoryLogic = inventoryLogic;
    _viewDataTracker = Campaign.Current.GetCampaignBehavior<IViewDataTracker>();
    _getItemUsageSetFlags = getItemUsageSetFlags;
    _filters = new Dictionary<Filters, List<int>>();
    _filters.Add(Filters.All, _everyItemType);
    _filters.Add(Filters.Weapons, _weaponItemTypes);
    _filters.Add(Filters.Armors, _armorItemTypes);
    _filters.Add(Filters.Mounts, _mountItemTypes);
    _filters.Add(Filters.ShieldsAndRanged, _shieldAndRangedItemTypes);
    _filters.Add(Filters.Miscellaneous, _miscellaneousItemTypes);
    _equipAfterTransferStack = new Stack<SPItemVM>();
    _comparedItemList = new List<ItemVM>();
    _donationMaxShareableXp = MobilePartyHelper.GetMaximumXpAmountPartyCanGet(MobileParty.MainParty);
    MBTextManager.SetTextVariable("XP_DONATION_LIMIT", _donationMaxShareableXp);
    if (_inventoryLogic != null)
    {
        _currentCharacter = _inventoryLogic.InitialEquipmentCharacter;
        _isTrading = inventoryLogic.IsTrading;
        _inventoryLogic.AfterReset += AfterReset;
        InventoryLogic inventoryLogic2 = _inventoryLogic;
        inventoryLogic2.TotalAmountChange = (Action<int>)Delegate.Combine(inventoryLogic2.TotalAmountChange, new Action<int>(OnTotalAmountChange));
        InventoryLogic inventoryLogic3 = _inventoryLogic;
        inventoryLogic3.DonationXpChange = (Action)Delegate.Combine(inventoryLogic3.DonationXpChange, new Action(OnDonationXpChange));
        _inventoryLogic.AfterTransfer += AfterTransfer;
        _rightTroopRoster = inventoryLogic.RightMemberRoster;
        _leftTroopRoster = inventoryLogic.LeftMemberRoster;
        _currentInventoryCharacterIndex = _rightTroopRoster.FindIndexOfTroop(_currentCharacter);
        OnDonationXpChange();
        CompanionExists = DoesCompanionExist();
    }
    MainCharacter = new HeroViewModel();
    MainCharacter.FillFrom(_currentCharacter.HeroObject);
    ItemMenu = new ItemMenuVM(ResetComparedItems, _inventoryLogic, _getItemUsageSetFlags, GetItemFromIndex);
    IsRefreshed = false;
    RightItemListVM = new MBBindingList<SPItemVM>();
    LeftItemListVM = new MBBindingList<SPItemVM>();
    CharacterHelmSlot = new SPItemVM();
    CharacterCloakSlot = new SPItemVM();
    CharacterTorsoSlot = new SPItemVM();
    CharacterGloveSlot = new SPItemVM();
    CharacterBootSlot = new SPItemVM();
    CharacterMountSlot = new SPItemVM();
    CharacterMountArmorSlot = new SPItemVM();
    CharacterWeapon1Slot = new SPItemVM();
    CharacterWeapon2Slot = new SPItemVM();
    CharacterWeapon3Slot = new SPItemVM();
    CharacterWeapon4Slot = new SPItemVM();
    CharacterBannerSlot = new SPItemVM();
    ProductionTooltip = new BasicTooltipViewModel();
    CurrentCharacterSkillsTooltip = new BasicTooltipViewModel(() => CampaignUIHelper.GetInventoryCharacterTooltip(_currentCharacter.HeroObject));
    RefreshCallbacks();
    _selectedEquipmentIndex = 0;
    if (isInCivilianModeByDefault)
    {
        EquipmentMode = 0;
    }
    if (_inventoryLogic != null)
    {
        UpdateRightCharacter();
        UpdateLeftCharacter();
        InitializeInventory();
    }
    RightInventoryOwnerGold = Hero.MainHero.Gold;
    if (_inventoryLogic.OtherSideCapacityData != null)
    {
        OtherSideHasCapacity = _inventoryLogic.OtherSideCapacityData.GetCapacity() != -1;
    }
    IsOtherInventoryGoldRelevant = _usageType != InventoryScreenHelper.InventoryMode.Loot;
    PlayerInventorySortController = new SPInventorySortControllerVM(ref _rightItemListVM);
    OtherInventorySortController = new SPInventorySortControllerVM(ref _leftItemListVM);
    PlayerInventorySortController.SortByDefaultState();
    if (_usageType == InventoryScreenHelper.InventoryMode.Loot)
    {
        OtherInventorySortController.CostState = 1;
        OtherInventorySortController.ExecuteSortByCost();
    }
    else
    {
        OtherInventorySortController.SortByDefaultState();
    }
    Tuple<int, int> tuple = _viewDataTracker.InventoryGetSortPreference((int)_usageType);
    if (tuple != null)
    {
        PlayerInventorySortController.SortByOption((SPInventorySortControllerVM.InventoryItemSortOption)tuple.Item1, (SPInventorySortControllerVM.InventoryItemSortState)tuple.Item2);
    }
    ItemPreview = new ItemPreviewVM(OnPreviewClosed);
    _characterList = new SelectorVM<InventoryCharacterSelectorItemVM>(0, OnCharacterSelected);
    AddApplicableCharactersToListFromRoster(_rightTroopRoster.GetTroopRoster());
    if (_inventoryLogic.IsOtherPartyFromPlayerClan && _leftTroopRoster != null)
    {
        AddApplicableCharactersToListFromRoster(_leftTroopRoster.GetTroopRoster());
    }
    if (_characterList.SelectedIndex == -1 && _characterList.ItemList.Count > 0)
    {
        _characterList.SelectedIndex = 0;
    }
    BannerTypeName = ItemObject.ItemTypeEnum.Banner.ToString();
    InventoryTradeVM.RemoveZeroCounts += ExecuteRemoveZeroCounts;
    Game.Current.EventManager.RegisterEvent<TutorialNotificationElementChangeEvent>(OnTutorialNotificationElementIDChange);
    RefreshValues();
}
```

### `SPItemVM.ProcessSellItem`

DLL: `TaleWorlds.CampaignSystem.ViewModelCollection.dll`

`SPItemVM.ProcessSellItem` is a public static field, not a method:

```csharp
// SPItemVM.cs:27-31
public static Action<SPItemVM> OnFocus;

public static Action<SPItemVM, bool> ProcessSellItem;

public static Action<SPItemVM> ProcessItemSlaughter;
```

It is assigned by `SPInventoryVM.RefreshCallbacks()`:

```csharp
// SPInventoryVM.cs:3306-3314
public void RefreshCallbacks()
{
    ...
    SPItemVM.ProcessLockItem = ProcessLockItem;
    SPItemVM.ProcessSellItem = ProcessSellItem;
    ItemVM.ProcessItemSelect = ProcessItemSelect;
```

Relevant receiver implementation:

```csharp
// SPInventoryVM.cs:3534-3571
private void ProcessSellItem(SPItemVM item, bool cameFromTradeData)
{
    if (!item.IsTransferable)
    {
        return;
    }
    if (InventoryLogic.IsEquipmentSide(item.InventorySide))
    {
        TransactionCount = 1;
    }
    else if (IsEntireStackModifierActive && !cameFromTradeData)
    {
        TransactionCount = _inventoryLogic.FindItemFromSide(InventoryLogic.InventorySide.PlayerInventory, item.ItemRosterElement.EquipmentElement)?.Amount ?? 0;
    }
    else if (IsFiveStackModifierActive && !cameFromTradeData)
    {
        TransactionCount = 5;
    }
    else
    {
        TransactionCount = item.TransactionCount;
    }
    if (TransactionCount == 0)
    {
        Debug.FailedAssert("Transaction count should not be zero", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem.ViewModelCollection\\Inventory\\SPInventoryVM.cs", "ProcessSellItem", 690);
        return;
    }
    IsRefreshed = false;
    MBTextManager.SetTextVariable("ITEM_DESCRIPTION", item.ItemDescription);
    MBTextManager.SetTextVariable("ITEM_COST", item.ItemCost);
    SellItem(item);
    if (!cameFromTradeData)
    {
        ExecuteRemoveZeroCounts();
    }
    RefreshInformationValues();
    IsRefreshed = true;
}
```

### `ItemRoster.AddToCounts(EquipmentElement, int)`

DLL: `TaleWorlds.CampaignSystem.dll`

```csharp
// ItemRoster.cs:185-191
public int AddToCounts(ItemObject item, int number)
{
    if (number == 0)
    {
        return -1;
    }
    return AddToCounts(new EquipmentElement(item), number);
}

// ItemRoster.cs:194-220
public int AddToCounts(EquipmentElement rosterElement, int number)
{
    if (number == 0)
    {
        return -1;
    }
    int num = FindIndexOfElement(rosterElement);
    if (num < 0)
    {
        if (number < 0)
        {
            Debug.FailedAssert("Trying to delete an element from Item Roster that does not exist!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\ItemRoster.cs", "AddToCounts", 169);
            return -1;
        }
        num = AddNewElement(new ItemRosterElement(rosterElement, 0));
    }
    OnRosterUpdated(ref _data[num], number);
    _data[num].Amount += number;
    if (_data[num].Amount <= 0)
    {
        _data[num] = _data[_count - 1];
        _data[_count - 1] = ItemRosterElement.Invalid;
        _count--;
    }
    UpdateVersion();
    return num;
}
```

`EquipmentElement` carries the modifier:

```csharp
// EquipmentElement.cs:94-100
public EquipmentElement(ItemObject item, ItemModifier itemModifier = null, ItemObject cosmeticItem = null, bool isQuestItem = false)
{
    Item = item;
    ItemModifier = itemModifier;
    CosmeticItem = cosmeticItem;
    IsQuestItem = isQuestItem;
}
```

### `Equipment[EquipmentIndex] = EquipmentElement.Invalid`

DLL: `TaleWorlds.Core.dll`

```csharp
// EquipmentElement.cs:11, 24
public static readonly EquipmentElement Invalid = new EquipmentElement(null);
public bool IsEmpty => Item == null;
```

```csharp
// Equipment.cs:55-76
public EquipmentElement this[int index]
{
    get
    {
        return _itemSlots[index];
    }
    set
    {
        IsItemFitsToSlot((EquipmentIndex)index, value.Item);
        _itemSlots[index] = value;
    }
}

public EquipmentElement this[EquipmentIndex index]
{
    get
    {
        return _itemSlots[(int)index];
    }
    set
    {
        this[(int)index] = value;
    }
}
```

```csharp
// Equipment.cs:443-449
public static bool IsItemFitsToSlot(EquipmentIndex slotIndex, ItemObject item)
{
    bool result = false;
    if (item == null)
    {
        result = true;
    }
```

Conclusion: assigning `EquipmentElement.Invalid` fully clears the slot by storing an `EquipmentElement` whose `Item` is `null`; it is not a count-only operation.

### Additional Vanilla Evidence Used

`MultiSelectionInquiryData` callback shape:

```csharp
// MultiSelectionInquiryData.cs:26-32
public readonly Action<List<InquiryElement>> AffirmativeAction;
public readonly Action<List<InquiryElement>> NegativeAction;

public MultiSelectionInquiryData(string titleText, string descriptionText, List<InquiryElement> inquiryElements, bool isExitShown, int minSelectableOptionCount, int maxSelectableOptionCount, string affirmativeText, string negativeText, Action<List<InquiryElement>> affirmativeAction, Action<List<InquiryElement>> negativeAction, string soundEventPath = "", bool isSeachAvailable = false)
```

Search visibility:

```csharp
// SPInventoryVM.cs:573-591
[DataSourceProperty]
public bool IsSearchAvailable
{
    get { return _isSearchAvailable; }
    set
    {
        if (value != _isSearchAvailable)
        {
            if (!value)
            {
                LeftSearchText = string.Empty;
                RightSearchText = string.Empty;
            }
            _isSearchAvailable = value;
            OnPropertyChangedWithValue(value, "IsSearchAvailable");
        }
    }
}
```

```xml
<!-- Modules/SandBox/GUI/Prefabs/Inventory/Inventory.xml:99 and :539 -->
<BrushWidget ... Brush="SaveLoad.Search.Button" IsVisible="@IsSearchAvailable">
```

Campaign session ordering:

```csharp
// Campaign.cs:1668-1670, saved campaign
base.GameManager.OnAfterGameLoaded(base.CurrentGame);
OnGameLoaded(gameStarter);
OnSessionStart(gameStarter);

// Campaign.cs:1692-1694, new campaign
MBObjectManager.Instance.RemoveTemporaryTypes();
OnNewGameCreated(gameStarter);
OnSessionStart(gameStarter);
```

```csharp
// CampaignBehaviorManager.cs:76-79
public void AddBehavior(CampaignBehaviorBase campaignBehavior)
{
    _campaignBehaviors.Add(campaignBehavior);
    campaignBehavior.RegisterEvents();
}
```

## Scenario Analysis

### Scenario A: Weapons filter, `Sell All (Vanilla)`

Walkthrough: `Patch34_SellAllItemsMenu` opens the menu. Selecting `Sell All (Vanilla)` runs the manual lambda. The lambda checks `item.IsFiltered`, `item.IsLocked`, and `item.IsTransferable`, so a weapons-only active filter is respected for row eligibility.

Result: filter parity is correct for this scenario. However, this still routes through the non-vanilla manual loop, so Findings 1 and 2 still apply for stacks and post-filter vanilla behavior.

### Scenario B: 200 items, over capacity, settlement market with low gold

Walkthrough: vanilla `ExecuteSellAllItems()` calls `TransferAll(false)`. If the other party is a settlement, vanilla enters `TransferAllForSettlement`, tracks `LeftInventoryOwnerGold`, and transfers units while accumulated sale value remains under available settlement gold. The TAOM lambda does not enter `TransferAllForSettlement`; it invokes `ProcessSellItem(item, true)` once per matching row.

Result: CONFIRMED user-visible bug. The option labeled vanilla does not honor settlement gold handling or full-stack/capacity logic. See Finding 1.

### Scenario C: Open inventory, close, reopen

Walkthrough: constructor Postfix sets `_active` to the new VM. `GauntletInventoryScreen.OnFinalize()` calls `SPInventoryVM.OnFinalize()`, and the Postfix clears only if `ReferenceEquals(_active, __instance)`.

Result: lifecycle cleanup is safe for both orderings. If old finalizes before new ctor, `_active` clears then new ctor sets it. If new ctor happens before old finalize, `_active` points to the new VM and old `ClearActiveIfMatches(oldVm)` no-ops.

### Scenario D: Old save missing `TAOM_IsInventorySearchAvailable`, MCM disabled

Walkthrough: `_isSearchAvailable` defaults to `true`, then `SyncData` leaves it true if the key is absent. `OnGameLoaded` compares the field to `_settings.EnableInventorySearch` and rewrites it to the MCM value when different. Vanilla saved-campaign ordering is `OnGameLoaded(gameStarter)` before `OnSessionStart(gameStarter)`.

Result: safe. With MCM disabled, `OnGameLoaded` sets `_isSearchAvailable=false` before the inventory screen can normally open.

### Scenario E: Exit to menu, start new campaign in same process

Walkthrough: the DryIoc singleton can retain `_isSearchAvailable` from the prior campaign, but vanilla new-campaign ordering calls `OnNewGameCreated(gameStarter)` before `OnSessionStart(gameStarter)`. The behavior's `OnNewGameCreated` writes `_isSearchAvailable = _settings.EnableInventorySearch`.

Result: safe on the vanilla path. A hypothetical inventory open before `OnNewGameCreated` would see stale singleton state, but decompiled session ordering does not expose such a window because session launch happens after new-game creation.

### Scenario F: `UseCustomThreshold=false`, `DamagedPreset=Pristine`

Walkthrough: `QuickActionsSettingsProvider.ResolveDamagedThreshold()` returns `DamagedPreset.ToThreshold()`. The service then treats any threshold `>= 0f` as a sentinel and sells nothing:

```csharp
// Main/Features/QuickActions/QuickActionsService.cs:259-262
// Pristine threshold (0f) is reserved as a sentinel -- never matches; user must pick a real preset.
if (threshold >= 0f) return false;
var modifierOffset = item.ModifierPriceMultiplier - 1f;
return modifierOffset <= threshold;
```

Result: behavior is intentional and safe, but UX depends on the setting hint making clear that `Pristine` means "disabled/no damaged sale" rather than "sell pristine quality items." I did not classify this as a bug because the source comment explicitly defines the sentinel.

### Scenario G: Non-transferable damaged item

Walkthrough: `IsDamagedSellTarget` checks `!item.IsTransferable` before reading modifier quality. Vanilla `SPItemVM` sets `IsTransferable = !IsQuestItem && Item.IsTransferable && ...`, and `ProcessSellItem` also returns immediately when `!item.IsTransferable`.

Result: safe. Non-transferable quest/special items are not sold by `Sell Damaged`.

## Config Cross-Reference

Skipped. The prompt explicitly says this feature has no XML/JSON config and no culture/kingdom IDs.

## Known Suspects

| # | Suspect | Verdict (CONFIRMED/DISPUTED) | Evidence |
|---|---------|------------------------------|----------|
| 1 | `MultiSelectionInquiryData` callback signatures | DISPUTED | v1.3.15 constructor takes both `Action<List<InquiryElement>> affirmativeAction` and `Action<List<InquiryElement>> negativeAction`; TAOM passes `chosen => { ... }` and `_ => { }` at `QuickActionsService.cs:83-89`. |
| 2 | Stale VM lifecycle | DISPUTED | The 3-arg constructor exists and is called by `GauntletInventoryScreen` when opening inventory; `SPInventoryVM.OnFinalize()` exists and is invoked by `GauntletInventoryScreen.OnFinalize()`. `ClearActiveIfMatches` uses `ReferenceEquals`, so old-finalize-after-new-ctor is safe. |
| 3 | `vanillaSellAll` lambda parity with vanilla `TransferAll` | CONFIRMED | The lambda mirrors only the row filter and then invokes `ProcessSellItem(item, true)`. Vanilla `ExecuteSellAllItems()` calls `TransferAll(false)`, which applies settlement gold, capacity/warehouse budget, full-stack amounts, sorting, and zero-count cleanup. See Finding 1. |
| 4 | Modifier preservation in unequip | DISPUTED | TAOM calls `roster.AddToCounts(element, 1)` at `PlayerEquipmentAdapter.cs:42`, where `element` is `EquipmentElement`. v1.3.15 `ItemRoster.AddToCounts(EquipmentElement,int)` stores an `ItemRosterElement(rosterElement, 0)` and preserves `ItemModifier`; the `ItemObject` overload is the one that creates `new EquipmentElement(item)` and would discard modifiers. |
| 5 | `InventorySearchCampaignBehavior` singleton cross-campaign safety | DISPUTED | Vanilla new-campaign ordering is `OnNewGameCreated(gameStarter)` before `OnSessionStart(gameStarter)`; saved-campaign ordering is `OnGameLoaded(gameStarter)` before `OnSessionStart(gameStarter)`. The singleton is reseeded/reconciled before a normal inventory open. |
| 6 | `RefreshCallbacks` attach point for `IsSearchAvailable` | DISPUTED | The prompt's "fires once after binding" premise is false: constructor calls `RefreshCallbacks()` before binding, and `GauntletInventoryScreen.OnActivate()` calls it again after `LoadMovie`. This is still safe because the Postfix runs on both calls and the second write happens after UI binding. `Inventory.xml` binds search containers with `IsVisible="@IsSearchAvailable"`, so false hides the search boxes. |

Final top-line summary: CRITICAL: 0 | HIGH: 2 | MEDIUM: 1 | LOW: 0 | INFO: 1. Verified-clean areas: 5 Known Suspects disputed as bugs, 7 scenarios completed, all required vanilla methods/properties decompiled with no required ilspycmd failures.
