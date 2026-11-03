using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// The computer commander. It reads the same <see cref="MatchView"/> a human panel does and answers with
/// <see cref="AiAction"/>s that the match applies through the human Try* methods. Every DecisionIntervalSeconds it
/// defends the worst threatened lane with the cheapest spear or infantry pack, buys back or fields heroes when gold
/// beats price plus HeroReserveGold, then builds its best lane on Hold with the dearest pack it can pay until the lane
/// holds AssaultThreshold units, and pushes. A faction's buy cycle also names its upgrades, its beasts and when to save
/// for a hero; a creature comes only from there. MistakeChance skips a whole decision. Gold it plans to spend is tracked
/// locally, so one decision never asks for more than the slot owns.
/// </summary>
internal sealed class AiCommander
{
    /// <summary>An assault pushes on until the lane holds less than this fraction of AssaultThreshold.</summary>
    private const float ReleaseFraction = 0.35f;

    /// <summary>A lane smaller than this does not go off to seize an objective: two creatures would eat it.</summary>
    private const int SeizeMinUnits = 12;

    /// <summary>A hero is bought back only with more than this share of its respawn delay left (and above the floor).</summary>
    private const float BuybackMinLeftFraction = 0.25f;

    private const float BuybackMinLeftSeconds = 10f;

    private readonly FordsAiProfile _profile;
    private readonly IFordsRandom _random;
    private readonly Dictionary<int, string> _assaultTargets = new();
    private const string UpgradeEntry = "up:";
    private const string BeastEntry = "beast:";
    private const string HeroEntry = "hero";

    private float _nextDecisionAt;
    private int _buyIndex;

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
    /// sent at falls. One lane outside the assault at a time seizes an open objective when no enemy is at its tower; the match
    /// returns it to Push once the objective is held, and it breaks off early when the enemy outnumbers it at its tower. Never
    /// overrides Fall back, and only off cooldown.
    /// </summary>
    private void SetOrders(MatchView view, List<MatchView.LaneView> lanes, MatchView.LaneView? assault, List<AiAction> actions)
    {
        var seizing = lanes.Any(l => l.Order == LaneOrder.Seize);
        foreach (var lane in lanes)
        {
            if (lane.Order == LaneOrder.Seize)
            {
                if (lane.Threat > lane.OwnLive && lane.CooldownLeft <= 0f)
                    actions.Add(AiAction.SetOrder(lane.Index, LaneOrder.Push));
                continue;
            }

            if (_assaultTargets.TryGetValue(lane.Index, out var target) && ShouldRelease(view, lane, target))
                _assaultTargets.Remove(lane.Index);

            var isAssault = assault != null && lane.Index == assault.Index;
            if (isAssault && lane.OwnLive >= view.AssaultThreshold && !_assaultTargets.ContainsKey(lane.Index))
                _assaultTargets[lane.Index] = NextEnemyTowerId(view, lane);

            var building = isAssault && lane.OwnLive < view.AssaultThreshold && !_assaultTargets.ContainsKey(lane.Index);
            var wanted = building ? LaneOrder.Hold : LaneOrder.Push;
            if (!seizing && !isAssault && !_assaultTargets.ContainsKey(lane.Index) && lane.CanSeize && lane.Threat <= 0
                && lane.OwnLive >= SeizeMinUnits && lane.Order != LaneOrder.FallBack && lane.CooldownLeft <= 0f)
            {
                wanted = LaneOrder.Seize;
                seizing = true;
            }

            if (lane.Order != wanted && lane.Order != LaneOrder.FallBack && lane.CooldownLeft <= 0f)
                actions.Add(AiAction.SetOrder(lane.Index, wanted));
        }
    }

    /// <summary>
    /// The upgrade, beast or hero when the cycle's next entry names one, with its price: moves past an upgrade at its top level, a beast whose timer still runs,
    /// and a hero with no slot free or none left to field. Null when the next entry is a pack. A hero entry names the cheapest, since the richer path is <see cref="FieldHeroes"/>.
    /// </summary>
    private (int Price, AiAction Action)? SpecialDue(MatchView view, int lane, IReadOnlyList<string> cycle)
    {
        for (var step = 0; step < cycle.Count; step++)
        {
            var entry = cycle[_buyIndex % cycle.Count];
            if (entry.StartsWith(UpgradeEntry, StringComparison.Ordinal))
            {
                var id = entry.Substring(UpgradeEntry.Length);
                var upgrade = view.Upgrades?.FirstOrDefault(u => u.Id == id);
                if (upgrade?.NextPrice is { } price)
                    return (price, AiAction.BuyUpgrade(upgrade.Id));
            }
            else if (entry.StartsWith(BeastEntry, StringComparison.Ordinal))
            {
                var id = entry.Substring(BeastEntry.Length);
                var beast = view.Creatures is { CooldownLeft: <= 0f } call ? call.Offers.FirstOrDefault(o => o.OfferId == id) : null;
                if (beast != null)
                    return (beast.Price, AiAction.CallCreature(lane, beast.OfferId));
            }
            else if (entry == HeroEntry)
            {
                var hero = view.HeroSlotsFree > 0 ? view.HeroOffers.Where(h => !h.Fielded).OrderBy(h => h.Price).ThenBy(h => h.HeroId, StringComparer.Ordinal).FirstOrDefault() : null;
                if (hero != null)
                    return (hero.Price, AiAction.BuyHero(lane, hero.HeroId));
            }
            else
            {
                return null;
            }

            _buyIndex++;
        }

        return null;
    }

    /// <summary>The next entry that has something on show, moving the place in the cycle past any that has not (a rotating faction's role may be absent for a while).</summary>
    private MatchView.UnitOfferView? NextInCycle(MatchView view, IReadOnlyList<string> cycle, int gold)
    {
        for (var step = 0; step < cycle.Count; step++)
        {
            var offer = OfferFor(view, cycle[(_buyIndex + step) % cycle.Count], gold);
            if (offer != null)
            {
                _buyIndex += step;
                return offer;
            }
        }

        return null;
    }

    /// <summary>An entry is an offer id, or a role name: the dearest of that role the gold reaches, else the cheapest of it, to save for.</summary>
    private static MatchView.UnitOfferView? OfferFor(MatchView view, string entry, int gold)
    {
        var exact = view.UnitOffers.FirstOrDefault(o => o.OfferId == entry);
        if (exact != null)
            return exact;

        var ofRole = view.UnitOffers.Where(o => o.Role.ToString() == entry).OrderByDescending(o => o.Price).ThenBy(o => o.OfferId, StringComparer.Ordinal).ToList();
        return ofRole.FirstOrDefault(o => o.Price <= gold) ?? ofRole.LastOrDefault();
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

    /// <summary>
    /// One pack for the assault lane. A faction with a buy cycle buys its next entry, and saves up when that costs more than the gold: a horde keeps
    /// buying cheap packs and a cavalry army keeps some foot, where the dearest offer alone would do neither. Without a cycle it buys the dearest it can afford.
    /// An upgrade, beast or hero still comes while the lane is full: a small faction's lane fills early, and its gold would sit unspent.
    /// </summary>
    private void BuildAssault(MatchView view, MatchView.LaneView? assault, int gold, List<AiAction> actions)
    {
        if (assault == null)
            return;

        if (view.AiBuyCycle is { Count: > 0 } cycle)
        {
            if (SpecialDue(view, assault.Index, cycle) is { } due)
            {
                if (due.Price <= gold)
                {
                    actions.Add(due.Action);
                    _buyIndex++;
                }

                return;
            }

            if (assault.OwnLive >= view.AssaultThreshold)
                return;

            var next = NextInCycle(view, cycle, gold);
            if (next != null && next.Price <= gold)
            {
                actions.Add(AiAction.BuyPack(assault.Index, next.OfferId));
                _buyIndex++;
            }

            return;
        }

        if (assault.OwnLive >= view.AssaultThreshold)
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
