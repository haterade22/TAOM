using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.SupplyLines.UI;

/// <summary>
/// One recruitable troop at the selected source. Same clamp-and-callback contract as
/// <see cref="SupplyGoodRowVM"/>; kept as its own class because the row label wording differs
/// and the prefab binds the two lists through separate item templates.
/// </summary>
public sealed class SupplyTroopRowVM : ViewModel
{
    private readonly Action _onChanged;
    private readonly string _label;
    private int _qty;

    public SupplyTroopRowVM(SupplyLineItem item, Action onChanged)
    {
        _onChanged = onChanged;
        ItemId = item?.Id ?? string.Empty;
        Available = Math.Max(0, item?.Available ?? 0);
        UnitPrice = item?.UnitPrice ?? 0;

        var label = new TextObject("{=taom_sl_troop_row}{NAME} - {COUNT} available, {PRICE} denars");
        label.SetTextVariable("NAME", item?.Name ?? ItemId);
        label.SetTextVariable("COUNT", Available);
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
