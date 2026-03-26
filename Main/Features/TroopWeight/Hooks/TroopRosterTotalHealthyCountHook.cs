using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.Roster;

namespace TAOM.Features.TroopWeight.Hooks;

public class TroopRosterTotalHealthyCountHook : IOnTroopRosterTotalHealthyCount
{
    private readonly ITroopWeightService _troopWeightService;
    private readonly IModLogger _logger;
    private readonly Dictionary<int, (int Version, float WeightedHealthyCount)> _cache;
    private const int CleanupThreshold = 500;

    public TroopRosterTotalHealthyCountHook(ITroopWeightService troopWeightService, IModLogger logger)
    {
        _troopWeightService = troopWeightService;
        _logger = logger;
        _cache = new Dictionary<int, (int, float)>();
    }

    public void OnTroopRosterTotalHealthyCount(TroopRoster troopRoster, ref int __result)
    {
        try
        {
            if (troopRoster == null)
                return;

            int cacheKey = troopRoster.GetHashCode();
            int currentVersion = troopRoster.VersionNo;

            if (_cache.TryGetValue(cacheKey, out var cached) && cached.Version == currentVersion)
            {
                var cachedResult = (int)Math.Ceiling(cached.WeightedHealthyCount);
                if (cachedResult > __result)
                    __result = cachedResult;
                return;
            }

            float weightedCount = 0f;
            for (int i = 0; i < troopRoster.Count; i++)
            {
                var element = troopRoster.GetElementCopyAtIndex(i);
                if (element.Character != null)
                {
                    var weight = _troopWeightService.GetTroopWeight(element.Character);
                    var healthy = Math.Max(0, element.Number - element.WoundedNumber);
                    weightedCount += healthy * weight;
                }
            }

            _cache[cacheKey] = (currentVersion, weightedCount);

            var weightedResult = (int)Math.Ceiling(weightedCount);
            if (weightedResult > __result)
                __result = weightedResult;

            if (_cache.Count > CleanupThreshold)
                _cache.Clear();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"TroopRoster.TotalHealthyCount hook error: {ex.Message}");
        }
    }
}
