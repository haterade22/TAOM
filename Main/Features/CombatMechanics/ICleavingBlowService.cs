using TAOM.Features.CombatMechanics.Domain;

namespace TAOM.Features.CombatMechanics;

// Cleaving Blows: a two-handed swing carries on to the next enemy. The momentum a hit leaves is what the engine scales the next
// hit of the swing by.
public interface ICleavingBlowService
{
    /// <summary>
    /// The momentum left after this hit, or null when the rule does not own it (vanilla then decides). A returned 0 means the
    /// rule owns the hit and the swing stops here (the floor or the target cap).
    /// </summary>
    float? CalculateMomentum(in CleavingBlowContext context, float originalMomentum);

    /// <summary>True when the hit qualified and left momentum, so the weapon should slice through instead of sticking.</summary>
    bool ShouldSliceThrough(in CleavingBlowContext context, float momentumRemaining);
}
