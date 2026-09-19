namespace TAOM.Features.Elephant;

/// <summary>
/// Paces the howdah status line: fires once per period of accumulated mission time (#627). A dt that is not a
/// positive finite number is ignored, because a NaN folded into the accumulator would never compare >= period again
/// and would silence the log for the rest of the battle. A long hitch fires once, not in a burst.
/// </summary>
public sealed class HowdahSampleClock
{
    private readonly float _period;
    private float _elapsed;
    private bool _pendingFirst;

    public HowdahSampleClock(float period, bool fireOnFirstTick = false)
    {
        _period = period;
        _pendingFirst = fireOnFirstTick;
    }

    public bool Tick(float dt)
    {
        if (dt > 0f && !float.IsPositiveInfinity(dt)) _elapsed += dt;
        if (_pendingFirst)
        {
            _pendingFirst = false;
            _elapsed = 0f;
            return true;
        }
        if (!(_elapsed >= _period)) return false;
        _elapsed = 0f;
        return true;
    }
}
