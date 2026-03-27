using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.TroopWeight.Hooks;

public class PartyBaseNumberOfAllMembersHook : IOnPartyBaseNumberOfAllMembers
{
    private readonly ITroopWeightService _troopWeightService;
    private readonly IModLogger _logger;
    private readonly Dictionary<int, (int Version, float WeightedCount)> _cache;
    private readonly HashSet<string> _loggedErrors = new();
    private const int MaxCacheSize = 2000;

    public PartyBaseNumberOfAllMembersHook(ITroopWeightService troopWeightService, IModLogger logger)
    {
        _troopWeightService = troopWeightService;
        _logger = logger;
        _cache = new Dictionary<int, (int, float)>();
    }

    public void OnPartyBaseNumberOfAllMembers(PartyBase partyBase, ref int __result)
    {
        try
        {
            if (partyBase?.MemberRoster == null)
                return;

            int cacheKey = partyBase.GetHashCode();
            int currentVersion = partyBase.MemberRoster.VersionNo;

            if (_cache.TryGetValue(cacheKey, out var cached) && cached.Version == currentVersion)
            {
                var cachedResult = (int)Math.Ceiling(cached.WeightedCount);
                if (cachedResult > __result)
                    __result = cachedResult;
                return;
            }

            var weightedCount = _troopWeightService.CalculateWeightedMemberCount(partyBase);

            _cache[cacheKey] = (currentVersion, weightedCount);

            if (weightedCount > __result)
                __result = (int)Math.Ceiling(weightedCount);

            if (_cache.Count > MaxCacheSize)
            {
                _logger.LogDebug($"[TroopWeight] AllMembers cache trimmed (was {_cache.Count} entries)");
                TrimCache();
            }
        }
        catch (Exception ex)
        {
            var errorKey = $"{ex.GetType().Name}:{ex.Message}";
            if (_loggedErrors.Add(errorKey))
            {
                var partyName = partyBase?.Name?.ToString() ?? "unknown";
                _logger.LogWarning($"[TroopWeight] AllMembers hook error for party '{partyName}': {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private void TrimCache()
    {
        _cache.Clear();
    }
}
