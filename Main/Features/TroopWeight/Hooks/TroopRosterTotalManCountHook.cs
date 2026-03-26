using System;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.Roster;

namespace TAOM.Features.TroopWeight.Hooks;

public class TroopRosterTotalManCountHook : IOnTroopRosterTotalManCount
{
    private readonly ITroopWeightService _troopWeightService;
    private readonly IModLogger _logger;

    public TroopRosterTotalManCountHook(ITroopWeightService troopWeightService, IModLogger logger)
    {
        _troopWeightService = troopWeightService;
        _logger = logger;
    }

    public void OnTroopRosterTotalManCount(TroopRoster troopRoster, ref int __result)
    {
        try
        {
            if (troopRoster == null)
                return;

            var weightedCount = _troopWeightService.CalculateWeightedRosterCount(troopRoster);
            if (weightedCount > __result)
                __result = (int)Math.Ceiling(weightedCount);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"TroopRoster.TotalManCount hook error: {ex.Message}");
        }
    }
}
