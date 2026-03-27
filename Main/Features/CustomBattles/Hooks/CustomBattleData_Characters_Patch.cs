using System;
using System.Collections.Generic;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.CustomBattle.CustomBattle;

namespace TAOM.Features.CustomBattles.Hooks;

[HarmonyPatch(typeof(CustomBattleData), nameof(CustomBattleData.Characters), MethodType.Getter)]
[HarmonyPatchCategory("Patch19_CustomBattles")]
public static class CustomBattleData_Characters_Patch
{
    private static IOnGetCustomBattleCommanders _hook;
    private static IModLogger _logger;

    public static void Initialize(IOnGetCustomBattleCommanders hook, IModLogger logger)
    {
        _hook = hook;
        _logger = logger;
    }

    [HarmonyPrefix]
    public static bool Prefix(ref IEnumerable<BasicCharacterObject> __result)
    {
        try
        {
            if (_hook == null)
            {
                _logger?.LogWarning("[CustomBattles] Characters patch: hook not initialized");
                return true;
            }

            __result = new List<BasicCharacterObject>();
            _hook.OnGetCustomBattleCommanders(ref __result);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[CustomBattles] Characters patch error: {ex.Message}");
            return true;
        }
    }
}
