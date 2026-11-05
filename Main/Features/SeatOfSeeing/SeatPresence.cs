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
    private static volatile bool _heroFightsOn;
    private static volatile bool _holdsBoundary;

    internal static bool IsSeated => _seated;

    /// <summary>The hero is on the AI (build-spec 8.1); leaving does not clear it, because the hero stays on the AI until the mission tick hands him back.</summary>
    internal static bool HeroFightsOn => _heroFightsOn;

    internal static void SetHeroFightsOn(bool fighting) => _heroFightsOn = fighting;

    /// <summary>The morale models' refusal (build-spec 8.1, Begin step 5): the hero on the AI never panics.</summary>
    internal static bool RefusesPanic(bool agentIsMainAgent) => _heroFightsOn && agentIsMainAgent;

    /// <summary>The order panel's test (build-spec 8.2): the engine's answer, or yes for the main agent while the hero fights on.</summary>
    internal static bool CountsAsPlayerControlled(bool engineAnswer, bool agentIsMainAgent) => engineAnswer || (_heroFightsOn && agentIsMainAgent);

    /// <summary>The boundary patch's question (build-spec 8.2): the main agent is held inside the line while the view is in the Seat, where the policy allows the hand-off.</summary>
    internal static bool HoldsBoundaryCountdown(bool agentIsMainAgent) => _holdsBoundary && agentIsMainAgent;

    /// <summary>The Seat shows a cursor for the order panel: the formation focus patch aims at it.</summary>
    internal static bool CursorShown => _cursorShown;

    /// <summary>Sets the seated flag and the boundary hold, which only the entry's fight-on permission raises; leaving also clears the cursor and the focus mark.</summary>
    internal static void Set(bool seated, bool holdsBoundary = false)
    {
        _seated = seated;
        _holdsBoundary = seated && holdsBoundary;
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
