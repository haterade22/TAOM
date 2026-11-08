using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

internal sealed partial class FordsMatch
{
    /// <summary>
    /// The read model for one slot. An unknown slot gets a view with no gold, no lanes owned and no offers, so a panel
    /// that outlives its slot shows nothing instead of throwing.
    /// </summary>
    public MatchView GetView(int slot)
    {
        _slotsById.TryGetValue(slot, out var commander);
        var side = commander?.Side ?? MatchSide.West;
        _factions.TryGetValue(side, out var faction);
        var gold = _ledger.Balance(slot);
        var elapsed = _clock.Elapsed;

        var lanes = _layout.Lanes.Select(l => LaneViewOf(side, l.Index, commander, elapsed)).ToList();
        var keeps = _chain.Ids
            .Select(id => _chain.Slot(id)!)
            .Where(s => s.Kind == TowerKind.Keep)
            .Select(TowerViewOf)
            .ToList();
        var heroes = _heroes.Heroes
            .Select(h => new MatchView.HeroView(h.HeroId, h.Side, h.OwnerSlot, h.Lane, h.State,
                h.State == HeroState.Dead ? Math.Max(0f, h.RespawnAt - elapsed) : 0f,
                _heroes.BuybackPrice(h.Price, _clock.Minute), h.State == HeroState.Dead ? h.RespawnTotal : 0f,
                h.Side == side && CheckBuyback(slot, h.HeroId, out _) == CommandResult.Ok))
            .ToList();
        var unitOffers = commander == null
            ? new List<MatchView.UnitOfferView>()
            : UnitOffersOf(side)
                .Select(o => new MatchView.UnitOfferView(o.OfferId, o.TroopId, o.Role, o.PackSize, o.PackPrice, gold >= o.PackPrice))
                .ToList();
        var heroOffers = commander == null
            ? new List<MatchView.HeroOfferView>()
            : HeroOffersOf(side)
                .Select(o => new MatchView.HeroOfferView(o.HeroId, o.Price, gold >= o.Price, _heroes.Get(side, o.HeroId) != null))
                .ToList();
        var points = _capture.Points
            .Select(p => new MatchView.CapturePointView(p.PointId, p.Owner, p.Capturer, p.Progress, p.Contested, p.Rising))
            .ToList();

        var laneCap = _config.Waves.MaxLivePerLanePerSide + (faction?.FeatsOrNone.UnitCapBonus ?? 0) / Math.Max(1, _layout.Lanes.Count);
        var assaultThreshold = _config.Lanes.AssaultThreshold * laneCap / _config.Waves.MaxLivePerLanePerSide;

        return new MatchView(slot, side, _clock.Phase, elapsed, gold, assaultThreshold,
            commander == null ? 0 : Math.Max(0, _heroes.MaxAlive(side) - _heroes.AliveCount(side)),
            commander == null ? new List<int>() : commander.Lanes.ToList(),
            lanes, keeps, heroes, unitOffers, heroOffers, points, _winner, faction?.FactionId ?? "", faction?.Traits,
            commander == null ? null : RegionViewOf(side, gold), commander == null ? null : CreatureViewOf(slot, side, gold),
            (int)Math.Round(_config.Economy.IncomePerMinute * (faction?.FeatsOrNone.Income ?? 1f)), faction?.Name ?? "",
            _layout.Camps.Select(c => new MatchView.CampView(c.Id, _camps.Alive(c.Id), CampRewardFor(side, c.Reward))).ToList(), _orders.CooldownOf(side), faction?.AiBuyCycle, UpgradeViewsOf(side, gold), _factions.TryGetValue(MatchSides.Opponent(side), out var enemy) ? enemy.Name : "", commander == null ? null : NextFreeHeroIn(side), _endedOnTime);
    }

    private MatchView.RegionView? RegionViewOf(MatchSide side, int gold)
    {
        if (!_decks.TryGetValue(side, out var deck))
            return null;

        return new MatchView.RegionView(deck.Current.Id, deck.Current.Name, deck.SecondsLeft, deck.TotalSeconds, deck.Next.Id, deck.Next.Name, deck.CallFee,
            gold >= deck.CallFee, deck.Regions.Select(r => new MatchView.RegionChoiceView(r.Id, r.Name)).ToList());
    }

    private MatchView.CreatureCallView? CreatureViewOf(int slot, MatchSide side, int gold)
    {
        if (!_factions.TryGetValue(side, out var faction) || faction.Creatures == null)
            return null;

        var offers = faction.Creatures.Offers.Where(o => !_lostPacks.Contains((side, o.OfferId)))
            .Select(o => new MatchView.UnitOfferView(o.OfferId, o.TroopId, o.Role, o.PackSize, o.PackPrice, gold >= o.PackPrice)).ToList();
        return new MatchView.CreatureCallView(CreatureCooldownLeft(slot), faction.Creatures.CooldownSeconds, offers);
    }

    private MatchView.LaneView LaneViewOf(MatchSide side, int lane, CommanderSlot? commander, float now)
    {
        var enemy = MatchSides.Opponent(side);
        var towers = _chain.Ids
            .Select(id => _chain.Slot(id)!)
            .Where(s => s.Kind == TowerKind.Tower && s.Lane == lane)
            .OrderBy(s => s.Owner)
            .ThenBy(s => s.Order)
            .Select(TowerViewOf)
            .ToList();

        var near = PressureOf(side, lane);
        return new MatchView.LaneView(lane, commander != null && commander.Lanes.Contains(lane), _orders.Order(side, lane),
            _orders.CooldownLeft(side, lane, now), LiveInLane(side, lane), LiveInLane(enemy, lane), ThreatOn(side, lane), towers,
            _seize.HasOpen(side, lane), near.Own, near.Enemy, near.Count, near.AtTower);
    }

    /// <summary>Metres around a lane's lead within which units count as the fight the lane is in.</summary>
    internal const float PressureRadius = 40f;

    /// <summary>What a hero counts for in a pressure sum, about one strong elite: its price (800 to 2400) would make one hero force a regroup, or forbid one on the other side.</summary>
    internal const float HeroPressureValue = 250f;

    /// <summary>
    /// The value of the side's own units and the value and number of the other side's units, of any lane in both cases, within
    /// <see cref="PressureRadius"/> of the lane's lead, from the last tick's positions and the recorded unit values; whether the lead
    /// stands at the lane's own tower. All zero (and false) for a lane with no lead. A unit the match has no record of is worth 0.
    /// </summary>
    private (float Own, float Enemy, int Count, bool AtTower) PressureOf(MatchSide side, int lane)
    {
        if (!_seenLanes.TryGetValue((side, lane), out var seen) || seen.UnitPositions == null
            || _march.LeadOf(side, lane, seen.UnitPositions) is not { } lead)
            return default;

        float own = 0f;
        float enemy = 0f;
        var count = 0;
        foreach (var other in _seenLanes.Values)
        {
            if (other.UnitPositions == null)
                continue;

            for (var i = 0; i < other.UnitPositions.Count; i++)
            {
                if (!(other.UnitPositions[i].Distance2D(lead.Position) <= PressureRadius))
                    continue;

                if (other.Side == side)
                {
                    own += ValueOf(other, i);
                    continue;
                }

                enemy += ValueOf(other, i);
                count++;
            }
        }

        return (own, enemy, count, _march.IsAtOwnTower(side, lane, lead.Position));
    }

    private float ValueOf(LaneSnapshot lane, int index) =>
        lane.UnitKeys != null && index < lane.UnitKeys.Count && _units.TryGetValue(lane.UnitKeys[index], out var unit)
            ? (unit.Role == UnitRole.Hero ? HeroPressureValue : unit.Value)
            : 0f;

    private MatchView.TowerView TowerViewOf(TowerSlot slot) =>
        new(slot.Id, slot.Owner, slot.Lane, slot.Order, slot.Kind, _chain.HitPoints(slot.Id), _chain.MaxHitPoints(slot.Id),
            _chain.IsLocked(slot.Id), _chain.IsDestroyed(slot.Id));

    /// <summary>Enemies in reach of the lane's outermost standing tower of <paramref name="side"/>; the enemy lane count when none stands.</summary>
    private int ThreatOn(MatchSide side, int lane)
    {
        var tower = _chain.AliveTowers(side, lane).FirstOrDefault();
        if (tower == null)
            return LiveInLane(MatchSides.Opponent(side), lane);

        return _seenThreats.TryGetValue(tower.Id, out var count) ? count : 0;
    }
}
