namespace TAOM.Adapters;

/// <summary>
/// Adapter wrapping <c>SPItemVM</c> for QuickActions filtering. Carries the underlying
/// <c>EquipmentElement</c> opaquely as <see cref="UnderlyingVm"/> so the sell delegate
/// (<c>SPItemVM.ProcessSellItem</c>) preserves <c>ItemModifier</c> through the round-trip.
/// </summary>
public interface IInventoryItemAdapter
{
    string ItemId { get; }
    int ItemValue { get; }
    bool IsLocked { get; }
    bool IsTransferable { get; }
    bool IsEquipped { get; }
    bool IsHorse { get; }
    bool IsFood { get; }
    bool IsTradeGood { get; }
    /// <summary>
    /// <c>ItemModifier?.PriceMultiplier ?? 1f</c>. The damage threshold compares
    /// <c>(this - 1f) &lt;= threshold</c>.
    /// </summary>
    float ModifierPriceMultiplier { get; }

    /// <summary>
    /// <c>ItemRosterElement.Amount</c> — the size of the underlying stack. Codex review
    /// #36 caught that <c>SPItemVM.ProcessSellItem(item, true)</c> reads
    /// <c>item.TransactionCount</c> (default 1), so without this, batch actions sold one
    /// unit per stack. The adapter's <see cref="IInventoryVMAdapter.TrySellItem"/>
    /// implementation is required to honor this amount.
    /// </summary>
    int StackAmount { get; }
    /// <summary>
    /// Opaque handle to the wrapped <c>SPItemVM</c>. Services do NOT type this back
    /// to <c>SPItemVM</c>; the adapter implementation owns that knowledge per ADR-007.
    /// </summary>
    object? UnderlyingVm { get; }
}
