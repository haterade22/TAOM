using System;
using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.CustomBattle;

namespace TAOM.Features.CustomBattles.Hooks;

[HarmonyPatch(typeof(CustomBattleSideVM), MethodType.Constructor)]
[HarmonyPatchCategory("Patch19_CustomBattles")]
public static class CustomBattleSideVM_Constructor_Patch
{
    private static IModLogger _logger;

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
    }

    [HarmonyPostfix]
    public static void Postfix(CustomBattleSideVM __instance)
    {
        try
        {
            var method = typeof(CustomBattleSideVM).GetMethod(
                "OnCultureSelection",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (method == null)
            {
                _logger?.LogWarning("[CustomBattles] Could not find OnCultureSelection method");
                return;
            }

            var callback = (Action<BasicCultureObject>)Delegate.CreateDelegate(
                typeof(Action<BasicCultureObject>), __instance, method);

            __instance.FactionSelectionGroup = new TaomFactionSelectionVM(callback);
            _logger?.LogInfo($"[CustomBattles] Injected TaomFactionSelectionVM with {__instance.FactionSelectionGroup.Factions.Count} factions");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[CustomBattles] Failed to inject TaomFactionSelectionVM: {ex.Message}");
        }
    }
}
