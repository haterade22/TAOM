using System;
using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>Click against drag, as the order placer tells them (<c>OrderTroopPlacer.UpdateFormationDrawing</c>, v1.5.7 :362-375).</summary>
internal static class SeatClickRule
{
    /// <summary>A pointer that moved less than this many pixels on both axes still stands where it went down.</summary>
    internal const float ClickPixels = 10f;

    /// <summary>A button let go sooner than this after it went down is a click, wherever the pointer went.</summary>
    internal const float ClickSeconds = 0.3f;

    /// <summary>A click: the pointer stayed within <see cref="ClickPixels"/> on both axes, or the button was let go within <see cref="ClickSeconds"/>.</summary>
    internal static bool IsClick(float pressX, float pressY, float nowX, float nowY, float heldSeconds) =>
        (Math.Abs(pressX - nowX) < ClickPixels && Math.Abs(pressY - nowY) < ClickPixels) || heldSeconds < ClickSeconds;

    /// <summary>A drag has gone far enough and long enough to draw a line.</summary>
    internal static bool IsDrag(float pressX, float pressY, float nowX, float nowY, float heldSeconds) =>
        !IsClick(pressX, pressY, nowX, nowY, heldSeconds);
}

/// <summary>What a left click does to the selection.</summary>
internal enum SeatSelectAction
{
    None,
    ClearAll,
    ReplaceWith,
    Add,
    Remove,
}

/// <summary>
/// What a left click writes, through the order controller's own selection: a click on nothing clears (an empty selection reads as all to the number
/// keys, <c>MissionOrderVM.cs:711-714</c>), except with the order panel open, where it keeps the selection because vanilla never leaves the panel
/// with none (<c>MissionOrderVM</c> selects all again, :997-999, :1017-1019) and the placer asserts on an empty list each tick.
/// </summary>
internal static class SeatSelectRule
{
    internal static SeatSelectAction Decide(bool shift, bool companyHit, bool alreadySelected, int selectedCount, bool panelOpen = false)
    {
        if (!companyHit)
            return shift || selectedCount == 0 || panelOpen ? SeatSelectAction.None : SeatSelectAction.ClearAll;
        if (shift)
            return alreadySelected ? SeatSelectAction.Remove : SeatSelectAction.Add;
        return alreadySelected && selectedCount == 1 ? SeatSelectAction.None : SeatSelectAction.ReplaceWith;
    }
}

/// <summary>What a right click orders.</summary>
internal enum SeatOrderKind
{
    None,
    Charge,
    Orderable,
    Move,
}

/// <summary>
/// The order a right click gives: an enemy company is charged, a thing the side can order is used, else valid ground is moved to; every order needs a
/// selected company, as the vanilla placer returns without one and the orderable call reads the first selected company with no test
/// (<c>OrderController.cs:1010</c>).
/// </summary>
internal static class SeatOrderRule
{
    internal static SeatOrderKind Decide(bool anySelected, bool enemyCompany, bool orderableObject, bool validGround)
    {
        if (!anySelected)
            return SeatOrderKind.None;
        if (enemyCompany)
            return SeatOrderKind.Charge;
        if (orderableObject)
            return SeatOrderKind.Orderable;
        return validGround ? SeatOrderKind.Move : SeatOrderKind.None;
    }

    internal static string Reason(bool anySelected) => anySelected ? "no-target" : "nothing-selected";
}

/// <summary>Which soldier a pointer names: the screen-space nearest one, as the commander's take does, and whether the ground hides him.</summary>
internal static class SeatPick
{
    /// <summary>How far from a soldier the pointer may stand, as a share of the screen's height. Soldiers are small from the Seat's height.</summary>
    internal const float ReachOfScreenHeight = 0.04f;

    /// <summary>The ray to a soldier stops this far short of his chest, so the ground he stands on, a bush or his own shield does not hide him.</summary>
    internal const float OccluderMargin = 0.75f;

    /// <summary>The index of the nearest candidate in reach, or -1. No soldier is preferred over another.</summary>
    internal static int Winner(IReadOnlyList<FordsControlCandidate> candidates, float pixelX, float pixelY, float screenHeight) =>
        FordsControlPick.Nearest(candidates, pixelX, pixelY, ReachOfScreenHeight * screenHeight);

    /// <summary>The distance the occlusion test gives <see cref="SeatKeepRule.AgentCounts"/> as the soldier's own: the camera's distance to his chest, less the margin.</summary>
    internal static float TestedDistance(float distanceToChest) => Math.Max(0f, distanceToChest - OccluderMargin);
}
