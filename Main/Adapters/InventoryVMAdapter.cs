using System;
using System.Collections.Generic;
using HarmonyLib;
using Helpers;
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
    // SPInventoryVM.UpdateRightCharacter() (v1.4.8 :4377) and RefreshInformationValues() (:3968)
    // are private. Vanilla's single-transfer handlers follow with RefreshInformationValues (:3494),
    // which a batch defeats: AfterTransfer clears the equipment dirty flag on every command (:3840),
    // so the doll is skipped when the last command moved nothing. So RefreshDisplay runs the pair
    // vanilla runs when the shown outfit or character changes (:1645-1647, :4314-4315); neither
    // runs inside AddTransferCommands. Null after engine drift: the transfers still land and the
    // panel catches up at the next vanilla refresh.
    private static readonly Action<SPInventoryVM>? UpdateRightCharacter = BindPanelRefresh("UpdateRightCharacter");
    private static readonly Action<SPInventoryVM>? RefreshInformationValues = BindPanelRefresh("RefreshInformationValues");

    private readonly IModLogger _logger;
    private SPInventoryVM? _active;

    public InventoryVMAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    internal static bool IsPanelRefreshBound => UpdateRightCharacter != null && RefreshInformationValues != null;

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
        var active = _active;
        if (active == null) return;
        try
        {
            UpdateRightCharacter?.Invoke(active);      // paper doll, slots, armour, weight, can-use flags
            RefreshInformationValues?.Invoke(active);  // capacity, no-saddle warning, Done button
        }
        catch (Exception ex)
        {
            _logger.LogError($"[QuickActions] inventory panel refresh failed: {ex.Message}");
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
        // Only through the open screen's InventoryLogic, so vanilla AfterTransfer updates the rows
        // and slots and Cancel or Reset can undo it (#917). The target is the main hero, not the
        // character on screen, because the menu promises the player's own gear. The logic comes
        // from the public InventoryScreenHelper path; reflection on SPInventoryVM._inventoryLogic
        // was flagged by BUTR.Harmony.Analyzer (BHA0001) in Codex review #37.
        if (_active == null) return 0;
        var logic = InventoryScreenHelper.GetActiveInventoryState()?.InventoryLogic;
        if (logic == null)
        {
            _logger.LogWarning("[QuickActions] no active InventoryLogic: Unequip All skipped");
            return 0;
        }

        var hero = Hero.MainHero;
        var character = hero?.CharacterObject;
        if (hero == null || character == null) return 0;

        return BuildAndApplyUnequipCommands(
                   logic, character, hero.BattleEquipment, InventoryLogic.InventorySide.BattleEquipment,
                   Campaign.Current?.DeadBattleEquipment)
             + BuildAndApplyUnequipCommands(
                   logic, character, hero.CivilianEquipment, InventoryLogic.InventorySide.CivilianEquipment,
                   Campaign.Current?.DeadCivilianEquipment);
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
            // The source side and index name the slot. InventoryLogic itself never empties it: the
            // subscribed SPInventoryVM.AfterTransfer does (SPInventoryVM.cs:4195), so this route is
            // only correct against the logic the open screen listens to. The destination index is
            // ignored for PlayerInventory (InventoryLogic.cs:890, :922).
            commands.Add(TransferCommand.Transfer(
                1, fromSide, InventoryLogic.InventorySide.PlayerInventory,
                roster, i, EquipmentIndex.None, character));
        }
        if (commands.Count == 0) return 0;
        logic.AddTransferCommands(commands);
        return commands.Count;
    }

    private static Action<SPInventoryVM>? BindPanelRefresh(string name)
    {
        try
        {
            var method = AccessTools.Method(typeof(SPInventoryVM), name, Type.EmptyTypes);
            return method == null ? null : AccessTools.MethodDelegate<Action<SPInventoryVM>>(method);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
