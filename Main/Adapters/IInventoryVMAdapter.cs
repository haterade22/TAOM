using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// Wraps the active <c>SPInventoryVM</c> instance so QuickActions and EquipPresets can
/// share one reflection / property-access surface. The implementation captures the active
/// VM via a Postfix on <c>SPInventoryVM</c> construction; methods become inert
/// (<see cref="IsAvailable"/> false) when no inventory screen is open.
/// </summary>
public interface IInventoryVMAdapter
{
    /// <summary>True when an inventory screen is currently open and the VM is ready for property writes.</summary>
    bool IsAvailable { get; }

    /// <summary>Snapshot of the right-pane (player inventory) item list. Empty when not available.</summary>
    IReadOnlyList<IInventoryItemAdapter> GetRightPaneItems();

    /// <summary>
    /// Sells a single item via the vanilla <c>SPItemVM.ProcessSellItem</c> static delegate.
    /// Returns true if invoked successfully; false if delegate or VM was not available.
    /// </summary>
    bool TrySellItem(IInventoryItemAdapter item);

    /// <summary>Refreshes the active VM's display (item list rebuild) after a batch operation.</summary>
    void RefreshDisplay();

    /// <summary>
    /// Read returns false if no VM is active. Write is a no-op if no VM is active.
    /// Used by the search-toggle Postfix and (later) EquipPresets.
    /// </summary>
    bool IsSearchAvailable { get; set; }

    /// <summary>
    /// Unequips every slot of <c>Hero.MainHero.BattleEquipment</c> and <c>CivilianEquipment</c>,
    /// depositing items (with modifier preserved) into the main party's <c>ItemRoster</c>.
    /// Returns the count of slots that had items.
    /// </summary>
    int TryUnequipAllPlayerSlots();
}
