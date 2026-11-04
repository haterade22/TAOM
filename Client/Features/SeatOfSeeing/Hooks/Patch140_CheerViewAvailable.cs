using HarmonyLib;
using TaleWorlds.MountAndBlade.GauntletUI.Mission;

namespace TAOM.Features.SeatOfSeeing.Hooks;

/// <summary>
/// Patch140 (docs/features/seat-of-seeing.md, build-spec 6.4): postfix on the private <c>MissionGauntletMainAgentCheerControllerView.IsMainAgentAvailable</c> that answers
/// false while the player's view is in the Seat, so the cheer wheel (game key 31) does not open from the air; a false answer also closes one that is
/// open. Off the Seat the postfix returns at its first statement.
/// </summary>
[HarmonyPatch(typeof(MissionGauntletMainAgentCheerControllerView), "IsMainAgentAvailable")]
[HarmonyPatchCategory(SeatOfSeeingModule.PatchCategory)]
public static class Patch140_CheerViewAvailable
{
    [HarmonyPostfix]
    public static void Postfix(ref bool __result)
    {
        if (SeatPresence.IsSeated)
            __result = false;
    }
}
