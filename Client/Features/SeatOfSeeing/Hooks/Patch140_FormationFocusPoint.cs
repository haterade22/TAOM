using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.ScreenSystem;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Features.SeatOfSeeing.Hooks;

/// <summary>
/// Patch140 (docs/features/seat-of-seeing.md, build-spec 6.3): prefix on the private
/// <c>MissionFormationTargetSelectionHandler.TryGetFormationDistanceToCenter</c> that, while the Seat shows a cursor, puts the pointer in
/// the handler's reference point, so the enemy company the player points at is the one a left release charges, not the one at the screen
/// centre. It never skips the method. Off the Seat, or with no cursor, it returns at its first statement.
/// </summary>
[HarmonyPatch(typeof(MissionFormationTargetSelectionHandler), "TryGetFormationDistanceToCenter")]
[HarmonyPatchCategory(SeatOfSeeingModule.PatchCategory)]
public static class Patch140_FormationFocusPoint
{
    // ____centerOfScreen injects the private field _centerOfScreen: Harmony strips exactly three underscores, and the engine's own
    // leading underscore stays (HarmonyFieldInjectionNamingTests).
    [HarmonyPrefix]
    public static void Prefix(ref Vec2 ____centerOfScreen)
    {
        if (!SeatPresence.CursorShown)
            return;

        var mouse = TaleWorlds.InputSystem.Input.MousePositionRanged;
        var usable = ScreenManager.UsableArea;
        if (!SeatPointer.TryInUsableArea(mouse.x, mouse.y, Screen.RealScreenResolutionWidth, Screen.RealScreenResolutionHeight, usable.X, usable.Y,
                out var x, out var y))
            return;

        ____centerOfScreen = new Vec2(x, y);
        SeatPresence.MarkFocusSet();
    }
}
