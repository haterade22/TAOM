using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// Upgrading the soldiers the player has (#1161). The director reads the standing defenders by troop between waves (<see cref="ObserveStanding"/>); a wave forgets that read, because soldiers die in
/// it. A buy takes every standing soldier of the step's troop that is not yet sold and that the gold pays for, spends at once, and goes out on the next tick as an <see cref="UpgradeOrder"/>; the
/// director books what the field replaced with <see cref="CompleteUpgrade"/>, which pays back the rest. Upgrade prices take no boon price factor.
/// </summary>
internal sealed partial class SurvivalRun
{
    private readonly Dictionary<string, int> _standingByTroop = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<UpgradeOrder> _upgrades = new();

    /// <summary>The standing defenders by troop id, read from the field between waves. A count below 0 is read as 0.</summary>
    public void ObserveStanding(IReadOnlyDictionary<string, int> byTroop)
    {
        _standingByTroop.Clear();
        foreach (var pair in byTroop)
            _standingByTroop[pair.Key] = Math.Max(0, pair.Value) + (_standingByTroop.TryGetValue(pair.Key, out var had) ? had : 0);
    }

    public UpgradeResult TryUpgrade(string stepId)
    {
        if (Result != null)
            return UpgradeResult.RunOver;
        if (!BetweenWaves)
            return UpgradeResult.NotOpen;

        var step = _plan.Shop.Upgrades.FirstOrDefault(s => s.Id == stepId);
        if (step == null)
            return UpgradeResult.UnknownStep;

        var free = Unsold(step.FromTroop);
        if (free <= 0)
            return UpgradeResult.NoneStanding;

        var count = Math.Min(free, _purse.Balance / step.Price);
        if (count <= 0 || !_purse.TrySpend(count * step.Price, PurseSink.Upgrades))
            return UpgradeResult.NoGold;

        _upgrades.Add(new UpgradeOrder(step, count, _wave + 1));
        _pendingEvents.Add(new RunEvent(RunEventKind.Bought, _wave, $"upgrade={step.Id} n={count}"));
        return UpgradeResult.Ok;
    }

    /// <summary>
    /// Books an order the field carried out: one purchase of the soldiers it replaced (none when it replaced nobody), the gold of the rest paid back, and the read moved from the old troop to the
    /// new one so the view stays right until the next read. A count outside 0 to the order's count is clamped; returns the count booked.
    /// </summary>
    public int CompleteUpgrade(UpgradeOrder order, int replaced)
    {
        var done = Math.Max(0, Math.Min(order.Count, replaced));
        var price = order.Step.Price;
        if (done > 0)
            _purchases.Add(new FortPurchase(order.BeforeWave, PurchaseKind.Upgrade, order.Step.Id, 0, price, price * done, 0f) { Count = done });
        _purse.Refund((order.Count - done) * price, PurseSink.Upgrades);

        var from = order.Step.FromTroop;
        _standingByTroop[from] = Math.Max(0, (_standingByTroop.TryGetValue(from, out var left) ? left : 0) - done);
        _standingByTroop[order.Step.ToTroop] = (_standingByTroop.TryGetValue(order.Step.ToTroop, out var had) ? had : 0) + done;
        return done;
    }

    private void EmitUpgrades(RunEffects fx)
    {
        foreach (var order in _upgrades)
            fx.Upgrades.Add(order with { HealthFactor = UpgradeHealth(order.Step) });
        _upgrades.Clear();
    }

    private float UpgradeHealth(UpgradeStep step) => DefenderHealth(new WaveSoldier(step.ToTroop, SlotOfLine(step.FromTroop)));

    private void ForgetStanding() => _standingByTroop.Clear();

    private IReadOnlyList<UpgradeRow> UpgradeRowsNow()
    {
        if (!BetweenWaves || Result != null || _plan.Shop.Upgrades.Count == 0)
            return Array.Empty<UpgradeRow>();

        var rows = new List<UpgradeRow>();
        foreach (var step in _plan.Shop.Upgrades)
        {
            var free = Unsold(step.FromTroop);
            if (free > 0)
                rows.Add(new UpgradeRow(step, free, Math.Min(free, _purse.Balance / step.Price)));
        }

        return rows;
    }

    /// <summary>The soldiers of a troop that stand and that no order still waiting to go out has taken.</summary>
    private int Unsold(string troopId)
    {
        var standing = _standingByTroop.TryGetValue(troopId, out var count) ? count : 0;
        var sold = _upgrades.Where(o => string.Equals(o.Step.FromTroop, troopId, StringComparison.OrdinalIgnoreCase)).Sum(o => o.Count);
        return Math.Max(0, standing - sold);
    }

    /// <summary>The slot of a line's first troop, walked back through the steps to a garrison row or a pack; infantry when none names it. Only the health factor's group fallback reads it.</summary>
    private FormationSlot SlotOfLine(string troopId)
    {
        var troop = troopId;
        for (var i = 0; i <= _plan.Shop.Upgrades.Count; i++)
        {
            var row = _plan.Garrison.FirstOrDefault(g => string.Equals(g.TroopId, troop, StringComparison.OrdinalIgnoreCase));
            if (row != null)
                return row.Slot;
            var offer = _plan.Shop.Offers.FirstOrDefault(o => string.Equals(o.TroopId, troop, StringComparison.OrdinalIgnoreCase));
            if (offer != null)
                return offer.Slot;
            var before = _plan.Shop.Upgrades.FirstOrDefault(s => string.Equals(s.ToTroop, troop, StringComparison.OrdinalIgnoreCase));
            if (before == null)
                break;
            troop = before.FromTroop;
        }

        return FormationSlot.Infantry;
    }
}
