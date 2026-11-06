namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// One volley source's timer (phase3-plan.md D30). The first use is due <c>firstSeconds</c> after it starts and <see cref="Ready"/> stays true until the use is booked: the cooldown starts only on
/// <see cref="Fired"/>, a shot that actually left. A <see cref="Withheld"/> shot (no target, no origin, no owner) keeps the volley ready and only holds the next ask for a second, so a wave with no
/// attackers in reach costs one target read a second. <see cref="CommanderClock.Tick"/> resets its ready time the moment it answers, which a shot the director may refuse cannot use; commanders keep
/// that clock. The time is the run's wave seconds. A time that is not a number is never ready and books nothing.
/// </summary>
internal sealed class VolleyClock
{
    /// <summary>How long a withheld shot holds the next ask.</summary>
    public const float RetrySeconds = 1f;

    private readonly float _cooldown;
    private float _ready;
    private float _retryAt;

    public VolleyClock(float now, float firstSeconds, float cooldownSeconds)
    {
        _ready = now + firstSeconds;
        _cooldown = cooldownSeconds;
    }

    public bool Stopped { get; private set; }

    /// <summary>Ends the timer for good: it is never ready again. A tower with no owner stops it for the wave, and the clear drops it.</summary>
    public void Stop() => Stopped = true;

    /// <summary>True when a shot may be armed at <paramref name="now"/>: the first delay or the cooldown has run out and no withheld shot holds the ask.</summary>
    public bool Ready(float now) => !Stopped && now >= _ready && now >= _retryAt;

    /// <summary>The shot left: the cooldown runs from <paramref name="now"/>, and a hold from an earlier withheld shot is over.</summary>
    public void Fired(float now)
    {
        if (!IsNumber(now))
            return;

        _ready = now + _cooldown;
        _retryAt = 0f;
    }

    /// <summary>The shot was refused: the volley stays ready and is not asked for again until <see cref="RetrySeconds"/> from <paramref name="now"/>.</summary>
    public void Withheld(float now)
    {
        if (IsNumber(now))
            _retryAt = now + RetrySeconds;
    }

    /// <summary>Seconds to the next use by cooldown or first delay, at least 0; 0 once stopped. A retry hold is not counted.</summary>
    public float SecondsLeft(float now) => Stopped ? 0f : System.Math.Max(0f, _ready - now);

    private static bool IsNumber(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
