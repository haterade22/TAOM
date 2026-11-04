namespace TAOM.Features.SeatOfSeeing;

/// <summary>
/// What the Seat's patches read: whether the player's view is in the Seat, and whether it shows a cursor. Leaving or faulting clears
/// both through <see cref="Set"/>, so a patch is a no-op in the next mission.
/// </summary>
internal static class SeatPresence
{
    private static volatile bool _seated;
    private static volatile bool _cursorShown;
    private static volatile bool _focusSet;

    internal static bool IsSeated => _seated;

    /// <summary>The Seat shows a cursor for the order panel: the formation focus patch aims at it.</summary>
    internal static bool CursorShown => _cursorShown;

    /// <summary>Sets the seated flag. Setting it false also clears the cursor and the focus mark.</summary>
    internal static void Set(bool seated)
    {
        _seated = seated;
        if (!seated)
        {
            _cursorShown = false;
            _focusSet = false;
        }
    }

    internal static void SetCursor(bool shown)
    {
        _cursorShown = shown;
        if (!shown)
            _focusSet = false;
    }

    /// <summary>The focus patch has put the pointer in the handler's field since the cursor was shown.</summary>
    internal static void MarkFocusSet() => _focusSet = true;

    /// <summary>True once after each <see cref="MarkFocusSet"/>.</summary>
    internal static bool TakeFocusSet()
    {
        if (!_focusSet)
            return false;
        _focusSet = false;
        return true;
    }
}
