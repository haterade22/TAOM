using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>One lane's row of tower bars, laid out as the lane runs: West's inner tower first, East's inner tower last.</summary>
public sealed class FordsLaneBarVM : FordsViewModel
{
    private string _title = string.Empty;

    public FordsLaneBarVM()
    {
        Towers = new MBBindingList<FordsTowerBarVM>();
    }

    [DataSourceProperty]
    public string Title
    {
        get => _title;
        private set => Set(ref _title, value, nameof(Title));
    }

    [DataSourceProperty]
    public MBBindingList<FordsTowerBarVM> Towers { get; }

    internal void Update(MatchView.LaneView lane, MatchSide own)
    {
        Title = FordsHudText.Lane(lane.Index + 1);
        var ordered = lane.Towers.Where(t => t.Owner == MatchSide.West).OrderByDescending(t => t.Order)
            .Concat(lane.Towers.Where(t => t.Owner == MatchSide.East).OrderBy(t => t.Order))
            .ToList();
        FordsCommandVM.Sync(Towers, ordered.Count, _ => new FordsTowerBarVM());
        for (var i = 0; i < ordered.Count; i++)
            Towers[i].Update(ordered[i], own, string.Empty);
    }
}
