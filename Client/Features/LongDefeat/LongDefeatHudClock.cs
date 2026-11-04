namespace TAOM.Client.Features.LongDefeat;

/// <summary>
/// The view's two timers: the refresh interval and the delay before the one-shot orders check. A step that is not a positive finite
/// number is no time passing, because a NaN would make every later comparison false and stop the refresh for the rest of the mission.
/// </summary>
internal sealed class LongDefeatHudClock
{
    internal const float RefreshIntervalSec = 0.25f;
    internal const float OrdersDelaySec = 1f;

    private float _sinceRefresh = RefreshIntervalSec;
    private float _sinceAttach;

    internal void Advance(float dt)
    {
        if (!(dt > 0f) || float.IsInfinity(dt))
            return;

        _sinceRefresh += dt;
        _sinceAttach += dt;
    }

    /// <summary>True once per interval; taking it restarts the interval.</summary>
    internal bool TakeRefresh()
    {
        if (_sinceRefresh < RefreshIntervalSec)
            return false;

        _sinceRefresh = 0f;
        return true;
    }

    internal bool OrdersDelayElapsed => _sinceAttach >= OrdersDelaySec;
}
