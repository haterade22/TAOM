using HarmonyLib;
using TaleWorlds.MountAndBlade.GauntletUI.Mission;

namespace TAOM.Features.SeatOfSeeing.Hooks;

/// <summary>
/// Patch140 (docs/features/seat-of-seeing.md, build-spec 6.4): postfix on the protected virtual
/// <c>MissionGauntletCrosshair.GetShouldArrowsBeVisible</c> that answers false while the player's view is in the Seat. The crosshair, the
/// attack direction arrows and the reload display all follow it, so one patch hides them. Off the Seat the postfix returns at its first
/// statement. The registry section holds the callers.
/// </summary>
[HarmonyPatch(typeof(MissionGauntletCrosshair), "GetShouldArrowsBeVisible")]
[HarmonyPatchCategory(SeatOfSeeingModule.PatchCategory)]
public static class Patch140_CrosshairArrows
{
    [HarmonyPostfix]
    public static void Postfix(ref bool __result)
    {
        if (SeatPresence.IsSeated)
            __result = false;
    }
}
