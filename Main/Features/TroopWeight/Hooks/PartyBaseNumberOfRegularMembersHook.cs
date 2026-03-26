using System;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.TroopWeight.Hooks;

public class PartyBaseNumberOfRegularMembersHook : IOnPartyBaseNumberOfRegularMembers
{
    private readonly ITroopWeightService _troopWeightService;
    private readonly IModLogger _logger;

    public PartyBaseNumberOfRegularMembersHook(ITroopWeightService troopWeightService, IModLogger logger)
    {
        _troopWeightService = troopWeightService;
        _logger = logger;
    }

    public void OnPartyBaseNumberOfRegularMembers(PartyBase partyBase, ref int __result)
    {
        try
        {
            if (partyBase?.MemberRoster == null)
                return;

            var originalTotal = partyBase.MemberRoster.TotalManCount;
            var originalWounded = partyBase.MemberRoster.TotalWounded;
            var weightedTotal = _troopWeightService.CalculateWeightedMemberCount(partyBase);

            if (weightedTotal > originalTotal)
            {
                var woundedRatio = originalTotal > 0 ? (float)originalWounded / originalTotal : 0;
                var weightedWounded = (int)(weightedTotal * woundedRatio);
                __result = (int)Math.Ceiling(weightedTotal) - weightedWounded;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"PartyBase.NumberOfRegularMembers hook error: {ex.Message}");
        }
    }
}
