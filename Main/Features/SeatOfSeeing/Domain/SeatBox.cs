using System;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>A screen rectangle in real pixels, with <c>Left &lt;= Right</c> and <c>Top &lt;= Bottom</c>.</summary>
internal readonly record struct SeatRect(float Left, float Top, float Right, float Bottom)
{
    internal float Width => Right - Left;

    internal float Height => Bottom - Top;
}

/// <summary>The Seat's box select: the geometry and the decision; the engine side is <c>ISeatCommandAdapter</c>.</summary>
internal static class SeatBox
{
    /// <summary>The rectangle between two corners, whichever way the drag went.</summary>
    internal static SeatRect Normalise(float x0, float y0, float x1, float y1) =>
        new(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));

    /// <summary>A projected point is inside the rectangle, its edges included.</summary>
    internal static bool Contains(in SeatRect box, float x, float y) =>
        x >= box.Left && x <= box.Right && y >= box.Top && y <= box.Bottom;

    /// <summary>
    /// What a released box writes: companies found replace the selection (Shift adds); a box on nothing clears it, unless Shift is held or the
    /// order panel is open.
    /// </summary>
    internal static SeatSelectAction Decide(bool shift, int companiesInside, int selectedCount, bool panelOpen)
    {
        if (companiesInside > 0)
            return shift ? SeatSelectAction.Add : SeatSelectAction.ReplaceWith;
        return shift || selectedCount == 0 || panelOpen ? SeatSelectAction.None : SeatSelectAction.ClearAll;
    }
}
