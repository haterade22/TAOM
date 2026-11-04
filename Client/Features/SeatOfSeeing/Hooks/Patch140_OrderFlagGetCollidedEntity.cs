using System;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews.Order;
using TaleWorlds.MountAndBlade.View.Screens;
using TAOM.Core.Logging;

namespace TAOM.Features.SeatOfSeeing.Hooks;

/// <summary>
/// Patch140 (docs/features/seat-of-seeing.md, build-spec 5.8): replaces <c>OrderFlag.GetCollidedEntity</c> (private, v1.5.6
/// OrderFlag.cs:335) while the player's view is in the Seat, and only then. Vanilla starts the order ray at
/// <c>Agent.Main.GetEyeGlobalPosition()</c> (line 341), so from the Seat it tests a line from the hero's eyes to a point in the sky,
/// and with no main agent it throws. Seated, both ends of the ray come from <c>ScreenPointToWorldRay</c>: the cursor point when the
/// screen shows a cursor, else the screen centre. The 10000 m extension, the focus-ray flags and the parent walk to an orderable
/// entity are vanilla's. Off the Seat the prefix returns true at its first statement. The registry section holds the callers.
/// </summary>
[HarmonyPatch(typeof(OrderFlag), "GetCollidedEntity")]
[HarmonyPatchCategory(SeatOfSeeingModule.PatchCategory)]
public static class Patch140_OrderFlagGetCollidedEntity
{
    private static int _reported;

    // ____missionScreen and ____mission inject the protected fields _missionScreen and _mission: Harmony strips exactly three
    // underscores, and the engine's own leading underscore stays (HarmonyFieldInjectionNamingTests).
    [HarmonyPrefix]
    public static bool Prefix(ref WeakGameEntity __result, ref Vec3 closestPoint, MissionScreen ____missionScreen, Mission ____mission)
    {
        if (!SeatPresence.IsSeated)
            return true;

        try
        {
            __result = SeatOrderRay.Collide(____missionScreen, ____mission, out closestPoint);
        }
        catch (Exception ex)
        {
            // Vanilla would throw here (no main agent), so the safe answer is "nothing under the cursor".
            if (System.Threading.Interlocked.Exchange(ref _reported, 1) == 0)
                IoC.Resolve<IModLogger>()?.LogError($"[SeatOfSeeing] order ray failed, no target this frame: {ex}");
            __result = WeakGameEntity.Invalid;
            closestPoint = Vec3.Invalid;
        }

        return false;
    }
}
