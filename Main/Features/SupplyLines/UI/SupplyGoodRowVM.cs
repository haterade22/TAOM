using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.SupplyLines.UI;

/// <summary>
/// One purchasable good at the selected source. Quantity is clamped to [0, Available]; every
/// change calls back into the parent VM's Recompute so the price lines and the one-source lock
/// stay current.
/// </summary>
public sealed class SupplyGoodRowVM : ViewModel
{
    private readonly Action _onChanged;
    private readonly string _label;
    private int _qty;

    public SupplyGoodRowVM(SupplyLineItem item, Action onChanged)
    {
        _onChanged = onChanged;
        ItemId = item?.Id ?? string.Empty;
        Available = Math.Max(0, item?.Available ?? 0);
        UnitPrice = item?.UnitPrice ?? 0;

        var label = new TextObject("{=taom_sl_good_row}{NAME} - {STOCK} in stock, {PRICE} denars");
        label.SetTextVariable("NAME", item?.Name ?? ItemId);
        label.SetTextVariable("STOCK", Available);
        label.SetTextVariable("PRICE", UnitPrice);
        _label = label.ToString();
    }

    public string ItemId { get; }

    public int Available { get; }

    public int UnitPrice { get; }

    public int Qty
    {
        get => _qty;
        private set
        {
            if (_qty != value)
            {
                _qty = value;
                OnPropertyChanged(nameof(QtyText));
                _onChanged?.Invoke();
            }
        }
    }

    [DataSourceProperty]
    public string QtyText => _qty.ToString();

    [DataSourceProperty]
    public string Label => _label;

    public void ExecutePlus()
    {
        if (_qty < Available)
            Qty = _qty + 1;
    }

    public void ExecuteMinus()
    {
        if (_qty > 0)
            Qty = _qty - 1;
    }

    /// <summary>Silent reset: no per-row callback, the caller runs one Recompute for the batch.</summary>
    public void ResetQty()
    {
        if (_qty != 0)
        {
            _qty = 0;
            OnPropertyChanged(nameof(QtyText));
        }
    }
}
