using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.TroopWeight.Hooks;

public class PartyBaseNumberOfRegularMembersHook : IOnPartyBaseNumberOfRegularMembers
{
    private readonly ITroopWeightService _troopWeightService;
    private readonly Dictionary<int, (int Version, int WeightedResult)> _cache = new();

    public PartyBaseNumberOfRegularMembersHook(ITroopWeightService troopWeightService, IModLogger logger)
    {
        _troopWeightService = troopWeightService;
    }

    public void OnPartyBaseNumberOfRegularMembers(PartyBase partyBase, ref int __result)
    {
        try
        {
            if (partyBase?.MemberRoster == null)
                return;

            int cacheKey = partyBase.GetHashCode();
            int currentVersion = partyBase.MemberRoster.VersionNo;

            if (_cache.TryGetValue(cacheKey, out var cached) && cached.Version == currentVersion)
            {
                if (cached.WeightedResult > __result)
                    __result = cached.WeightedResult;
                return;
            }

            var originalTotal = partyBase.MemberRoster.TotalManCount;
            var originalWounded = partyBase.MemberRoster.TotalWounded;
            var weightedTotal = _troopWeightService.CalculateWeightedMemberCount(partyBase);

            int weightedResult = __result;
            if (weightedTotal > originalTotal)
            {
                var woundedRatio = originalTotal > 0 ? (float)originalWounded / originalTotal : 0;
                var weightedWounded = (int)(weightedTotal * woundedRatio);
                weightedResult = (int)Math.Ceiling(weightedTotal) - weightedWounded;
            }

            _cache[cacheKey] = (currentVersion, weightedResult);

            if (weightedResult > __result)
                __result = weightedResult;
        }
        catch
        {
        }
    }
}
