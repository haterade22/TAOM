namespace TAOM.Features.LongDefeat.Domain;

internal enum StalemateAction
{
    None,
    Charge,
    FellStragglers,
    NoResult,
}

/// <param name="Action">What to do this tick.</param>
/// <param name="Count">For FellStragglers, how many attackers fall.</param>
internal readonly record struct StalemateDecision(StalemateAction Action, int Count = 0);

/// <summary>Seconds without a death, and whether the charge has been ordered for this wave.</summary>
internal struct StalemateState
{
    public float Quiet;
    public bool Charging;
}

/// <summary>
/// After a quiet spell both sides charge; after a second quiet spell a handful of stragglers nobody reaches fall, and anything bigger
/// is a stalemate. NoResult repeats every tick until the run ends.
/// </summary>
internal static class StalemateRule
{
    internal static StalemateDecision Step(ref StalemateState state, float dt, bool lossThisTick,
        int attackers, int defenders, int waveCount, RunRules r)
    {
        if (!(dt > 0f) || float.IsInfinity(dt))
            return default;

        if (lossThisTick)
        {
            state.Quiet = 0f;
            return default;
        }

        state.Quiet += dt;
        if (attackers > 0 && attackers <= r.StragglerShare * waveCount && attackers < defenders && state.Quiet >= r.StragglerQuietSeconds)
        {
            state.Quiet = 0f;
            return new StalemateDecision(StalemateAction.FellStragglers, attackers);
        }

        if (state.Charging)
        {
            if (!(state.Quiet >= r.StrandedSeconds) || attackers <= 0)
                return default;

            if (attackers <= r.StragglerShare * waveCount && attackers < defenders)
            {
                state.Quiet = 0f;
                return new StalemateDecision(StalemateAction.FellStragglers, attackers);
            }

            return new StalemateDecision(StalemateAction.NoResult);
        }

        var limit = attackers * 2 <= waveCount ? r.QuietSeconds : r.QuietAnySeconds;
        if (!(state.Quiet >= limit))
            return default;

        state.Charging = true;
        state.Quiet = 0f;
        return new StalemateDecision(StalemateAction.Charge);
    }
}
