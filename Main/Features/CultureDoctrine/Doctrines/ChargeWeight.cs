using TAOM.Core.Validation;

namespace TAOM.Features.CultureDoctrine.Doctrines;

/// <summary>
/// The weight of a foot charge, vanilla's own curve so the plan rows written against
/// <c>BehaviorTacticalCharge</c> keep their meaning when <c>BehaviorFootCharge</c> takes the row
/// (`BehaviorTacticalCharge.cs:284-330`, the infantry branch): 0.8 at ten seconds from the
/// target rising to 1 at four; times 1.2 between 1.5 and 4 s out (the moment to go); times 1.2
/// when the target is not coming for us; times 0.5 when the target is horse, vanilla's class
/// factor for infantry against cavalry. Vanilla's slope term (0.9 to 1.1 by the navmesh height
/// difference) is left out: a native query per weigh for a tenth either way. Pure; a NaN
/// distance weighs 0.
/// </summary>
public static class ChargeWeight
{
    public const float FarSeconds = 10f;
    public const float NearSeconds = 4f;
    public const float GoSeconds = 1.5f;

    public static float Foot(float secondsToTarget, bool targetNotOnUs, bool targetIsHorse)
    {
        if (!FiniteFloatValidator.IsFinite(secondsToTarget))
            return 0f;
        var t = secondsToTarget < NearSeconds ? NearSeconds : secondsToTarget > FarSeconds ? FarSeconds : secondsToTarget;
        var weight = 0.8f + 0.2f * (1f - (t - NearSeconds) / (FarSeconds - NearSeconds));
        if (secondsToTarget <= NearSeconds && secondsToTarget >= GoSeconds)
            weight *= 1.2f;
        if (secondsToTarget <= NearSeconds && targetNotOnUs)
            weight *= 1.2f;
        if (targetIsHorse)
            weight *= 0.5f;
        return weight;
    }
}
