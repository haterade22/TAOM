using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.Roster;

namespace TAOM.Features.TroopWeight.Hooks;

public class TroopRosterTotalManCountHook : IOnTroopRosterTotalManCount
{
    private readonly ITroopWeightService _troopWeightService;
    private readonly IModLogger _logger;
    private readonly Dictionary<int, (int Version, float WeightedCount)> _cache;
    private const int CleanupThreshold = 500;

    public TroopRosterTotalManCountHook(ITroopWeightService troopWeightService, IModLogger logger)
    {
        _troopWeightService = troopWeightService;
        _logger = logger;
        _cache = new Dictionary<int, (int, float)>();
    }

    public void OnTroopRosterTotalManCount(TroopRoster troopRoster, ref int __result)
    {
        try
        {
            if (troopRoster == null)
                return;

            int cacheKey = troopRoster.GetHashCode();
            int currentVersion = troopRoster.VersionNo;

            if (_cache.TryGetValue(cacheKey, out var cached) && cached.Version == currentVersion)
            {
                var cachedResult = (int)Math.Ceiling(cached.WeightedCount);
                if (cachedResult > __result)
                    __result = cachedResult;
                return;
            }

            var weightedCount = _troopWeightService.CalculateWeightedRosterCount(troopRoster);

            _cache[cacheKey] = (currentVersion, weightedCount);

            if (weightedCount > __result)
                __result = (int)Math.Ceiling(weightedCount);

            if (_cache.Count > CleanupThreshold)
                TrimCache();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"TroopRoster.TotalManCount hook error: {ex.Message}");
        }
    }

    private void TrimCache()
    {
        var keysToRemove = new List<int>(_cache.Count / 4);
        int count = 0;
        foreach (var key in _cache.Keys)
        {
            keysToRemove.Add(key);
            count++;
            if (count >= _cache.Count / 4)
                break;
        }
        foreach (var key in keysToRemove)
            _cache.Remove(key);
    }
}
