using TAOM.Core.Validation;

namespace TAOM.Features.CultureDoctrine.Doctrines;

/// <summary>When a doctrine's archers may loose. Fractions of the formation's average adjusted
/// missile range: fire once the closest enemy is inside <see cref="ReleaseFraction"/>, stop once
/// it is back outside <see cref="HoldFraction"/>. The gap is hysteresis against a target that
/// hovers at the edge.</summary>
public readonly struct VolleyTunables
{
    public VolleyTunables(float releaseFraction, float holdFraction)
    {
        ReleaseFraction = releaseFraction;
        HoldFraction = holdFraction;
    }

    /// <summary>Vanilla: fire at will at any range.</summary>
    public static readonly VolleyTunables FireAtWill = new VolleyTunables(float.PositiveInfinity, float.PositiveInfinity);

    public float ReleaseFraction { get; }
    public float HoldFraction { get; }

    public bool IsFireAtWill => float.IsPositiveInfinity(ReleaseFraction);
}

/// <summary>
/// Volley control as one pure step. The engine fires at will from the first tick and every
/// vanilla behaviour resets the firing order to that when it activates
/// (<c>BehaviorSkirmishLine.OnBehaviorActivatedAux</c> and the rest), so the discipline lives in
/// the tactic's own once-a-second tick: hold until the enemy is inside the release range, where
/// the engine's own shooter error makes the arrows count, then fire at will. Every comparison is
/// a positive requirement: a NaN distance or range keeps the current order.
/// </summary>
public static class VolleyDecision
{
    /// <summary>The firing order for this tick given the last one.</summary>
    public static bool ShouldFire(bool firingNow, bool hasEnemy, float distance, float rangeAdjusted, in VolleyTunables t)
    {
        if (t.IsFireAtWill)
            return true;
        if (!hasEnemy)
            return false;
        if (!FiniteFloatValidator.IsFinite(distance) || !FiniteFloatValidator.IsFinite(rangeAdjusted) || !(rangeAdjusted > 0f))
            return firingNow;
        if (!firingNow)
            return distance <= rangeAdjusted * t.ReleaseFraction;
        return distance <= rangeAdjusted * t.HoldFraction;
    }
}
