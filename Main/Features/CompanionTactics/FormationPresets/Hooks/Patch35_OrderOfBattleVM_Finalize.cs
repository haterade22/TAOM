using HarmonyLib;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;

namespace TAOM.Features.CompanionTactics.FormationPresets.Hooks;

/// <summary>
/// Prefix on <c>OrderOfBattleVM.OnFinalize</c>. Clears the tracker reference so post-finalize
/// queries don't hold a stale VM.
/// </summary>
[HarmonyPatchCategory("Patch35_CompanionTactics")]
[HarmonyPatch(typeof(OrderOfBattleVM), nameof(OrderOfBattleVM.OnFinalize))]
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
        catch { }
    }
}
