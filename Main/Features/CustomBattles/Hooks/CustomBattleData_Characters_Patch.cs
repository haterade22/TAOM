using System;
using System.Collections.Generic;
using System.Linq;
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

            IEnumerable<BasicCharacterObject> commanders = new List<BasicCharacterObject>();
            _hook.OnGetCustomBattleCommanders(ref commanders);

            var list = commanders as ICollection<BasicCharacterObject> ?? commanders.ToList();
            if (list.Count == 0)
            {
                _logger?.LogWarning("[CustomBattles] Characters patch: no TAOM commanders found, falling back to vanilla");
                return true;
            }

            __result = list;
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[CustomBattles] Characters patch error: {ex.Message}");
            return true;
        }
    }
}
