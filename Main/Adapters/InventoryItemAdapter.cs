using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;

namespace TAOM.Adapters;

/// <summary>
/// Wraps a single <c>SPItemVM</c>. Reads vanilla properties only — no reflection on
/// the fast path (all members were verified public in v1.3.15).
/// </summary>
public sealed class InventoryItemAdapter : IInventoryItemAdapter
{
    private readonly SPItemVM _vm;

    public InventoryItemAdapter(SPItemVM vm)
    {
        _vm = vm;
    }

    public string ItemId => _vm?.ItemRosterElement.EquipmentElement.Item?.StringId ?? string.Empty;

    public int ItemValue => _vm?.ItemCost ?? 0;

    public bool IsLocked => _vm?.IsLocked ?? true;

    public bool IsTransferable => _vm?.IsTransferable ?? false;

    public bool IsHorse => _vm?.ItemRosterElement.EquipmentElement.Item?.IsMountable ?? false;

    public bool IsFood => _vm?.ItemRosterElement.EquipmentElement.Item?.IsFood ?? false;

    public bool IsTradeGood => _vm?.ItemRosterElement.EquipmentElement.Item?.IsTradeGood ?? false;

    public float ModifierPriceMultiplier =>
        _vm?.ItemRosterElement.EquipmentElement.ItemModifier?.PriceMultiplier ?? 1f;

    public int StackAmount => _vm?.ItemRosterElement.Amount ?? 0;

    public object? UnderlyingVm => _vm;
}
