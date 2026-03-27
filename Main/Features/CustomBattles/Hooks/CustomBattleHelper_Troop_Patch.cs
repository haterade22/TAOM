using System;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.CustomBattle.CustomBattle;

namespace TAOM.Features.CustomBattles.Hooks;

[HarmonyPatch(typeof(CustomBattleHelper), nameof(CustomBattleHelper.GetDefaultTroopOfFormationForFaction))]
[HarmonyPatchCategory("Patch19_CustomBattles")]
public static class CustomBattleHelper_Troop_Patch
{
    private static IOnGetDefaultTroopOfFormation _hook;
    private static IModLogger _logger;

    public static void Initialize(IOnGetDefaultTroopOfFormation hook, IModLogger logger)
    {
        _hook = hook;
        _logger = logger;
    }

    [HarmonyPostfix]
    public static void Postfix(BasicCultureObject culture, FormationClass formation, ref BasicCharacterObject __result)
    {
        try
        {
            if (_hook == null || culture == null)
                return;

            _hook.OnGetDefaultTroopOfFormation(culture.StringId, (int)formation, ref __result);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[CustomBattles] Troop patch error: {ex.Message}");
        }
    }
}
