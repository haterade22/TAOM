using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.SeatOfSeeing.Hooks;

/// <summary>Patch140 (build-spec section 9): postfix on <c>Mission.CanTakeControlOfAgent</c> that answers false while the view is in the Seat, so vanilla's Kick key takes nothing from the sky; the registry holds why.</summary>
[HarmonyPatch(typeof(Mission), nameof(Mission.CanTakeControlOfAgent))]
[HarmonyPatchCategory(SeatOfSeeingModule.PatchCategory)]
public static class Patch140_TakeControlOfAgent
{
    [HarmonyPostfix]
    public static void Postfix(ref bool __result)
    {
        if (SeatPresence.IsSeated)
            __result = false;
    }
}
