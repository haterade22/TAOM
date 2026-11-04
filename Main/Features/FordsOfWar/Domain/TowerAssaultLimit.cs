using System;
using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// How much damage ordinary blows may do to one tower each second. Without it a tower's fall time shrinks with every body at its walls, so the cheapest pack is the best
/// tower killer in the game. Each tower holds at most one second's worth, so a long quiet earns no burst. A rate of 0 means no limit.
/// </summary>
internal sealed class TowerAssaultLimit
{
    private readonly float _perSecond;
    private readonly Dictionary<string, (float Budget, float At)> _towers = new();

    public TowerAssaultLimit(float perSecond)
    {
        _perSecond = perSecond;
    }

    /// <summary>The share of this blow that the tower's budget lets through at match time <paramref name="now"/>, spent from the budget. <paramref name="wear"/> raises the rate, so the limit does not hold weakened walls back.</summary>
    public int Take(string towerId, int damage, float now, float wear = 1f)
    {
        if (!(_perSecond > 0f))
            return damage;

        var rate = _perSecond * wear;
        var budget = rate;
        if (_towers.TryGetValue(towerId, out var last))
            budget = Math.Min(rate, last.Budget + Math.Max(0f, now - last.At) * rate);

        var taken = Math.Min(damage, (int)budget);
        _towers[towerId] = (budget - taken, now);
        return taken;
    }
}
