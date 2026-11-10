namespace TAOM.Features.HeroDefence.Domain;

/// <summary>When a pack counts as stalled. The defaults are first guesses (plan "How every fight ends"; run 5, 2026-11-09).</summary>
internal sealed record PackStallLimits
{
    /// <summary>No progress and no blow for this long is a stall.</summary>
    public float WindowSeconds { get; init; } = 40f;

    /// <summary>The forward gain along the road that counts as progress. A marching foot pack covers it in about 1.6 s.</summary>
    public float StepMetres { get; init; } = 5f;
}

/// <summary>
/// One pack's stall clock. The pack stalls when its forward progress along the road has not grown by one step, and no blow given or
/// taken involved it (blocked hits included), for the window. The director feeds it mission time, the pack's mean progress (from the
/// forward-only waypoint index, over the creeps that have not arrived) and the number of creeps in that mean, and removes a stalled pack
/// through <see cref="HeroDefenceRun.PackStalled"/>.
/// </summary>
internal sealed class PackStallClock
{
    private readonly PackStallLimits _limits;

    /// <summary>The progress at the last step. Only a gain over it counts, so a pack pushed back must pass it again.</summary>
    private float _mark;

    /// <summary>The time of the last step or blow.</summary>
    private float _activeAt;

    /// <summary>The creeps in the last mean. A creep that leaves the mean moves it by itself.</summary>
    private int _creeps;

    /// <summary>The last finite progress reading.</summary>
    private float _last;

    /// <summary>A NaN or infinite start value waits for the first finite reading.</summary>
    public PackStallClock(PackStallLimits limits, float now, float progress, int creeps)
    {
        _limits = limits;
        _activeAt = IsFinite(now) ? now : float.NaN;
        _mark = IsFinite(progress) ? progress : float.NaN;
        _last = _mark;
        _creeps = creeps;
    }

    /// <summary>
    /// Takes one reading and returns true while the pack is stalled. A reading at a NaN or infinite time changes nothing. When the
    /// number of creeps changes (one arrived or fell), the jump in the mean is taken out: the mark moves with it, so the gain or loss
    /// since the mark still counts, and the change itself is no activity.
    /// </summary>
    public bool Observe(float now, float progress, int creeps, bool blow)
    {
        if (!IsFinite(now))
            return false;

        if (float.IsNaN(_activeAt))
            _activeAt = now;

        if (creeps != _creeps)
        {
            _creeps = creeps;
            _mark = IsFinite(progress) && !float.IsNaN(_mark) ? progress - (_last - _mark) : float.NaN;
        }

        if (IsFinite(progress))
        {
            if (float.IsNaN(_mark))
            {
                _mark = progress;
            }
            else if (progress - _mark >= _limits.StepMetres)
            {
                _mark = progress;
                _activeAt = now;
            }

            _last = progress;
        }

        if (blow)
            _activeAt = now;

        return now - _activeAt >= _limits.WindowSeconds;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
