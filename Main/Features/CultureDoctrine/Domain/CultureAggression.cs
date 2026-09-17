using TAOM.Core.Validation;

namespace TAOM.Features.CultureDoctrine.Domain;

/// <summary>
/// How a culture's soldiers fight once the lines meet: four multipliers on the AI decision
/// values the engine derives from skill in <c>AgentStatCalculateModel.SetAiRelatedProperties</c>
/// (`AgentStatCalculateModel.cs:165-229`), applied as a post-pass after the base model has run.
/// <see cref="Attack"/> scales <c>AIAttackOnDecideChance</c> (0.05..1 in the engine);
/// <see cref="Shield"/> scales <c>AiDefendWithShieldDecisionChanceValue</c> (0..2) and
/// <c>AiUseShieldAgainstEnemyMissileProbability</c> (0..1); <see cref="ShooterError"/> scales
/// <c>AiShooterError</c> and the four ranger error terms (below 1 shoots straighter);
/// <see cref="ChargeDistance"/> scales <c>AiChargeHorsebackTargetDistFactor</c> (a rider commits
/// to a charge target from further away). <c>Agent.Defensiveness</c> is deliberately not touched:
/// the formation writes it from the movement and arrangement orders (`Formation.cs:2842`), and
/// it is the engine's own channel for "this order is defensive". Immutable; read at spawn and on
/// every agent property refresh on the main thread.
/// </summary>
public sealed class CultureAggression
{
    public const float MinMultiplier = 0.25f;
    public const float MaxMultiplier = 4f;

    /// <summary>Vanilla: every multiplier 1, the post-pass changes nothing.</summary>
    public static readonly CultureAggression Vanilla = new CultureAggression(1f, 1f, 1f, 1f);

    public CultureAggression(float attack, float shield, float shooterError, float chargeDistance)
    {
        Attack = Sanitize(attack);
        Shield = Sanitize(shield);
        ShooterError = Sanitize(shooterError);
        ChargeDistance = Sanitize(chargeDistance);
    }

    public float Attack { get; }
    public float Shield { get; }
    public float ShooterError { get; }
    public float ChargeDistance { get; }

    public bool IsVanilla => Attack == 1f && Shield == 1f && ShooterError == 1f && ChargeDistance == 1f;

    private static float Sanitize(float multiplier) =>
        FiniteFloatValidator.IsFiniteInRange(multiplier, MinMultiplier, MaxMultiplier) ? multiplier : 1f;
}

/// <summary>The post-pass arithmetic on plain floats, so the boundary model stays a delegate and
/// the numbers are testable without an <c>AgentDrivenProperties</c>. Every result is clamped to
/// the range the engine itself produces, and a non-finite input comes back unchanged.</summary>
public static class AggressionMath
{
    public static float AttackChance(float engineValue, CultureAggression profile) =>
        Scaled(engineValue, profile.Attack, 0.05f, 1f);

    public static float ShieldDecision(float engineValue, CultureAggression profile) =>
        Scaled(engineValue, profile.Shield, 0f, 2f);

    public static float ShieldAgainstMissiles(float engineValue, CultureAggression profile) =>
        Scaled(engineValue, profile.Shield, 0f, 1f);

    /// <summary>Error terms have no upper bound in the engine (they are small radians and
    /// fractions); only the sign is preserved, so a negative lead error stays negative.</summary>
    public static float Error(float engineValue, CultureAggression profile) =>
        FiniteFloatValidator.IsFinite(engineValue) ? engineValue * profile.ShooterError : engineValue;

    public static float ChargeDistance(float engineValue, CultureAggression profile) =>
        Scaled(engineValue, profile.ChargeDistance, 0f, 20f);

    private static float Scaled(float value, float multiplier, float min, float max)
    {
        if (!FiniteFloatValidator.IsFinite(value))
            return value;
        var scaled = value * multiplier;
        return scaled < min ? min : scaled > max ? max : scaled;
    }
}
