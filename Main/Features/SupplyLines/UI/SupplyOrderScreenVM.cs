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
    private readonly MBBindingList<SupplySearchHitRowVM> _searchHits = new MBBindingList<SupplySearchHitRowVM>();

    private SupplySourceRowVM? _selectedSource;

    // Cross-market search (#587). The catalogue is built on the first qualifying keystroke and
    // reused for the life of the screen: campaign time is frozen under this pushed game state, so
    // stock cannot move. Hits are keyed back to their settlement rows by the SupplySourceInfo
    // reference both sides already hold.
    private List<SupplyGoodsCatalogueEntry>? _catalogue;
    private readonly Dictionary<SupplySourceInfo, SupplySourceRowVM> _rowsBySource =
        new Dictionary<SupplySourceInfo, SupplySourceRowVM>();
    private string? _promotedItemId;
    private string _searchText = string.Empty;
    private bool _searchActive;
    private string _searchPlaceholderText;
    private string _searchStatusText = string.Empty;
    private float _goodsScrollValue;

    private string _screenTitle;
    // Button labels live here, not as literal Text= in the prefab: Gauntlet does not localize
    // literal prefab text (only VM-bound strings pass through TextObject), so a literal
    // "{=key}Label" renders the raw token in-game (field-tested 2026-08-25).
    private string _escortNoneText;
    private string _escortMercsText;
    private string _escortCompanionText;
    private string _confirmText;
    private string _clearText;
    private string _cancelText;
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
        _escortNoneText = new TextObject("{=taom_sl_escort_none}None").ToString();
        _escortMercsText = new TextObject("{=taom_sl_escort_mercs}Mercenaries").ToString();
        _escortCompanionText = new TextObject("{=taom_sl_escort_companion}Companion").ToString();
        _confirmText = new TextObject("{=taom_sl_confirm}Confirm").ToString();
        _clearText = new TextObject("{=taom_sl_clear}Clear").ToString();
        _cancelText = new TextObject("{=taom_sl_cancel}Cancel").ToString();
        _goodsHeaderText = new TextObject("{=taom_sl_goods_header}Goods in stock").ToString();
        _troopsHeaderText = new TextObject("{=taom_sl_troops_header}Volunteers (recruits)").ToString();
        _searchPlaceholderText = new TextObject("{=taom_sl_search_placeholder}Search goods in every market").ToString();

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
    public string EscortNoneText
    {
        get => _escortNoneText;
        set
        {
            if (_escortNoneText != value)
            {
                _escortNoneText = value;
                OnPropertyChangedWithValue(value, nameof(EscortNoneText));
            }
        }
    }

    [DataSourceProperty]
    public string EscortMercsText
    {
        get => _escortMercsText;
        set
        {
            if (_escortMercsText != value)
            {
                _escortMercsText = value;
                OnPropertyChangedWithValue(value, nameof(EscortMercsText));
            }
        }
    }

    [DataSourceProperty]
    public string EscortCompanionText
    {
        get => _escortCompanionText;
        set
        {
            if (_escortCompanionText != value)
            {
                _escortCompanionText = value;
                OnPropertyChangedWithValue(value, nameof(EscortCompanionText));
            }
        }
    }

    [DataSourceProperty]
    public string ConfirmText
    {
        get => _confirmText;
        set
        {
            if (_confirmText != value)
            {
                _confirmText = value;
                OnPropertyChangedWithValue(value, nameof(ConfirmText));
            }
        }
    }

    [DataSourceProperty]
    public string ClearText
    {
        get => _clearText;
        set
        {
            if (_clearText != value)
            {
                _clearText = value;
                OnPropertyChangedWithValue(value, nameof(ClearText));
            }
        }
    }

    [DataSourceProperty]
    public string CancelText
    {
        get => _cancelText;
        set
        {
            if (_cancelText != value)
            {
                _cancelText = value;
                OnPropertyChangedWithValue(value, nameof(CancelText));
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
    public MBBindingList<SupplySearchHitRowVM> SearchHits => _searchHits;

    /// <summary>
    /// Two-way: the EditableTextWidget writes every keystroke into this setter. The change is
    /// notified with the RAW value on purpose: echoing a trimmed or folded string re-enters the
    /// widget's Text setter while it is mid-update and desynchronises its visible and real text
    /// (the encyclopedia's SearchText stores lowercase but notifies the value it was given).
    /// </summary>
    [DataSourceProperty]
    public string SearchText
    {
        get => _searchText;
        set
        {
            value ??= string.Empty;
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChangedWithValue(value, nameof(SearchText));
                RefreshSearch();
            }
        }
    }

    [DataSourceProperty]
    public string SearchPlaceholderText
    {
        get => _searchPlaceholderText;
        set
        {
            if (_searchPlaceholderText != value)
            {
                _searchPlaceholderText = value;
                OnPropertyChangedWithValue(value, nameof(SearchPlaceholderText));
            }
        }
    }

    [DataSourceProperty]
    public string SearchStatusText
    {
        get => _searchStatusText;
        set
        {
            if (_searchStatusText != value)
            {
                _searchStatusText = value;
                OnPropertyChangedWithValue(value, nameof(SearchStatusText));
            }
        }
    }

    // Get-only on purpose: both IsHidden="@IsSearchActive" and IsVisible="@IsSearchActive"
    // write back through ViewModel.SetPropertyValue, which only finds PUBLIC setters; a setter
    // here would let the widget's own visibility change loop into the VM. Cached by
    // RefreshSearch so the query is folded once per keystroke, not once per binding read.
    [DataSourceProperty]
    public bool IsSearchActive => _searchActive;

    /// <summary>
    /// Two-way with the goods scrollbar. The panel keeps its offset across a repopulate, so a
    /// good promoted to the top after a search pick could sit above the viewport and the click
    /// would look dead; every repopulate resets this to 0.
    /// </summary>
    [DataSourceProperty]
    public float GoodsScrollValue
    {
        get => _goodsScrollValue;
        set
        {
            if (_goodsScrollValue != value)
            {
                _goodsScrollValue = value;
                OnPropertyChangedWithValue(value, nameof(GoodsScrollValue));
            }
        }
    }

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

    /// <summary>The "x" beside the box: the notify-back clears the widget's own text.</summary>
    public void ExecuteClearSearch()
    {
        SearchText = string.Empty;
    }

    private void PopulateSources()
    {
        _settlements.Clear();
        _rowsBySource.Clear();
        var sources = _sourceService.GetSources();
        if (sources == null)
            return;
        foreach (var info in sources)
        {
            if (info == null)
                continue;
            var row = new SupplySourceRowVM(info, OnSourceSelected);
            _settlements.Add(row);
            _rowsBySource[info] = row;
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

    // A plain settlement click: no promoted good, and any earlier hit highlight is forgotten.
    private void OnSourceSelected(SupplySourceRowVM row) => SelectSource(row, null);

    private void SelectSource(SupplySourceRowVM row, string? preferredItemId)
    {
        if (_selectedSource != null)
            _selectedSource.IsSelected = false;
        _selectedSource = row;
        if (row != null)
            row.IsSelected = true;
        _promotedItemId = preferredItemId;

        PopulateGoods(row, preferredItemId);
        PopulateTroops(row);
        RefreshHitHighlights();
        Recompute();
    }

    private void OnHitSelected(SupplySearchHitRowVM hit)
    {
        var row = hit?.SourceRow;
        if (row == null || !row.CanOrder)
            return;
        SelectSource(row, hit!.Item?.Id);
    }

    /// <summary>
    /// The goods pane's own <c>ScrollablePanel.ResetTweenSpeed</c>, handed in by the screen once
    /// the movie exists. A wheel notch leaves the pane coasting (ScrollablePanel.cs:588-598,
    /// v1.4.8) and that momentum survives a repopulate whenever both lists overflow, so the
    /// value reset alone could carry the pane off the promoted row again (Codex review #587
    /// P2). Not a binding: the VM never holds a widget.
    /// </summary>
    public Action? ResetGoodsScroll { get; set; }

    private void PopulateGoods(SupplySourceRowVM? row, string? preferredItemId)
    {
        _goods.Clear();
        ResetGoodsScroll?.Invoke();
        GoodsScrollValue = 0f;
        if (row == null)
            return;
        var lines = _sourceService.GetGoods(row.Info);
        if (lines == null)
            return;
        // The good the player searched for goes first; the rest keep the service's order.
        SupplyLineItem? preferred = null;
        foreach (var line in lines)
        {
            if (line == null)
                continue;
            if (preferred == null && !string.IsNullOrEmpty(preferredItemId) && line.Id == preferredItemId)
                preferred = line;
        }
        if (preferred != null)
            _goods.Add(new SupplyGoodRowVM(preferred, Recompute));
        foreach (var line in lines)
        {
            if (line != null && !ReferenceEquals(line, preferred))
                _goods.Add(new SupplyGoodRowVM(line, Recompute));
        }
    }

    // --- cross-market search ---

    private void RefreshSearch()
    {
        _searchHits.Clear();
        _searchActive = SupplyGoodsSearch.IsActive(_searchText);
        if (!_searchActive)
        {
            SearchStatusText = string.Empty;
            OnPropertyChanged(nameof(IsSearchActive));
            return;
        }

        var hits = SupplyGoodsSearch.Search(EnsureCatalogue(), _searchText, out var total);
        // Locks are applied at construction, never through Recompute: a keystroke must not
        // re-quote the order or erase a confirm failure the player is reading.
        bool locked = CanClear;
        foreach (var hit in hits)
        {
            if (hit?.Source == null || !_rowsBySource.TryGetValue(hit.Source, out var row))
                continue;
            bool isPicked = ReferenceEquals(row, _selectedSource)
                && _promotedItemId != null && hit.Item?.Id == _promotedItemId;
            _searchHits.Add(new SupplySearchHitRowVM(hit, row, isPicked, locked, OnHitSelected));
        }

        SearchStatusText = BuildSearchStatus(_searchHits.Count, total);
        OnPropertyChanged(nameof(IsSearchActive));
    }

    private static string BuildSearchStatus(int shown, int total)
    {
        if (total <= 0)
            return new TextObject("{=taom_sl_search_none}No source stocks that.").ToString();
        if (shown < total)
        {
            var capped = new TextObject("{=taom_sl_search_capped}Matches: {COUNT}, showing the nearest {SHOWN}. Narrow the search.");
            capped.SetTextVariable("SHOWN", shown);
            capped.SetTextVariable("COUNT", total);
            return capped.ToString();
        }
        var found = new TextObject("{=taom_sl_search_count}Matches: {COUNT}, nearest first.");
        found.SetTextVariable("COUNT", total);
        return found.ToString();
    }

    /// <summary>
    /// Orderable settlement rows only: lords sell no goods, and an at-war or unreachable row
    /// (CanOrder false after the row's own sanitizing) is never scanned, so a hit can always be
    /// ordered from.
    /// </summary>
    private List<SupplyGoodsCatalogueEntry> EnsureCatalogue()
    {
        if (_catalogue != null)
            return _catalogue;
        _catalogue = new List<SupplyGoodsCatalogueEntry>();
        foreach (var row in _settlements)
        {
            if (row == null || !row.CanOrder || row.IsLord)
                continue;
            _catalogue.Add(SupplyGoodsCatalogueEntry.Create(row.Info, row.Distance, _sourceService.GetGoods(row.Info)));
        }
        return _catalogue;
    }

    private void RefreshHitHighlights()
    {
        foreach (var hit in _searchHits)
        {
            hit.IsSelected = ReferenceEquals(hit.SourceRow, _selectedSource)
                && _promotedItemId != null && hit.Item?.Id == _promotedItemId;
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

        // One source at a time: any pending quantity locks every other row until Clear. Hits lock
        // wholesale: a hit from the selected source would repopulate the goods and wipe the order.
        foreach (var row in _settlements)
            row.SetLocked(CanClear && !ReferenceEquals(row, _selectedSource));
        foreach (var hit in _searchHits)
            hit.SetLocked(CanClear);

        // Any change invalidates a stale failure message from the previous confirm attempt.
        ErrorText = string.Empty;
    }

    private static string FormatLine(TextObject label, int value)
    {
        return $"{label}: {value}";
    }
}
