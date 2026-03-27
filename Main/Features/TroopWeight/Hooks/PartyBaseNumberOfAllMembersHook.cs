using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.TroopWeight.Hooks;

public class PartyBaseNumberOfAllMembersHook : IOnPartyBaseNumberOfAllMembers
{
    private readonly ITroopWeightService _troopWeightService;
    private readonly Dictionary<int, (int Version, int WeightedResult)> _cache = new();

    public PartyBaseNumberOfAllMembersHook(ITroopWeightService troopWeightService, IModLogger logger)
    {
        _troopWeightService = troopWeightService;
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
                if (cached.WeightedResult > __result)
                    __result = cached.WeightedResult;
                return;
            }

            var weightedCount = _troopWeightService.CalculateWeightedMemberCount(partyBase);
            var weightedResult = (int)Math.Ceiling(weightedCount);

            _cache[cacheKey] = (currentVersion, weightedResult);

            if (weightedResult > __result)
                __result = weightedResult;
        }
        catch
        {
        }
    }
}
