using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.TrainingGrounds.Waves.Domain;

/// <summary>A troop of the town's own pool: its id and its tier.</summary>
internal sealed record PoolTroop(string Id, int Tier);

/// <summary>Draws the fighters of one wave from the town's troops: a tier by weight, then a troop of that tier.</summary>
internal static class WaveRoster
{
    private const int MaxWeight = 1_000_000;

    private static readonly int[] Tiers = { 3, 4, 5 };

    /// <param name="below">A draw from 0 to n - 1 for the n it is given (a seeded source, or the engine's); a value outside is clamped.</param>
    /// <returns>
    /// <paramref name="count"/> troop ids. A tier the pool lacks falls back to the nearest lower tier it has, then the nearest higher. An
    /// empty pool, a count of 0 or less, or weights that are all 0 give an empty list. Each weight is held to a million, so the three sum inside an int.
    /// </returns>
    public static IReadOnlyList<string> Build(IReadOnlyList<PoolTroop> pool, IReadOnlyDictionary<int, int> tierWeights, int count, Func<int, int> below)
    {
        var result = new List<string>();
        if (pool.Count == 0 || count <= 0)
            return result;

        var weights = Tiers.Select(t => tierWeights.TryGetValue(t, out var w) && w > 0 ? Math.Min(w, MaxWeight) : 0).ToArray();
        var total = weights.Sum();
        if (total == 0)
            return result;

        for (var i = 0; i < count; i++)
        {
            var tier = DrawTier(weights, total, below);
            var group = pool.Where(p => p.Tier == NearestTierPresent(pool, tier)).ToList();
            result.Add(group[Clamp(below(group.Count), group.Count)].Id);
        }

        return result;
    }

    private static int DrawTier(int[] weights, int total, Func<int, int> below)
    {
        var roll = Clamp(below(total), total);
        for (var i = 0; i < weights.Length; i++)
        {
            if (roll < weights[i])
                return Tiers[i];
            roll -= weights[i];
        }

        return Tiers[weights.Length - 1];
    }

    private static int NearestTierPresent(IReadOnlyList<PoolTroop> pool, int tier)
    {
        if (pool.Any(p => p.Tier == tier))
            return tier;

        var lower = pool.Where(p => p.Tier < tier).Select(p => p.Tier).DefaultIfEmpty(int.MinValue).Max();
        if (lower != int.MinValue)
            return lower;
        return pool.Where(p => p.Tier > tier).Min(p => p.Tier);
    }

    private static int Clamp(int draw, int n) => Math.Min(n - 1, Math.Max(0, draw));
}
