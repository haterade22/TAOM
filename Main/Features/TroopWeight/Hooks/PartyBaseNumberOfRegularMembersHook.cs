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

            // Compute exact weighted healthy count per troop type instead of
            // approximating via wounded ratio (which breaks for asymmetric weights)
            float weightedHealthy = 0f;
            int rosterCount = partyBase.MemberRoster.Count;
            for (int i = 0; i < rosterCount; i++)
            {
                var element = partyBase.MemberRoster.GetElementCopyAtIndex(i);
                int healthy = element.Number - element.WoundedNumber;
                if (healthy > 0)
                    weightedHealthy += healthy * _troopWeightService.GetTroopWeight(element.Character);
            }

            int weightedResult = (int)Math.Ceiling(weightedHealthy);

            _cache[cacheKey] = (currentVersion, weightedResult);

            if (weightedResult > __result)
                __result = weightedResult;
        }
        catch
        {
        }
    }
}
