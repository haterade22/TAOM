using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;

namespace TAOM.Features.CompanionTactics.FormationPresets.Hooks;

/// <summary>
/// Postfix on every <c>OrderOfBattleVM</c> constructor. Captures the new VM into the
/// tracker so the OOB UI overlay can find it later.
/// </summary>
[HarmonyPatchCategory("Patch35_CompanionTactics")]
[HarmonyPatch(typeof(OrderOfBattleVM))]
public static class Patch35_OrderOfBattleVM_Ctor
{
    private static IOrderOfBattleVMTracker _tracker;

    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var ctor in typeof(OrderOfBattleVM).GetConstructors())
            yield return ctor;
    }

    [HarmonyPostfix]
    public static void Postfix(OrderOfBattleVM __instance)
    {
        try
        {
            _tracker ??= IoC.Resolve<IOrderOfBattleVMTracker>();
            _tracker?.SetCurrent(__instance);
        }
        catch { }
    }
}
