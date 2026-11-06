using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;

namespace TAOM.Features.FactionUI.UI;

/// <summary>
/// The kingdom page's Clans panel: one horizontal gallery that shows the member clans, wars,
/// alliances or trade agreements, picked by a tab. Each native list keeps its own order; switching
/// tabs returns that list to its first page. The members are borrowed, never finalized here.
/// </summary>
internal sealed class KingdomRelationSectionsVM<T> : ViewModel where T : ViewModel
{
    internal const int Clans = 0, Wars = 1, Alliances = 2, Trade = 3;
    private readonly List<T>[] _lists = { new(), new(), new(), new() };
    private readonly string[] _labels = { "", "", "", "" };
    private int _selected = Clans;

    [DataSourceProperty] public RelationshipGalleryVM<T> Gallery { get; }
    [DataSourceProperty] public string ClansLabel => _labels[Clans];
    [DataSourceProperty] public string WarsLabel => _labels[Wars];
    [DataSourceProperty] public string AlliancesLabel => _labels[Alliances];
    [DataSourceProperty] public string TradeLabel => _labels[Trade];
    [DataSourceProperty] public bool IsClansSelected => _selected == Clans;
    [DataSourceProperty] public bool IsWarsSelected => _selected == Wars;
    [DataSourceProperty] public bool IsAlliancesSelected => _selected == Alliances;
    [DataSourceProperty] public bool IsTradeSelected => _selected == Trade;

    public KingdomRelationSectionsVM(string emptyText, int pageSize) =>
        Gallery = new RelationshipGalleryVM<T>(emptyText, pageSize);

    /// <summary>Takes a fresh snapshot of the four native lists and keeps the selected tab.</summary>
    public void SetSections(IEnumerable<T> clans, string clansLabel, IEnumerable<T> wars, string warsLabel,
        IEnumerable<T> alliances, string alliancesLabel, IEnumerable<T> trade, string tradeLabel)
    {
        Store(Clans, clans, clansLabel);
        Store(Wars, wars, warsLabel);
        Store(Alliances, alliances, alliancesLabel);
        Store(Trade, trade, tradeLabel);
        OnPropertyChanged(nameof(ClansLabel));
        OnPropertyChanged(nameof(WarsLabel));
        OnPropertyChanged(nameof(AlliancesLabel));
        OnPropertyChanged(nameof(TradeLabel));
        Show(_selected);
    }

    public void ExecuteShowClans() => Show(Clans);
    public void ExecuteShowWars() => Show(Wars);
    public void ExecuteShowAlliances() => Show(Alliances);
    public void ExecuteShowTrade() => Show(Trade);

    private void Store(int section, IEnumerable<T> items, string label)
    {
        _lists[section] = items?.ToList() ?? new List<T>();
        _labels[section] = label ?? "";
    }

    private void Show(int section)
    {
        _selected = section;
        Gallery.SetItems(_lists[section], Enumerable.Empty<T>(), _labels[section]);
        OnPropertyChanged(nameof(IsClansSelected));
        OnPropertyChanged(nameof(IsWarsSelected));
        OnPropertyChanged(nameof(IsAlliancesSelected));
        OnPropertyChanged(nameof(IsTradeSelected));
    }

    public override void OnFinalize()
    {
        // The encyclopedia page owns the native faction VMs.
        foreach (var list in _lists) list.Clear();
        Gallery.OnFinalize();
        base.OnFinalize();
    }
}
