using HarmonyLib;
using TaleWorlds.MountAndBlade.ViewModelCollection.HUD;

namespace TAOM.Features.SeatOfSeeing.Hooks;

/// <summary>
/// Patch140 (docs/features/seat-of-seeing.md, build-spec 6.4): prefix on <c>CrosshairVM.ShowHitMarker</c> that skips it while the player's
/// view is in the Seat. The combat log calls it with no visibility test, and the marker is a sibling of the crosshair in the prefab, so an
/// arrow loosed before entry could flash it at the centre of the sky view. Off the Seat the prefix returns true at its first statement.
/// </summary>
[HarmonyPatch(typeof(CrosshairVM), nameof(CrosshairVM.ShowHitMarker))]
[HarmonyPatchCategory(SeatOfSeeingModule.PatchCategory)]
public static class Patch140_CrosshairHitMarker
{
    [HarmonyPrefix]
    public static bool Prefix() => !SeatPresence.IsSeated;
}
