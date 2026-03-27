using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.TroopWeight.Hooks;

public class PartyBaseNumberOfRegularMembersHook : IOnPartyBaseNumberOfRegularMembers
{
    private readonly ITroopWeightService _troopWeightService;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _loggedErrors = new();

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
            var errorKey = $"{ex.GetType().Name}:{ex.Message}";
            if (_loggedErrors.Add(errorKey))
            {
                var partyName = partyBase?.Name?.ToString() ?? "unknown";
                _logger.LogWarning($"[TroopWeight] RegularMembers hook error for party '{partyName}': {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
