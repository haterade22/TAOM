using TaleWorlds.MountAndBlade;

namespace TAOM.Features.HeroAbilities.Hooks;

/// <summary>
/// The zero-aim-error pass of a ranged hero kit (#1361, decision R6: "never misses" is zero aim error, no homing arrow).
/// These are the ten properties the engine's stat models write for aim error (AgentStatCalculateModel.cs:170, 198,
/// 220-223, 235; CustomBattleAgentStatCalculateModel.cs:235-309, 359). Native reads them; no managed reader exists in the
/// decompiled set. Zeroing AiShooterError alone is not enough (RaceAbilityStatApplier.cs:34 touches only that one).
/// </summary>
internal static class HeroPrecisionApplier
{
    internal static void Apply(AgentDrivenProperties p)
    {
        p.AiShooterError = 0f;
        p.AiShooterErrorWoRangeUpdate = 0f;
        p.AiRangerLeadErrorMin = 0f;
        p.AiRangerLeadErrorMax = 0f;
        p.AiRangerVerticalErrorMultiplier = 0f;
        p.AiRangerHorizontalErrorMultiplier = 0f;
        p.WeaponInaccuracy = 0f;
        p.WeaponMaxMovementAccuracyPenalty = 0f;
        p.WeaponMaxUnsteadyAccuracyPenalty = 0f;
        p.WeaponRotationalAccuracyPenaltyInRadians = 0f;
    }

    // The precision check's count of the ten that are not 0. A NaN is not 0, so it fails the check.
    internal static int CountNonZero(AgentDrivenProperties p) =>
        NonZero(p.AiShooterError) + NonZero(p.AiShooterErrorWoRangeUpdate) + NonZero(p.AiRangerLeadErrorMin)
        + NonZero(p.AiRangerLeadErrorMax) + NonZero(p.AiRangerVerticalErrorMultiplier) + NonZero(p.AiRangerHorizontalErrorMultiplier)
        + NonZero(p.WeaponInaccuracy) + NonZero(p.WeaponMaxMovementAccuracyPenalty) + NonZero(p.WeaponMaxUnsteadyAccuracyPenalty)
        + NonZero(p.WeaponRotationalAccuracyPenaltyInRadians);

    private static int NonZero(float value) => value == 0f ? 0 : 1;
}
