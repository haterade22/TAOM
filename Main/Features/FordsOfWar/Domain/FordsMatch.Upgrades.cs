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
        if (!_ledger.TrySpend(slot, price, GoldUse.Upgrades))
            return CommandResult.NotEnoughGold;

        _upgrades.TryRaise(owner.Side, trackId);
        _pendingEvents.Add(new MatchEvent(MatchEventKind.UpgradeBought, owner.Side, _upgrades.Tracks.First(t => t.Id == trackId).Name, price));
        return CommandResult.Ok;
    }

    /// <summary>An arrow or bolt struck a unit. A registered attacker of a faction with poison poisons a registered enemy; a tower gunner and a mount are not registered units.</summary>
    public void OnArrowHit(AgentKey victim, AgentKey attacker)
    {
        if (!_units.TryGetValue(attacker, out var shooter) || !_units.TryGetValue(victim, out var target) || shooter.Side == target.Side)
            return;

        if (_factions.TryGetValue(shooter.Side, out var faction) && faction.FeatsOrNone is { PoisonPerSecond: > 0f, PoisonSeconds: > 0f } feats)
            _poison.Apply(victim, attacker, shooter.Side, feats.PoisonPerSecond, feats.PoisonSeconds);
    }

    /// <summary>The poison doses each side landed and the damage they did, for the log at the end of a match.</summary>
    public (int Doses, long Damage) PoisonTotals(MatchSide side) => _poison.Totals(side);

    /// <summary>Every commander's gold so far, for the log at the end of a match.</summary>
    public IEnumerable<(MatchSide Side, int Slot, long Earned, long Spent, int Left, string Breakdown)> GoldTotals() =>
        _slots.Select(s => (s.Side, s.Id, _ledger.Earned(s.Id), _ledger.Spent(s.Id), _ledger.Balance(s.Id), _ledger.Breakdown(s.Id)));

    /// <summary>What this side's upgrades do to the blows of its agents and the blows they take.</summary>
    public CombatUpgradeFactors CombatUpgrades(MatchSide side) => _upgrades.CombatFactors(side);

    private List<MatchView.UpgradeView> UpgradeViewsOf(MatchSide side, int gold) =>
        _upgrades.Tracks.Select(t =>
        {
            var next = _upgrades.NextPrice(side, t.Id);
            return new MatchView.UpgradeView(t.Id, t.Name, t.Effect, t.StepPerLevel, _upgrades.Level(side, t.Id), t.Prices.Count, next, next != null && gold >= next);
        }).ToList();
}
