using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.SupplyLines.Domain;

namespace TAOM.Features.SupplyLines.UI;

/// <summary>
/// The supply order screen's data source. All campaign state arrives through the injected
/// services and the gold delegate, so the VM is constructible with mocks only (review #26
/// lesson: no IoC.Resolve inside a VM; the screen resolves at its engine-instantiated boundary
/// and passes everything in).
///
/// <para>Behaviour ported from the source module's SupplyOrderScreenVM with two fixes: the
/// quote uses the same escort the order will be charged with (the source quoted a mercenary
/// guard fee on lord sources it then never charged), and a disabled/failed confirm reports
/// inline through <see cref="ErrorText"/> instead of silently closing the screen.</para>
/// </summary>
public sealed class SupplyOrderScreenVM : ViewModel
{
    private readonly ISupplySourceService _sourceService;
    private readonly ISupplyPricingService _pricingService;
    private readonly ISupplyOrderService _orderService;
    private readonly ISupplyLinesSettingsProvider _settings;
    private readonly Func<int> _playerGold;
    private readonly Action _closeAction;
    private readonly bool _placedFromCamp;

    private readonly MBBindingList<SupplySourceRowVM> _settlements = new MBBindingList<SupplySourceRowVM>();
    private readonly MBBindingList<SupplyGoodRowVM> _goods = new MBBindingList<SupplyGoodRowVM>();
    private readonly MBBindingList<SupplyTroopRowVM> _troops = new MBBindingList<SupplyTroopRowVM>();

    private SupplySourceRowVM? _selectedSource;

    private string _screenTitle;
    private string _goodsHeaderText;
    private string _troopsHeaderText;
    private bool _escortNone = true;
    private bool _escortMercenaries;
    private bool _escortCompanion;
    private string _goodsText = string.Empty;
    private string _troopText = string.Empty;
    private string _transportText = string.Empty;
    private string _guardText = string.Empty;
    private string _totalText = string.Empty;
    private string _errorText = string.Empty;
    private bool _canConfirm;
    private bool _canClear;

    public SupplyOrderScreenVM(
        ISupplySourceService sourceService,
        ISupplyPricingService pricingService,
        ISupplyOrderService orderService,
        ISupplyLinesSettingsProvider settings,
        Func<int> playerGold,
        Action closeAction,
        bool placedFromCamp = false)
    {
        _sourceService = sourceService;
        _pricingService = pricingService;
        _orderService = orderService;
        _settings = settings;
        _playerGold = playerGold;
        _closeAction = closeAction;
        _placedFromCamp = placedFromCamp;

        _screenTitle = new TextObject("{=taom_sl_screen_title}Supply Order").ToString();
        _goodsHeaderText = new TextObject("{=taom_sl_goods_header}Goods in stock").ToString();
        _troopsHeaderText = new TextObject("{=taom_sl_troops_header}Volunteers (recruits)").ToString();

        PopulateSources();
        var first = FirstOrderableSource();
        if (first != null)
            OnSourceSelected(first);
        Recompute();
    }

    private SupplyEscortOption CurrentEscort
    {
        get
        {
            if (_escortCompanion)
                return SupplyEscortOption.Companion;
            return _escortMercenaries ? SupplyEscortOption.Mercenaries : SupplyEscortOption.None;
        }
    }

    // Lord sources never carry an escort (the lord's own men bring the recruits). Using the
    // effective escort for BOTH the quote and the order keeps the displayed guard fee equal to
    // the charged one; the source module quoted CurrentEscort but charged None for lords.
    private SupplyEscortOption EffectiveEscort =>
        _selectedSource != null && _selectedSource.IsLord ? SupplyEscortOption.None : CurrentEscort;

    [DataSourceProperty]
    public string ScreenTitle
    {
        get => _screenTitle;
        set
        {
            if (_screenTitle != value)
            {
                _screenTitle = value;
                OnPropertyChangedWithValue(value, nameof(ScreenTitle));
            }
        }
    }

    [DataSourceProperty]
    public string GoodsHeaderText
    {
        get => _goodsHeaderText;
        set
        {
            if (_goodsHeaderText != value)
            {
                _goodsHeaderText = value;
                OnPropertyChangedWithValue(value, nameof(GoodsHeaderText));
            }
        }
    }

    [DataSourceProperty]
    public string TroopsHeaderText
    {
        get => _troopsHeaderText;
        set
        {
            if (_troopsHeaderText != value)
            {
                _troopsHeaderText = value;
                OnPropertyChangedWithValue(value, nameof(TroopsHeaderText));
            }
        }
    }

    [DataSourceProperty]
    public MBBindingList<SupplySourceRowVM> Settlements => _settlements;

    [DataSourceProperty]
    public MBBindingList<SupplyGoodRowVM> Goods => _goods;

    [DataSourceProperty]
    public MBBindingList<SupplyTroopRowVM> Troops => _troops;

    [DataSourceProperty]
    public bool EscortNone
    {
        get => _escortNone;
        set
        {
            if (_escortNone != value)
            {
                _escortNone = value;
                OnPropertyChangedWithValue(value, nameof(EscortNone));
            }
        }
    }

    [DataSourceProperty]
    public bool EscortMercenaries
    {
        get => _escortMercenaries;
        set
        {
            if (_escortMercenaries != value)
            {
                _escortMercenaries = value;
                OnPropertyChangedWithValue(value, nameof(EscortMercenaries));
            }
        }
    }

    [DataSourceProperty]
    public bool EscortCompanion
    {
        get => _escortCompanion;
        set
        {
            if (_escortCompanion != value)
            {
                _escortCompanion = value;
                OnPropertyChangedWithValue(value, nameof(EscortCompanion));
            }
        }
    }

    [DataSourceProperty]
    public string GoodsText
    {
        get => _goodsText;
        set
        {
            if (_goodsText != value)
            {
                _goodsText = value;
                OnPropertyChangedWithValue(value, nameof(GoodsText));
            }
        }
    }

    [DataSourceProperty]
    public string TroopText
    {
        get => _troopText;
        set
        {
            if (_troopText != value)
            {
                _troopText = value;
                OnPropertyChangedWithValue(value, nameof(TroopText));
            }
        }
    }

    [DataSourceProperty]
    public string TransportText
    {
        get => _transportText;
        set
        {
            if (_transportText != value)
            {
                _transportText = value;
                OnPropertyChangedWithValue(value, nameof(TransportText));
            }
        }
    }

    [DataSourceProperty]
    public string GuardText
    {
        get => _guardText;
        set
        {
            if (_guardText != value)
            {
                _guardText = value;
                OnPropertyChangedWithValue(value, nameof(GuardText));
            }
        }
    }

    [DataSourceProperty]
    public string TotalText
    {
        get => _totalText;
        set
        {
            if (_totalText != value)
            {
                _totalText = value;
                OnPropertyChangedWithValue(value, nameof(TotalText));
            }
        }
    }

    [DataSourceProperty]
    public string ErrorText
    {
        get => _errorText;
        set
        {
            if (_errorText != value)
            {
                _errorText = value;
                OnPropertyChangedWithValue(value, nameof(ErrorText));
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    [DataSourceProperty]
    public bool HasError => !string.IsNullOrEmpty(_errorText);

    [DataSourceProperty]
    public bool CanConfirm
    {
        get => _canConfirm;
        set
        {
            if (_canConfirm != value)
            {
                _canConfirm = value;
                OnPropertyChangedWithValue(value, nameof(CanConfirm));
            }
        }
    }

    [DataSourceProperty]
    public bool CanClear
    {
        get => _canClear;
        set
        {
            if (_canClear != value)
            {
                _canClear = value;
                OnPropertyChangedWithValue(value, nameof(CanClear));
            }
        }
    }

    public void ExecuteEscortNone()
    {
        EscortNone = true;
        EscortMercenaries = false;
        EscortCompanion = false;
        Recompute();
    }

    public void ExecuteEscortMercenaries()
    {
        EscortNone = false;
        EscortMercenaries = true;
        EscortCompanion = false;
        Recompute();
    }

    public void ExecuteEscortCompanion()
    {
        EscortNone = false;
        EscortMercenaries = false;
        EscortCompanion = true;
        Recompute();
    }

    public void ExecuteClear()
    {
        foreach (var good in _goods)
            good.ResetQty();
        foreach (var troop in _troops)
            troop.ResetQty();
        Recompute();
    }

    public void ExecuteConfirm()
    {
        // Fresh gate: gold or stock may have moved since the last +/- click.
        Recompute();
        if (_selectedSource == null || !CanConfirm)
            return;

        var goods = new Dictionary<string, int>();
        foreach (var good in _goods)
        {
            if (good.Qty > 0)
                goods[good.ItemId] = good.Qty;
        }

        var troops = new Dictionary<string, int>();
        foreach (var troop in _troops)
        {
            if (troop.Qty > 0)
                troops[troop.ItemId] = troop.Qty;
        }

        var order = _orderService.TryPlaceOrder(
            _selectedSource.Info, goods, troops, EffectiveEscort, out var failReason, _placedFromCamp);
        if (order != null)
        {
            _closeAction?.Invoke();
            return;
        }

        // The screen stays up so the player can adjust the order; the service's reason renders
        // inline above the footer (the source module closed silently on some failure paths).
        ErrorText = string.IsNullOrEmpty(failReason)
            ? new TextObject("{=taom_sl_order_failed}The order could not be placed.").ToString()
            : failReason;
    }

    public void ExecuteCancel()
    {
        _closeAction?.Invoke();
    }

    private void PopulateSources()
    {
        _settlements.Clear();
        var sources = _sourceService.GetSources();
        if (sources == null)
            return;
        foreach (var info in sources)
        {
            if (info != null)
                _settlements.Add(new SupplySourceRowVM(info, OnSourceSelected));
        }
    }

    private SupplySourceRowVM? FirstOrderableSource()
    {
        foreach (var row in _settlements)
        {
            if (row.CanOrder)
                return row;
        }
        return null;
    }

    private void OnSourceSelected(SupplySourceRowVM row)
    {
        if (_selectedSource != null)
            _selectedSource.IsSelected = false;
        _selectedSource = row;
        if (row != null)
            row.IsSelected = true;

        PopulateGoods(row);
        PopulateTroops(row);
        Recompute();
    }

    private void PopulateGoods(SupplySourceRowVM? row)
    {
        _goods.Clear();
        if (row == null)
            return;
        var lines = _sourceService.GetGoods(row.Info);
        if (lines == null)
            return;
        foreach (var line in lines)
        {
            if (line != null)
                _goods.Add(new SupplyGoodRowVM(line, Recompute));
        }
    }

    private void PopulateTroops(SupplySourceRowVM? row)
    {
        _troops.Clear();
        if (row == null)
            return;
        var lines = _sourceService.GetTroops(row.Info);
        if (lines == null)
            return;
        foreach (var line in lines)
        {
            if (line != null)
                _troops.Add(new SupplyTroopRowVM(line, Recompute));
        }
    }

    private void Recompute()
    {
        float goodsMarketValue = 0f;
        var goodsQty = 0;
        foreach (var good in _goods)
        {
            goodsMarketValue += good.UnitPrice * (float)good.Qty;
            goodsQty += good.Qty;
        }

        var troopRecruitCost = 0;
        var troopQty = 0;
        foreach (var troop in _troops)
        {
            troopRecruitCost += troop.UnitPrice * troop.Qty;
            troopQty += troop.Qty;
        }

        // Row distance is already sanitized (finite, non-negative) by SupplySourceRowVM.
        var distance = _selectedSource?.Distance ?? 0f;
        var quote = _pricingService.Quote(goodsMarketValue, troopRecruitCost, distance, EffectiveEscort);

        GoodsText = FormatLine(new TextObject("{=taom_sl_goods}Goods"), quote.Goods);
        TroopText = FormatLine(new TextObject("{=taom_sl_troops}Recruits"), quote.Troops);
        TransportText = FormatLine(new TextObject("{=taom_sl_transport}Transport"), quote.Transport);
        GuardText = FormatLine(new TextObject("{=taom_sl_guard}Guard"), quote.Guard);
        TotalText = FormatLine(new TextObject("{=taom_sl_total}Total"), quote.Total);

        var totalQty = goodsQty + troopQty;
        var gold = _playerGold?.Invoke() ?? 0;

        // Enabled gate: the screen only opens while the feature is on, but MCM can flip the
        // toggle mid-session with the screen open; a disabled feature must not take new orders.
        CanConfirm = _selectedSource != null
            && _selectedSource.CanOrder
            && totalQty > 0
            && quote.Total <= gold
            && _settings.Enabled;
        CanClear = totalQty > 0;

        // One source at a time: any pending quantity locks every other row until Clear.
        foreach (var row in _settlements)
            row.SetLocked(CanClear && !ReferenceEquals(row, _selectedSource));

        // Any change invalidates a stale failure message from the previous confirm attempt.
        ErrorText = string.Empty;
    }

    private static string FormatLine(TextObject label, int value)
    {
        return $"{label}: {value}";
    }
}
