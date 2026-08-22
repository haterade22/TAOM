using System;
using TAOM.Core.Validation;

namespace TAOM.Features.FieldCamp;

/// <summary>
/// The armed-ambush decision maths. Pure; the caller (CampService) owns the scan cadence and the
/// candidate selection, this service only prices one spring attempt.
/// </summary>
public class CampAmbushService : ICampAmbushService
{
    /// <summary>How much of the chance rides on the enemy walking deep into the trap.</summary>
    private const float ConcealmentWeight = 0.3f;

    /// <summary>One Scouting point buys 1/300 extra chance (source module constant).</summary>
    private const float ScoutingDivisor = 300f;

    public float AmbushedMoraleFactor => 0.5f;

    public float TriggerChance(float candidateSpottingDistance, float maxRange, float baseChance, float scoutingSkill)
    {
        // Any poisoned input zeroes the whole chance rather than rolling dice on garbage.
        if (!FiniteFloatValidator.IsFinite(candidateSpottingDistance)
            || !FiniteFloatValidator.IsFinite(maxRange)
            || !FiniteFloatValidator.IsFinite(baseChance)
            || !FiniteFloatValidator.IsFinite(scoutingSkill))
        {
            return 0f;
        }

        // Positive requirement on the range: a zero or negative max range cannot form the
        // concealment ratio, so no ambush springs at all. (The source kept base + scouting here;
        // an ambush with a degenerate reach springing anyway is the less defensible read, and the
        // settings provider clamps the range to [1, 30] in production.)
        if (!(maxRange > 0f))
            return 0f;

        float concealment = 1f - Math.Min(1f, Math.Max(0f, candidateSpottingDistance) / maxRange);
        float chance = baseChance + concealment * ConcealmentWeight + scoutingSkill / ScoutingDivisor;
        if (!FiniteFloatValidator.IsFinite(chance))
            return 0f;
        return Math.Max(0f, Math.Min(1f, chance));
    }
}
