using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

internal sealed partial class FordsMatch
{
    /// <summary>Buys the track's next level for the slot's whole side. A level lasts for the match and also reaches the units already on the field.</summary>
    public CommandResult TryBuyUpgrade(int slot, string trackId)
    {
        if (!_slotsById.TryGetValue(slot, out var owner))
            return CommandResult.LaneNotOwned;
        if (_clock.Phase == MatchPhase.Ended)
            return CommandResult.NotLive;
        if (!_upgrades.Knows(trackId))
            return CommandResult.UnknownOffer;
        if (!(_upgrades.NextPrice(owner.Side, trackId) is { } price))
            return CommandResult.UpgradeComplete;
        if (!_ledger.TrySpend(slot, price))
            return CommandResult.NotEnoughGold;

        _upgrades.TryRaise(owner.Side, trackId);
        _pendingEvents.Add(new MatchEvent(MatchEventKind.UpgradeBought, owner.Side, _upgrades.Tracks.First(t => t.Id == trackId).Name, price));
        return CommandResult.Ok;
    }

    /// <summary>Every commander's gold so far, for the log at the end of a match.</summary>
    public IEnumerable<(MatchSide Side, int Slot, long Earned, long Spent, int Left)> GoldTotals() =>
        _slots.Select(s => (s.Side, s.Id, _ledger.Earned(s.Id), _ledger.Spent(s.Id), _ledger.Balance(s.Id)));

    /// <summary>What this side's upgrades do to the blows of its agents and the blows they take.</summary>
    public CombatUpgradeFactors CombatUpgrades(MatchSide side) => _upgrades.CombatFactors(side);

    private List<MatchView.UpgradeView> UpgradeViewsOf(MatchSide side, int gold) =>
        _upgrades.Tracks.Select(t =>
        {
            var next = _upgrades.NextPrice(side, t.Id);
            return new MatchView.UpgradeView(t.Id, t.Name, t.Effect, t.StepPerLevel, _upgrades.Level(side, t.Id), t.Prices.Count, next, next != null && gold >= next);
        }).ToList();
}
