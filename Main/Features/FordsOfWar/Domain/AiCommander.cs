using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// The computer commander. It reads the same <see cref="MatchView"/> a human panel does and answers with
/// <see cref="AiAction"/>s that the match applies through the human Try* methods. Every DecisionIntervalSeconds it
/// defends the worst threatened lane with the cheapest spear or infantry pack, buys back or fields heroes when gold
/// beats price plus HeroReserveGold, then builds its best lane on Hold with the dearest pack it can pay until the lane
/// holds AssaultThreshold units, and pushes. MistakeChance skips a whole decision. Gold it plans to spend is tracked
/// locally, so one decision never asks for more than the slot owns.
/// </summary>
internal sealed class AiCommander
{
    /// <summary>An assault pushes on until the lane holds less than this fraction of AssaultThreshold.</summary>
    private const float ReleaseFraction = 0.35f;

    /// <summary>A hero is bought back only with more than this share of its respawn delay left (and above the floor).</summary>
    private const float BuybackMinLeftFraction = 0.25f;

    private const float BuybackMinLeftSeconds = 10f;

    private readonly FordsAiProfile _profile;
    private readonly IFordsRandom _random;
    private readonly Dictionary<int, string> _assaultTargets = new();
    private float _nextDecisionAt;

    public AiCommander(int slot, FordsAiProfile profile, IFordsRandom random)
    {
        Slot = slot;
        _profile = profile;
        _random = random;
        _nextDecisionAt = profile.DecisionIntervalSeconds;
    }

    public int Slot { get; }

    public IReadOnlyList<AiAction> Decide(MatchView view, float now)
    {
        var actions = new List<AiAction>();
        if (now < _nextDecisionAt)
            return actions;

        _nextDecisionAt = now + _profile.DecisionIntervalSeconds;
        if (view.Phase != MatchPhase.Live)
            return actions;
        if (_profile.MistakeChance > 0f && _random.NextFloat() < _profile.MistakeChance)
            return actions;

        var lanes = view.Lanes.Where(l => l.Owned).ToList();
        if (lanes.Count == 0)
            return actions;

        var gold = view.Gold;
        gold -= Defend(view, lanes, gold, actions);
        var assault = PickAssaultLane(lanes);
        gold -= FieldHeroes(view, lanes, assault, gold, actions);
        SetOrders(view, lanes, assault, actions);
        BuildAssault(view, assault, gold, actions);
        return actions;
    }

    /// <summary>Buys the cheapest spear or infantry pack in the lane where the enemy outnumbers us the most. Returns the gold planned.</summary>
    private static int Defend(MatchView view, List<MatchView.LaneView> lanes, int gold, List<AiAction> actions)
    {
        var worst = lanes
            .Where(l => l.Threat > l.OwnLive)
            .OrderByDescending(l => l.Threat - l.OwnLive)
            .ThenBy(l => l.Index)
            .FirstOrDefault();
        if (worst == null)
            return 0;

        var pack = view.UnitOffers
            .Where(o => (o.Role == UnitRole.Spear || o.Role == UnitRole.Infantry) && o.Price <= gold)
            .OrderBy(o => o.Price)
            .ThenBy(o => o.TroopId, StringComparer.Ordinal)
            .ThenBy(o => o.OfferId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (pack == null)
            return 0;

        actions.Add(AiAction.BuyPack(worst.Index, pack.OfferId));
        return pack.Price;
    }

    /// <summary>The lane we are not losing with the best own-to-enemy ratio; the lowest index wins a tie.</summary>
    private static MatchView.LaneView? PickAssaultLane(List<MatchView.LaneView> lanes) =>
        lanes
            .Where(l => l.Threat <= l.OwnLive)
            .OrderByDescending(l => (l.OwnLive + 1f) / (l.EnemyLive + 1f))
            .ThenBy(l => l.Index)
            .FirstOrDefault();

    private int FieldHeroes(MatchView view, List<MatchView.LaneView> lanes, MatchView.LaneView? assault, int gold, List<AiAction> actions)
    {
        var spent = 0;
        var slotsFree = view.HeroSlotsFree;
        var reserve = _profile.HeroReserveGold;

        var dead = view.Heroes
            .Where(h => h.Side == view.Side && h.OwnerSlot == view.Slot && h.State == HeroState.Dead
                && h.CanBuyback && h.RespawnLeft > Math.Max(BuybackMinLeftSeconds, h.RespawnTotal * BuybackMinLeftFraction))
            .OrderBy(h => h.BuybackPrice)
            .ThenBy(h => h.HeroId, StringComparer.Ordinal);
        foreach (var hero in dead)
        {
            if (slotsFree <= 0 || gold - spent <= hero.BuybackPrice + reserve)
                continue;

            actions.Add(AiAction.Buyback(hero.HeroId));
            spent += hero.BuybackPrice;
            slotsFree--;
        }

        if (slotsFree <= 0)
            return spent;

        var left = gold - spent;
        var offer = view.HeroOffers
            .Where(h => !h.Fielded && left > h.Price + reserve)
            .OrderByDescending(h => h.Price)
            .ThenBy(h => h.HeroId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (offer == null)
            return spent;

        actions.Add(AiAction.BuyHero((assault ?? lanes[0]).Index, offer.HeroId));
        return spent + offer.Price;
    }

    /// <summary>
    /// Hold while the assault lane builds up, Push otherwise. Reaching the threshold latches the assault for that lane: it
    /// keeps Push until the lane's own count drops below <see cref="ReleaseFraction"/> of the threshold or the tower it was
    /// sent at falls. Only flips Push and Hold, and only off cooldown.
    /// </summary>
    private void SetOrders(MatchView view, List<MatchView.LaneView> lanes, MatchView.LaneView? assault, List<AiAction> actions)
    {
        foreach (var lane in lanes)
        {
            if (_assaultTargets.TryGetValue(lane.Index, out var target) && ShouldRelease(view, lane, target))
                _assaultTargets.Remove(lane.Index);

            var isAssault = assault != null && lane.Index == assault.Index;
            if (isAssault && lane.OwnLive >= view.AssaultThreshold && !_assaultTargets.ContainsKey(lane.Index))
                _assaultTargets[lane.Index] = NextEnemyTowerId(view, lane);

            var building = isAssault && lane.OwnLive < view.AssaultThreshold && !_assaultTargets.ContainsKey(lane.Index);
            var wanted = building ? LaneOrder.Hold : LaneOrder.Push;
            if (lane.Order != wanted && lane.Order != LaneOrder.FallBack && lane.CooldownLeft <= 0f)
                actions.Add(AiAction.SetOrder(lane.Index, wanted));
        }
    }

    private static bool ShouldRelease(MatchView view, MatchView.LaneView lane, string targetTowerId)
    {
        if (lane.OwnLive < view.AssaultThreshold * ReleaseFraction)
            return true;

        return targetTowerId.Length > 0 && lane.Towers.Any(t => t.Id == targetTowerId && t.Destroyed);
    }

    /// <summary>The enemy's outermost standing tower in the lane; empty when only its keep is left to attack.</summary>
    private static string NextEnemyTowerId(MatchView view, MatchView.LaneView lane) =>
        lane.Towers
            .Where(t => t.Owner != view.Side && t.Kind == TowerKind.Tower && !t.Destroyed)
            .OrderBy(t => t.Order)
            .Select(t => t.Id)
            .FirstOrDefault() ?? string.Empty;

    private static void BuildAssault(MatchView view, MatchView.LaneView? assault, int gold, List<AiAction> actions)
    {
        if (assault == null || assault.OwnLive >= view.AssaultThreshold)
            return;

        var pack = view.UnitOffers
            .Where(o => o.Role != UnitRole.Hero && o.Price <= gold)
            .OrderByDescending(o => o.Price)
            .ThenBy(o => o.TroopId, StringComparer.Ordinal)
            .ThenBy(o => o.OfferId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (pack != null)
            actions.Add(AiAction.BuyPack(assault.Index, pack.OfferId));
    }
}
