using System;
using System.Collections.Generic;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.CustomBattle.CustomBattle;

namespace TAOM.Features.CustomBattles.Hooks;

[HarmonyPatch(typeof(CustomBattleData), nameof(CustomBattleData.Factions), MethodType.Getter)]
[HarmonyPatchCategory("Patch19_CustomBattles")]
public static class CustomBattleData_Factions_Patch
{
    private static IOnGetCustomBattleFactions _hook;
    private static IModLogger _logger;

    public static void Initialize(IOnGetCustomBattleFactions hook, IModLogger logger)
    {
        _hook = hook;
        _logger = logger;
    }

    [HarmonyPrefix]
    public static bool Prefix(ref IEnumerable<BasicCultureObject> __result)
    {
        try
        {
            if (_hook == null)
            {
                _logger?.LogWarning("[CustomBattles] Factions patch: hook not initialized");
                return true;
            }

            __result = new List<BasicCultureObject>();
            _hook.OnGetCustomBattleFactions(ref __result);

            var list = __result as List<BasicCultureObject>;
            return list == null || list.Count == 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[CustomBattles] Factions patch error: {ex.Message}");
            return true;
        }
    }
}
