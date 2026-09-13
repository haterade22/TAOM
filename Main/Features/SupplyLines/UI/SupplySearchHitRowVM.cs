using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.SupplyLines.UI;

/// <summary>
/// One cross-market search result: a good at a source. Picking it is delegated to the parent VM,
/// which selects the source and promotes the good. A pending order locks EVERY hit (a hit from the
/// selected source would repopulate the goods list and wipe the quantities), and the picked hit
/// keeps its highlight while locked so the player still sees what they chose.
/// </summary>
public sealed class SupplySearchHitRowVM : ViewModel
{
    private readonly Action<SupplySearchHitRowVM> _onSelected;
    private readonly string _label;
    private readonly string _detailText;
    private bool _isSelected;
    private bool _locked;

    public SupplySearchHitRowVM(
        SupplyGoodsHit hit,
        SupplySourceRowVM sourceRow,
        bool isSelected,
        bool locked,
        Action<SupplySearchHitRowVM> onSelected)
    {
        Item = hit?.Item ?? new SupplyLineItem { Id = string.Empty };
        SourceRow = sourceRow;
        _onSelected = onSelected;
        _isSelected = isSelected;
        _locked = locked;

        var itemName = string.IsNullOrEmpty(Item.Name) ? Item.Id : Item.Name;
        var sourceName = sourceRow?.Info?.DisplayName;
        if (string.IsNullOrEmpty(sourceName))
            sourceName = "?";

        var label = new TextObject("{=taom_sl_hit_row}{ITEM} at {SOURCE}");
        label.SetTextVariable("ITEM", itemName ?? string.Empty);
        label.SetTextVariable("SOURCE", sourceName);
        _label = label.ToString();

        // Distance reuses the source row's sanitized text so both columns agree on the number.
        var detail = new TextObject("{=taom_sl_hit_detail}{STOCK} in stock, {PRICE} denars, {DISTANCE} away");
        detail.SetTextVariable("STOCK", Math.Max(0, Item.Available));
        detail.SetTextVariable("PRICE", Item.UnitPrice);
        detail.SetTextVariable("DISTANCE", sourceRow?.DistanceText ?? "?");
        _detailText = detail.ToString();
    }

    /// <summary>The good this hit names; the parent promotes it to the top of the goods list.</summary>
    public SupplyLineItem Item { get; }

    /// <summary>The settlement row the hit belongs to; the parent selects it on pick.</summary>
    public SupplySourceRowVM SourceRow { get; }

    [DataSourceProperty]
    public bool RowEnabled => !_locked;

    [DataSourceProperty]
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChangedWithValue(value, nameof(IsSelected));
            }
        }
    }

    [DataSourceProperty]
    public string Label => _label;

    [DataSourceProperty]
    public string DetailText => _detailText;

    public void ExecuteSelect()
    {
        if (!_locked)
            _onSelected?.Invoke(this);
    }

    public void SetLocked(bool locked)
    {
        if (_locked != locked)
        {
            _locked = locked;
            OnPropertyChanged(nameof(RowEnabled));
        }
    }
}
