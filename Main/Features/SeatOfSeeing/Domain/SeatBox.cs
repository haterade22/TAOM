using System;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>A screen rectangle in real pixels, with <c>Left &lt;= Right</c> and <c>Top &lt;= Bottom</c>.</summary>
internal readonly record struct SeatRect(float Left, float Top, float Right, float Bottom)
{
    internal float Width => Right - Left;

    internal float Height => Bottom - Top;
}

/// <summary>
/// The Seat's box select (design section 3): a left drag past <see cref="SeatClickRule"/>'s click test draws a rectangle from where the
/// button went down to the pointer, and on release every selectable company with a soldier whose projected chest is inside it is selected. The
/// engine side (projecting the chests, writing the selection) is <c>ISeatCommandAdapter</c>; this type holds the geometry and the decision.
/// </summary>
internal static class SeatBox
{
    /// <summary>The rectangle between two corners, whichever way the drag went.</summary>
    internal static SeatRect Normalise(float x0, float y0, float x1, float y1) =>
        new(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));

    /// <summary>A projected point is inside the rectangle, its edges included.</summary>
    internal static bool Contains(in SeatRect box, float x, float y) =>
        x >= box.Left && x <= box.Right && y >= box.Top && y <= box.Bottom;

    /// <summary>
    /// What a released box writes, with the company count the box found. A box on companies replaces the selection, or with Shift adds to it. A
    /// box on nothing clears like a click on nothing, but Shift never clears, and with the order panel open the selection is never left empty
    /// (the placer asserts on an empty list each tick, <see cref="SeatSelectRule"/>).
    /// </summary>
    internal static SeatSelectAction Decide(bool shift, int companiesInside, int selectedCount, bool panelOpen)
    {
        if (companiesInside > 0)
            return shift ? SeatSelectAction.Add : SeatSelectAction.ReplaceWith;
        return shift || selectedCount == 0 || panelOpen ? SeatSelectAction.None : SeatSelectAction.ClearAll;
    }
}
