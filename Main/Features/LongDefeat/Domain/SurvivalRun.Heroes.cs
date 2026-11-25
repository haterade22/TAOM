using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// The heroes of the shop (#1325): bought between waves, one of each alive, on the field from the next tick, each one more defender
/// under the garrison cap. A hero who has fallen is for sale again at once. The plan's free heroes (#1366) stand from the first tick with no purchase, and a fallen one is not for sale.
/// </summary>
internal sealed partial class SurvivalRun
{
    // A hero with no entry is for sale.
    private readonly Dictionary<string, HeroState> _heroes = new(StringComparer.Ordinal);
    private readonly List<HeroSpawn> _heroOrders = new();

    /// <summary>Spends the price at once; the hero spawns on the next tick, and the field answers through <see cref="HeroSpawned"/>.</summary>
    public HeroResult TryBuyHero(string heroId)
    {
        if (Result != null)
            return HeroResult.RunOver;
        if (!BetweenWaves)
            return HeroResult.NotOpen;

        var offer = _plan.Shop.Heroes.FirstOrDefault(h => h.Id == heroId);
        if (offer == null)
            return HeroResult.UnknownHero;
        if (_heroes.ContainsKey(offer.Id))
            return HeroResult.Alive;

        var price = FortPrice(offer.Price);
        if (price > _purse.Balance)
            return HeroResult.NoGold;
        if (_lastDefenders + PendingDefenders + 1 > _r.GarrisonCap)
            return HeroResult.GarrisonFull;
        if (!_purse.TrySpend(price, PurseSink.Heroes))
            return HeroResult.NoGold;

        _purchases.Add(new FortPurchase(_wave + 1, PurchaseKind.Hero, offer.Id, 0, offer.Price, price, 0f));
        _heroes[offer.Id] = HeroState.Pending;
        _heroOrders.Add(new HeroSpawn(offer.Id, offer.TroopId, offer.HealthFactor));
        _pendingEvents.Add(new RunEvent(RunEventKind.Bought, _wave, "hero=" + offer.Id));
        return HeroResult.Ok;
    }

    /// <summary>
    /// The field's answer to a spawn. A hero it could not spawn is paid back and his purchase dropped, so the purchases still sum to the
    /// sink, and he is for sale again.
    /// </summary>
    public void HeroSpawned(string heroId, bool ok)
    {
        if (!_heroes.TryGetValue(heroId, out var state) || state != HeroState.Pending)
            return;
        if (ok)
        {
            _heroes[heroId] = HeroState.Standing;
            return;
        }

        var bought = _purchases.FindLastIndex(p => p.Kind == PurchaseKind.Hero && p.Id == heroId);
        if (bought >= 0)
        {
            _purse.Refund(_purchases[bought].Price, PurseSink.Heroes);
            _purchases.RemoveAt(bought);
        }

        _heroes.Remove(heroId);

        if (IsMustLive(heroId) && Result == null)
            End(RunEndReason.Stalemate);
    }

    /// <summary>A standing hero left the field: he is for sale again. A fall is also a defender's loss; a rout is not.</summary>
    public void HeroDown(string heroId, bool loss)
    {
        if (!_heroes.TryGetValue(heroId, out var state) || state != HeroState.Standing)
            return;

        _heroes.Remove(heroId);
        if (!loss)
            return;

        DefenderDown();
        if (IsMustLive(heroId) && Result == null)
            End(RunEndReason.HeroFell);
    }

    private bool IsMustLive(string heroId) => _plan.MustLive != null && string.Equals(_plan.MustLive, heroId, StringComparison.Ordinal);

    private int PendingHeroes
    {
        get
        {
            var pending = 0;
            foreach (var state in _heroes.Values)
            {
                if (state == HeroState.Pending)
                    pending++;
            }

            return pending;
        }
    }

    private void EmitHeroes(RunEffects fx)
    {
        fx.SpawnHeroes.AddRange(_heroOrders);
        _heroOrders.Clear();
    }

    private void QueueFreeHeroes()
    {
        foreach (var hero in _plan.FreeHeroes)
        {
            if (_heroes.ContainsKey(hero.Id))
                continue;

            _heroes[hero.Id] = HeroState.Pending;
            _heroOrders.Add(hero);
        }
    }

    private IReadOnlyList<HeroRow> HeroRowsNow() => _plan.Shop.Heroes
        .Select(h => new HeroRow(h, FortPrice(h.Price), _heroes.TryGetValue(h.Id, out var state) ? state : HeroState.ForSale))
        .ToList();
}
