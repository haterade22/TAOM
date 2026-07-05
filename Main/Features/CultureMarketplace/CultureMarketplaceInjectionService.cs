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

    public IReadOnlyList<string> SelectItems(string cultureId, int currentRosterCount, Random rng)
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

        var picks = new List<string>(drawCount);
        for (var i = 0; i < drawCount; i++)
        {
            var pick = WeightedDraw(pool, rng);
            if (pick != null)
                picks.Add(pick);
        }

        return picks;
    }

    private static string WeightedDraw(CultureItemPool pool, Random rng)
    {
        var roll = (float)(rng.NextDouble() * pool.TotalWeight);
        var cumulative = 0f;
        for (var i = 0; i < pool.Items.Count; i++)
        {
            cumulative += pool.Items[i].Weight;
            if (roll <= cumulative)
                return pool.Items[i].ItemId;
        }
        // Floating-point edge case — fall back to last item.
        return pool.Items.Count > 0 ? pool.Items[pool.Items.Count - 1].ItemId : null;
    }
}
