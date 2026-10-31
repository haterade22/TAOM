using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using TAOM.Core.Logging;

namespace TAOM.Features.CompanionTactics.FormationPresets.Hooks;

/// <summary>
/// Prefix on <c>OrderOfBattleVM.OnFinalize</c>. Clears the tracker reference so post-finalize
/// queries don't hold a stale VM.
/// </summary>
[HarmonyPatch(typeof(OrderOfBattleVM), nameof(OrderOfBattleVM.OnFinalize))]
[HarmonyPatchCategory("Patch35_CompanionTactics")]
[ClientOnly]
public static class Patch35_OrderOfBattleVM_Finalize
{
    private static IOrderOfBattleVMTracker _tracker;

    [HarmonyPrefix]
    public static void Prefix()
    {
        try
        {
            _tracker ??= IoC.Resolve<IOrderOfBattleVMTracker>();
            _tracker?.ClearCurrent();
        }
        catch (Exception ex)
        {
            Outcomes.Swallowed("CompanionTactics", "Patch35_OrderOfBattleVM_Finalize.Prefix", ex);
        }
    }
}
