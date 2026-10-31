namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Countdown, then live time. Elapsed and Minute count live time only, so income, wave tiers and respawn timers
/// all start from the moment the match goes live.
/// </summary>
internal sealed class MatchClock
{
    private float _countdownRemaining;

    public MatchClock(float countdownSeconds)
    {
        _countdownRemaining = countdownSeconds > 0f ? countdownSeconds : 0f;
        Phase = _countdownRemaining > 0f ? MatchPhase.Countdown : MatchPhase.Live;
    }

    public MatchPhase Phase { get; private set; }

    /// <summary>Seconds of live play so far.</summary>
    public float Elapsed { get; private set; }

    public float Minute => Elapsed / 60f;

    public float CountdownRemaining => _countdownRemaining;

    /// <summary>Moves time forward. A non-positive or NaN step, or a clock that has ended, changes nothing.</summary>
    public void Advance(float dt)
    {
        if (!(dt > 0f) || Phase == MatchPhase.Ended)
            return;

        if (Phase == MatchPhase.Countdown)
        {
            if (dt < _countdownRemaining)
            {
                _countdownRemaining -= dt;
                return;
            }

            dt -= _countdownRemaining;
            _countdownRemaining = 0f;
            Phase = MatchPhase.Live;
        }

        Elapsed += dt;
    }

    public void End() => Phase = MatchPhase.Ended;
}
