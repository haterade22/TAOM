using TAOM.Adapters;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// The daily market sweep (docs/features/armour-acquisition.md): every gated piece in a town's market that
/// the town's armoury does not allow today leaves it. That covers stock from before the gate, stock a
/// visiting armourer allowed after he leaves, AI lords selling loot, and pieces the player sells. Only
/// governed gated classes are touched: light and medium stock and ungoverned goods (a routed mount the
/// guaranteed-stock pass adds every day, say) never are. The roster adapter removes per stack, modifier
/// by modifier.
/// </summary>
public sealed class ArmourStockSweepService
{
    private readonly IArmourGateService _gate;
    private readonly ArmouryLevelService _levels;
    private readonly ITownRosterAdapter _rosters;

    public ArmourStockSweepService(IArmourGateService gate, ArmouryLevelService levels, ITownRosterAdapter rosters)
    {
        _gate = gate;
        _levels = levels;
        _rosters = rosters;
    }

    /// <summary>Removes the town's disallowed gated pieces; returns how many units left the market.</summary>
    public int SweepTown(string townId)
    {
        if (!_gate.IsActive || string.IsNullOrEmpty(townId))
            return 0;
        var level = _levels.GetTownLevel(townId);
        var removed = 0;
        foreach (var row in _rosters.EnumerateRosterById(townId))
        {
            var cls = _gate.GetClass(row.ItemId);
            if (cls == null || !ArmourClassRules.IsGated(cls.Value))
                continue;
            if (_gate.IsEligibleForMarket(row.ItemId, level))
                continue;
            if (_rosters.RemoveItemById(townId, row.ItemId, row.Count))
                removed += row.Count;
        }
        return removed;
    }
}
