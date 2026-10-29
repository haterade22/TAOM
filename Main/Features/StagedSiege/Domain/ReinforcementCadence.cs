namespace TAOM.Features.StagedSiege.Domain;

/// <summary>
/// When each side may release a reinforcement batch: one batch every trickle interval, and a quick run of batches for a short
/// time after each captured point so the new stage is not fought with the old stage's stragglers.
/// </summary>
public sealed class ReinforcementCadence
{
    private readonly StagedSiegeTunables _tunables;
    private readonly float[] _next = { float.PositiveInfinity, float.PositiveInfinity };
    private float _burstUntil = float.NegativeInfinity;

    public ReinforcementCadence(StagedSiegeTunables tunables) => _tunables = tunables;

    private static int Slot(StagedSide side) => side == StagedSide.Attacker ? 0 : 1;

    /// <summary>The engine reset the timer: the next batch is one trickle interval away.</summary>
    public void Reset(StagedSide side, float now) => _next[Slot(side)] = now + _tunables.TrickleSeconds;

    /// <summary>A point was captured: both sides are due at once, and the burst interval applies for a while.</summary>
    public void OnStageChanged(float now)
    {
        _burstUntil = now + _tunables.RefillBurstSeconds;
        _next[0] = now;
        _next[1] = now;
    }

    /// <summary>Whether a batch is due. False for a NaN clock and before the first reset.</summary>
    public bool Due(StagedSide side, float now) => now >= _next[Slot(side)];

    /// <summary>A batch was released: schedule the next one.</summary>
    public void Fired(StagedSide side, float now) =>
        _next[Slot(side)] = now + (now < _burstUntil ? _tunables.RefillBurstIntervalSeconds : _tunables.TrickleSeconds);
}
