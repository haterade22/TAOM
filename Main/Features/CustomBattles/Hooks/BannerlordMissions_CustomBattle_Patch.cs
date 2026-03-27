using System;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CustomBattles.Hooks;

[HarmonyPatch(typeof(BannerlordMissions), nameof(BannerlordMissions.OpenCustomBattleMission))]
[HarmonyPatchCategory("Patch19_CustomBattles")]
public static class BannerlordMissions_CustomBattle_Patch
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
            if (__result != null)
            {
                _logger?.LogDebug("[CustomBattles] Adding CustomBattleTeamFixBehavior to custom battle mission");
                __result.AddMissionBehavior(new CustomBattleTeamFixBehavior(_logger));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[CustomBattles] Error adding team fix to custom battle: {ex.Message}");
        }
    }
}
