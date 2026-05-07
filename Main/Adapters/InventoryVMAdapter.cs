using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <summary>
/// Singleton adapter wrapping the active <c>SPInventoryVM</c>. The active VM is captured
/// via <see cref="SetActive"/>, called from a Postfix on <c>SPInventoryVM</c>'s constructor;
/// cleared by the inventory screen finalize patch (or when next opens, since each screen
/// open creates a new VM instance).
/// </summary>
public sealed class InventoryVMAdapter : IInventoryVMAdapter
{
    private readonly IPlayerEquipmentAdapter _playerEquipment;
    private readonly IModLogger _logger;
    private SPInventoryVM? _active;

    public InventoryVMAdapter(IPlayerEquipmentAdapter playerEquipment, IModLogger logger)
    {
        _playerEquipment = playerEquipment;
        _logger = logger;
    }

    /// <summary>Called from <c>Patch34_SPInventoryVMCapture</c>.</summary>
    public void SetActive(SPInventoryVM? vm)
    {
        _active = vm;
    }

    /// <summary>
    /// Called from <c>Patch34_SPInventoryVMFinalize</c>. Only clears if the supplied VM
    /// is the currently-active one (defensive against constructor/finalize overlap).
    /// </summary>
    public void ClearActiveIfMatches(SPInventoryVM vm)
    {
        if (_active != null && ReferenceEquals(_active, vm))
            _active = null;
    }

    public bool IsAvailable => _active != null;

    public IReadOnlyList<IInventoryItemAdapter> GetRightPaneItems()
    {
        if (_active == null) return Array.Empty<IInventoryItemAdapter>();
        var list = _active.RightItemListVM;
        if (list == null || list.Count == 0) return Array.Empty<IInventoryItemAdapter>();

        var result = new List<IInventoryItemAdapter>(list.Count);
        foreach (var vm in list)
        {
            if (vm != null) result.Add(new InventoryItemAdapter(vm));
        }
        return result;
    }

    public bool TrySellItem(IInventoryItemAdapter item)
    {
        if (_active == null) return false;
        if (item?.UnderlyingVm is not SPItemVM spItem) return false;

        // Vanilla path: SPItemVM.ProcessSellItem is a public static Action<SPItemVM, bool> set
        // by the inventory screen on init. Calling it preserves ItemModifier because the delegate
        // operates on the SPItemVM (which carries the full EquipmentElement).
        //
        // Codex review #36 fix: ProcessSellItem with cameFromTradeData=true reads
        // item.TransactionCount (default 1) for the sell amount. Without setting it, every
        // call sold a single unit per stack — a 50-arrow stack reported "1 sold" but only
        // transferred 1. Set TransactionCount = full stack size before invoke. We pass
        // cameFromTradeData=true to skip vanilla's "entire stack modifier" / "five stack
        // modifier" branches (those rely on ALT/CTRL key state which doesn't apply in
        // programmatic sells).
        var del = SPItemVM.ProcessSellItem;
        if (del == null)
        {
            _logger.LogWarning("[QuickActions] SPItemVM.ProcessSellItem delegate was null — cannot sell");
            return false;
        }

        try
        {
            var amount = item.StackAmount;
            if (amount <= 0) return false;
            spItem.TransactionCount = amount;
            del.Invoke(spItem, true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[QuickActions] ProcessSellItem invoke failed: {ex.Message}");
            return false;
        }
    }

    public void RefreshDisplay()
    {
        if (_active == null) return;
        try
        {
            _active.RefreshValues();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[QuickActions] RefreshValues failed: {ex.Message}");
        }
    }

    public bool IsSearchAvailable
    {
        get => _active?.IsSearchAvailable ?? false;
        set
        {
            if (_active == null) return;
            _active.IsSearchAvailable = value;
        }
    }

    public int TryUnequipAllPlayerSlots()
    {
        // Codex review #36 fix: when an inventory screen is open, route unequip through
        // InventoryLogic.TransferCommand. Vanilla AfterTransfer rebuilds RightItemListVM and
        // the equipment-slot SPItemVMs in response. Without this, direct mutation of
        // Hero.BattleEquipment + ItemRoster.AddToCounts left the UI showing stale empty rows.
        if (_active != null && TryUnequipViaInventoryLogic(out var unitsTransferred))
            return unitsTransferred;

        // Fallback: direct mutation. Used when no inventory screen is active (this code
        // path is technically unreachable today because UnequipAll is only invoked from
        // the open-inventory menu, but keep it as defensive belt-and-suspenders).
        return _playerEquipment.TryUnequipAllPlayerSlots();
    }

    // Cached reflection -- SPInventoryVM stores its InventoryLogic and the active character
    // as private fields. Resolved once on first use.
    private static readonly FieldInfo? _inventoryLogicField =
        AccessToolsLite.GetPrivateField(typeof(SPInventoryVM), "_inventoryLogic");
    private static readonly FieldInfo? _currentCharacterField =
        AccessToolsLite.GetPrivateField(typeof(SPInventoryVM), "_currentCharacter");

    private bool TryUnequipViaInventoryLogic(out int unitsTransferred)
    {
        unitsTransferred = 0;
        if (_active == null) return false;
        if (_inventoryLogicField == null || _currentCharacterField == null)
        {
            _logger.LogWarning("[QuickActions] SPInventoryVM private field reflection failed — falling back to direct mutation");
            return false;
        }

        if (_inventoryLogicField.GetValue(_active) is not InventoryLogic logic) return false;
        var character = _currentCharacterField.GetValue(_active) as CharacterObject;
        if (character == null) return false;

        var hero = Hero.MainHero;
        if (hero == null) return false;

        unitsTransferred += BuildAndApplyUnequipCommands(
            logic, character, hero.BattleEquipment, InventoryLogic.InventorySide.BattleEquipment,
            Campaign.Current?.DeadBattleEquipment);
        unitsTransferred += BuildAndApplyUnequipCommands(
            logic, character, hero.CivilianEquipment, InventoryLogic.InventorySide.CivilianEquipment,
            Campaign.Current?.DeadCivilianEquipment);

        return true;
    }

    private static int BuildAndApplyUnequipCommands(
        InventoryLogic logic, CharacterObject character, Equipment equipment,
        InventoryLogic.InventorySide fromSide, Equipment? deadSingleton)
    {
        if (equipment == null || equipment == deadSingleton) return 0;
        var commands = new List<TransferCommand>();
        for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumEquipmentSetSlots; i++)
        {
            var element = equipment[i];
            if (element.IsEmpty) continue;
            var roster = new ItemRosterElement(element, 1);
            // Source side carries the EquipmentIndex so vanilla knows which slot to clear.
            // EquipmentIndex.None on the destination = drop into the inventory roster.
            commands.Add(TransferCommand.Transfer(
                1, fromSide, InventoryLogic.InventorySide.PlayerInventory,
                roster, i, EquipmentIndex.None, character));
        }
        if (commands.Count == 0) return 0;
        logic.AddTransferCommands(commands);
        return commands.Count;
    }
}

internal static class AccessToolsLite
{
    public static FieldInfo? GetPrivateField(Type type, string name)
    {
        return type?.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    }
}
