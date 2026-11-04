using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Screens;

namespace TAOM.Features.SeatOfSeeing.Hooks;

/// <summary>
/// Patch140 (docs/features/seat-of-seeing.md, build-spec 5.8): postfix on <c>MissionScreen.ScreenPointToWorldRay</c> (v1.5.6
/// MissionScreen.cs:3502) that, while the player's view is in the Seat, extends the ray end to at least 1000 m along the same line.
/// Vanilla clamps the ray to the largest boundary radius, or 30 m with no boundaries (lines 3525-3540), so from a view that can be
/// 110 m up the ray would stop in the air. Off the Seat the postfix returns at its first statement. Called once a frame or so on the
/// main thread by the order panel's placer and flag; the registry section lists them.
/// </summary>
[HarmonyPatch(typeof(MissionScreen), nameof(MissionScreen.ScreenPointToWorldRay))]
[HarmonyPatchCategory(SeatOfSeeingModule.PatchCategory)]
public static class Patch140_MissionScreenScreenPointToWorldRay
{
    [HarmonyPostfix]
    public static void Postfix(Vec3 rayBegin, ref Vec3 rayEnd)
    {
        if (!SeatPresence.IsSeated)
            return;

        rayEnd = SeatOrderRay.Lengthen(rayBegin, rayEnd);
    }
}
