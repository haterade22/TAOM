using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.MountAndBlade.ViewModelCollection.Order;

namespace TAOM.Features.SeatOfSeeing.Hooks;

/// <summary>Patch140 (build-spec 8.2): transpiler on <c>MissionOrderVM.CheckCanBeOpened</c> that lets the order panel open for a hero who fights on; the registry holds why.</summary>
[HarmonyPatch(typeof(MissionOrderVM), "CheckCanBeOpened")]
[HarmonyPatchCategory(SeatOfSeeingModule.PatchCategory)]
public static class Patch140_OrderPanelCheck
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => SeatOrderPanelTranspiler.LetTheHandedHeroOpenIt(instructions, SeatPatchLog.Logger);
}
