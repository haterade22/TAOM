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

        return new MatchView(slot, side, _clock.Phase, elapsed, gold, _config.Lanes.AssaultThreshold,
            commander == null ? 0 : Math.Max(0, _config.Heroes.MaxAlivePerSide - _heroes.AliveCount(side)),
            commander == null ? new List<int>() : commander.Lanes.ToList(),
            lanes, keeps, heroes, unitOffers, heroOffers, points, _winner);
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

        return new MatchView.LaneView(lane, commander != null && commander.Lanes.Contains(lane), _orders.Order(side, lane),
            _orders.CooldownLeft(side, lane, now), LiveInLane(side, lane), LiveInLane(enemy, lane), ThreatOn(side, lane), towers);
    }

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
