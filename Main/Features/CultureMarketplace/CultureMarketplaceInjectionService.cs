using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Features.CultureMarketplace;

public class CultureMarketplaceInjectionService : ICultureMarketplaceInjectionService
{
    private readonly ICultureItemPoolService _poolService;
    private readonly MarketplaceTuning _tuning;
    private readonly IModLogger _logger;

    // Log-hygiene: a pool-less culture is a static fact for the session (pools build once),
    // so warn only the first time each culture is seen, not on every daily tick.
    private readonly HashSet<string> _warnedMissingPools = new(StringComparer.OrdinalIgnoreCase);

    public CultureMarketplaceInjectionService(
        ICultureItemPoolService poolService,
        MarketplaceTuning tuning,
        IModLogger logger)
    {
        _poolService = poolService;
        _tuning = tuning;
        _logger = logger;
    }

    public IReadOnlyList<string> SelectItems(string cultureId, int currentRosterCount, Random rng, Func<string, bool> isEligible = null)
    {
        if (string.IsNullOrEmpty(cultureId))
            return Array.Empty<string>();

        if (rng == null)
            throw new ArgumentNullException(nameof(rng));

        if (currentRosterCount >= _tuning.PerTownTotalRosterCap)
        {
            // Roster already at or above cap — skip this tick.
            return Array.Empty<string>();
        }

        var pool = _poolService.GetPool(cultureId);
        if (pool == null || pool.Items.Count == 0 || pool.TotalWeight <= 0f)
        {
            if (_warnedMissingPools.Add(cultureId))
                _logger.LogDebug($"[CultureMarketplace] No pool for culture '{cultureId}' — no items injected");
            return Array.Empty<string>();
        }

        var headroom = _tuning.PerTownTotalRosterCap - currentRosterCount;
        var drawCount = Math.Min(_tuning.ItemsPerTownPerDay, headroom);
        if (drawCount <= 0)
            return Array.Empty<string>();

        // The town's filter narrows the pool (armour acquisition): filter once, then draw. Weights are summed
        // in pool order, as CultureItemPool.TotalWeight is, so a filter that keeps everything draws exactly
        // as no filter does.
        IReadOnlyList<ItemPoolEntry> eligible = pool.Items;
        var totalWeight = pool.TotalWeight;
        if (isEligible != null)
        {
            var kept = new List<ItemPoolEntry>(pool.Items.Count);
            totalWeight = 0f;
            for (var i = 0; i < pool.Items.Count; i++)
            {
                var entry = pool.Items[i];
                if (!isEligible(entry.ItemId)) continue;
                kept.Add(entry);
                totalWeight += entry.Weight;
            }
            eligible = kept;
        }
        if (eligible.Count == 0 || !(totalWeight > 0f))
            return Array.Empty<string>();

        var picks = new List<string>(drawCount);
        for (var i = 0; i < drawCount; i++)
        {
            var pick = WeightedDraw(eligible, totalWeight, rng);
            if (pick != null)
                picks.Add(pick);
        }

        return picks;
    }

    private static string WeightedDraw(IReadOnlyList<ItemPoolEntry> items, float totalWeight, Random rng)
    {
        var roll = (float)(rng.NextDouble() * totalWeight);
        var cumulative = 0f;
        for (var i = 0; i < items.Count; i++)
        {
            cumulative += items[i].Weight;
            if (roll <= cumulative)
                return items[i].ItemId;
        }
        // Floating-point edge case — fall back to last item.
        return items.Count > 0 ? items[items.Count - 1].ItemId : null;
    }
}
