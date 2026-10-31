using System;
using TaleWorlds.Library;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>One lane button of the command panel: its name, the key that selects it, the live counts and the standing order.</summary>
public sealed class FordsLaneVM : FordsViewModel
{
    private readonly Action<int> _onSelect;
    private string _title = string.Empty;
    private string _keyText = string.Empty;
    private string _countsText = string.Empty;
    private string _orderText = string.Empty;
    private bool _isSelected;

    internal FordsLaneVM(Action<int> onSelect)
    {
        _onSelect = onSelect;
    }

    /// <summary>The lane this row selects; each refresh rebinds it along with the label.</summary>
    internal int Lane { get; private set; }

    [DataSourceProperty]
    public string Title
    {
        get => _title;
        private set => Set(ref _title, value, nameof(Title));
    }

    [DataSourceProperty]
    public string KeyText
    {
        get => _keyText;
        private set => Set(ref _keyText, value, nameof(KeyText));
    }

    [DataSourceProperty]
    public string CountsText
    {
        get => _countsText;
        private set => Set(ref _countsText, value, nameof(CountsText));
    }

    [DataSourceProperty]
    public string OrderText
    {
        get => _orderText;
        private set => Set(ref _orderText, value, nameof(OrderText));
    }

    [DataSourceProperty]
    public bool IsSelected
    {
        get => _isSelected;
        private set => Set(ref _isSelected, value, nameof(IsSelected));
    }

    public void ExecuteSelect() => _onSelect(Lane);

    internal void Update(MatchView.LaneView lane, bool selected)
    {
        Lane = lane.Index;
        Title = FordsHudText.Lane(lane.Index + 1);
        KeyText = lane.Index < FordsCommandVM.LaneHotkeys ? (lane.Index + 1).ToString() : string.Empty;
        CountsText = FordsHudText.LaneCounts(lane.OwnLive, lane.EnemyLive);
        OrderText = FordsHudText.Order(lane.Order);
        IsSelected = selected;
    }
}
