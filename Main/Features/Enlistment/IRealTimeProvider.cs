using System.Diagnostics;

namespace TAOM.Features.Enlistment;

/// <summary>
/// Monotonic wall-clock seconds since the process started. A seam for the same reason
/// <c>IRandomProvider</c> is one: a service that reads the clock directly is a service whose
/// behaviour cannot be tested.
///
/// Deliberately NOT campaign time. Everything else in this feature measures in campaign days,
/// because that is what the fiction and the mechanics run on. This measures what the PLAYER
/// experienced, which is a different quantity and diverges by the time-acceleration multiplier —
/// a 4-hour shift is four campaign hours at every speed and four real seconds at 4x. The one
/// consumer is the duty toast pair, whose whole problem is that two messages a player must read
/// can land closer together than a message stays on screen.
///
/// Monotonic, so it cannot go backwards across a system clock change; relative to an arbitrary
/// origin, so only DIFFERENCES are meaningful — never treat a reading as a timestamp.
/// </summary>
public interface IRealTimeProvider
{
    double ElapsedSeconds { get; }
}

/// <inheritdoc />
public sealed class RealTimeProvider : IRealTimeProvider
{
    // One Stopwatch for the process. Registered Reuse.Singleton, but static anyway so two
    // registrations could never hand out two different origins — differences across them would be
    // meaningless, and the failure would look like a timing bug rather than a wiring one.
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    public double ElapsedSeconds => Clock.Elapsed.TotalSeconds;
}
