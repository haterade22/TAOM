using System;
using TAOM.Core.Validation;

namespace TAOM.Features.DreadAura.Domain;

/// <summary>
/// The morale a one-shot fear burst takes from one victim. Shared by the signature strikes and
/// the hero abilities so the two cannot drift. Every gate is a positive requirement, so a NaN
/// input fails closed (csharp-architecture.md "Engine-Float Decision Gates").
/// </summary>
public static class FearDrainMath
{
    public static float Compute(float scaledFear, float falloff, float raceResist, float currentMorale)
    {
        if (!FiniteFloatValidator.IsFinite(scaledFear)
            || !FiniteFloatValidator.IsFinite(falloff)
            || !FiniteFloatValidator.IsFinite(raceResist)
            || !FiniteFloatValidator.IsFinite(currentMorale))
            return 0f;

        // GetMorale() answers -1f for an agent with no CommonAIComponent; that is "no morale",
        // never "already drained".
        if (!(currentMorale > 0f))
            return 0f;

        var drain = scaledFear * falloff * raceResist;
        if (!(drain > 0f))
            return 0f;

        return Math.Min(drain, currentMorale);
    }
}
