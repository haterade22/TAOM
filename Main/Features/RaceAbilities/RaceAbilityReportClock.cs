namespace TAOM.Features.RaceAbilities;

// When the battle report goes to the log: every 30 s of mission time, and only when something new fired
// since the last one, so a quiet battle writes nothing. Main thread only.
public sealed class RaceAbilityReportClock
{
    public const float IntervalSeconds = 30f;

    private float _next = IntervalSeconds;
    private long _reportedActivations;

    public bool Due(float now, long activations)
    {
        if (!(now >= _next))
            return false;
        _next = now + IntervalSeconds;
        if (activations == _reportedActivations)
            return false;
        _reportedActivations = activations;
        return true;
    }

    public void Reset()
    {
        _next = IntervalSeconds;
        _reportedActivations = 0;
    }
}
