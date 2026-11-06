namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// One commander ability's timer (phase2-plan.md section 2): the first use is due <c>firstSeconds</c> after it starts, each later one <c>cooldownSeconds</c> after the previous use.
/// It fires by itself on <see cref="Tick"/>, at most once per call however long the step (the ready time restarts from the tick that fired), and <see cref="Stop"/> is final. The
/// shape follows <c>FordsMatch</c>'s ability clock (ready time is now plus the cooldown; time left is ready minus now, at least 0) and adds the first delay, the self-fire and the stop.
/// The clock's time is the run's wave seconds.
/// </summary>
internal sealed class CommanderClock
{
    private readonly float _cooldown;
    private float _ready;

    public CommanderClock(float now, float firstSeconds, float cooldownSeconds)
    {
        _ready = now + firstSeconds;
        _cooldown = cooldownSeconds;
    }

    public bool Stopped { get; private set; }

    /// <summary>Ends the timer for good: it never fires again.</summary>
    public void Stop() => Stopped = true;

    /// <summary>True when the ability is used at <paramref name="now"/>. A time that is not a number never fires.</summary>
    public bool Tick(float now)
    {
        if (Stopped || !(now >= _ready))
            return false;

        _ready = now + _cooldown;
        return true;
    }

    /// <summary>Seconds until the next use, at least 0; 0 once stopped.</summary>
    public float SecondsLeft(float now) => Stopped ? 0f : System.Math.Max(0f, _ready - now);
}
