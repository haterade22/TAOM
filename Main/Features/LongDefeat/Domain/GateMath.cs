using System;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// The gate arithmetic (phase3-plan.md D17 and D18): the damage-state index the engine computes, the hit points a level write leaves, the levelled maximum and the repair price.
/// Pure. The engine's state index only has to stay or rise from one hit to the next, or the gate's next hit reads a state of -1 and throws (E9b).
/// </summary>
internal static class GateMath
{
    /// <summary>
    /// The state index <c>DestructableComponent.CalculateNextDestructionLevel</c> gives a gate with <paramref name="states"/> damage states (v1.5.6, <c>DestructableComponent.cs:405-418</c>):
    /// the number of whole steps of <c>max / states</c> the hit points have fallen past, in 32-bit float as the engine does. A maximum that is not a positive finite number, no states, or hit points that are
    /// not a number give 0, so a reading the engine never made cannot loop.
    /// </summary>
    public static int StateIndex(float hitPoint, float maxHitPoint, int states)
    {
        if (states <= 0 || !(maxHitPoint > 0f) || float.IsInfinity(maxHitPoint) || float.IsNaN(hitPoint))
            return 0;

        var step = maxHitPoint / (float)states;
        var left = maxHitPoint;
        var index = 0;
        while (index <= states && left - step >= hitPoint)
        {
            left -= step;
            index++;
        }

        return index;
    }

    /// <summary>The maximum of a level: the vanilla maximum times one plus the level's cumulative percent over 100.</summary>
    public static float LevelledMax(float vanillaMax, float hitPointsPercent) => vanillaMax * (1f + hitPointsPercent / 100f);

    /// <summary>
    /// The hit points to write when the maximum rises from <paramref name="oldMax"/> to <paramref name="newMax"/> on a standing gate: its share of the maximum, less one hit point (the one is left off a
    /// gate on its last hit point, so it stays alive). The share can only fall by that point, so the state index of the next hit can only stay or rise (E9b); a level never heals the share that was missing.
    /// A broken gate (0) stays 0: a level raises only its maximum.
    /// </summary>
    public static float KeepShare(float hitPoint, float oldMax, float newMax)
    {
        if (!(hitPoint > 0f) || !(oldMax > 0f))
            return 0f;

        var scaled = hitPoint * newMax / oldMax;
        return hitPoint > 1f ? scaled - 1f : scaled;
    }

    /// <summary>
    /// What mending a gate costs: the repair price (already through the price factor) times the share of the gate that is missing, rounded up, at least 1. A broken gate has share 1 and pays all of it.
    /// The product is rounded to three decimals before the ceiling, so the float noise of a share like 0.37 (0.3700000048) does not cost a gold piece.
    /// </summary>
    public static int RepairPrice(int priceAfterFactor, float missingShare)
    {
        var share = float.IsNaN(missingShare) ? 0.0 : Math.Max(0.0, Math.Min(1.0, missingShare));
        var owed = Math.Ceiling(Math.Round(Math.Max(1, priceAfterFactor) * share, 3));
        return (int)Math.Max(1.0, owed);
    }
}
