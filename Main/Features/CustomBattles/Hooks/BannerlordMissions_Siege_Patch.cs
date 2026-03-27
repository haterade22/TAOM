using System;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CustomBattles.Hooks;

[HarmonyPatch(typeof(BannerlordMissions), nameof(BannerlordMissions.OpenSiegeMissionWithDeployment))]
[HarmonyPatchCategory("Patch19_CustomBattles")]
public static class BannerlordMissions_Siege_Patch
{
    private static IModLogger _logger;

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
    }

    [HarmonyPostfix]
    public static void Postfix(ref Mission __result)
    {
        try
        {
            if (__result == null)
                return;

            bool isCustomBattle = true;
            foreach (var behavior in __result.MissionBehaviors)
            {
                if (behavior.GetType().Name.Contains("Campaign"))
                {
                    isCustomBattle = false;
                    break;
                }
            }

            if (isCustomBattle)
            {
                _logger?.LogDebug("[CustomBattles] Adding CustomBattleTeamFixBehavior to custom siege mission");
                __result.AddMissionBehavior(new CustomBattleTeamFixBehavior(_logger));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[CustomBattles] Error adding team fix to siege: {ex.Message}");
        }
    }
}
