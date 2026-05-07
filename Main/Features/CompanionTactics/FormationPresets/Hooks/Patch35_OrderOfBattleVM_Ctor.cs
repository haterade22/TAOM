using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;

namespace TAOM.Features.CompanionTactics.FormationPresets.Hooks;

/// <summary>
/// Postfix on the parameterless <c>OrderOfBattleVM</c> constructor (the only public ctor
/// in v1.3.15 — verified via ilspycmd). Captures the new VM into the tracker so the OOB
/// UI overlay can find it later.
///
/// Uses <see cref="MethodType.Constructor"/> with an empty parameter-type array so
/// BUTR.Harmony.Analyzer can resolve the target method statically (the older
/// <c>[HarmonyTargetMethods]</c> ctor-enumeration form was opaque to the analyzer and
/// triggered a BHA0001 false positive).
/// </summary>
[HarmonyPatch(typeof(OrderOfBattleVM), MethodType.Constructor, new Type[0])]
[HarmonyPatchCategory("Patch35_CompanionTactics")]
public static class Patch35_OrderOfBattleVM_Ctor
{
    private static IOrderOfBattleVMTracker _tracker;

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
