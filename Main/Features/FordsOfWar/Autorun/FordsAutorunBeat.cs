using System.Globalization;

namespace TAOM.Features.FordsOfWar.Autorun;

/// <summary>
/// The autorun's two timers: the status heartbeat the launcher watches (every 5 wall seconds) and the clock line (every 30 wall
/// seconds while the match is live). The clock line's ratio is mission seconds per wall second, so a value near 0 would show the engine
/// pausing the mission while the window is unfocused.
/// </summary>
internal sealed class FordsAutorunBeat
{
    private const double StatusSeconds = 5.0;
    private const double ClockSeconds = 30.0;

    private double _lastStatus;
    private double _lastClock;
    private double _clockMt;

    /// <summary>True once per <see cref="StatusSeconds"/>.</summary>
    internal bool StatusDue(double wall)
    {
        if (wall - _lastStatus < StatusSeconds)
            return false;

        _lastStatus = wall;
        return true;
    }

    internal void StartClock(double wall, double mt)
    {
        _lastClock = wall;
        _clockMt = mt;
    }

    /// <summary>The clock line's body when 30 wall seconds have passed since the last one, else null.</summary>
    internal string? ClockLine(double wall, double mt, double live)
    {
        if (wall - _lastClock < ClockSeconds)
            return null;

        var ratio = (mt - _clockMt) / (wall - _lastClock);
        StartClock(wall, mt);
        return "clock wall=" + FordsAutorunDone.F1(wall) + " mt=" + FordsAutorunDone.F1(mt) + " live=" + FordsAutorunDone.F1(live)
            + " ratio=" + ratio.ToString("F2", CultureInfo.InvariantCulture);
    }
}
