using System;

namespace TAOM.Features.CombatMechanics;

/// <summary>
/// The damage a shield-blocked missile needs to pass the shield. The engine passes it on when the flag is set and
/// the damage is above offset + factor * armour (Mission.cs:5831-5845; managed_core_parameters.xml gives 30 and 3).
/// </summary>
public static class ShieldPierceMath
{
    /// <summary>The larger of the damage and the next whole number above the threshold. A non-finite input or a negative armour returns the damage.</summary>
    public static float Floor(float damage, float offset, float factor, float armour)
    {
        if (!IsFinite(damage) || !IsFinite(offset) || !IsFinite(factor) || !IsFinite(armour) || armour < 0f)
            return damage;
        var threshold = Math.Ceiling((double)offset + (double)factor * armour) + 1.0;
        return damage >= threshold ? damage : (float)threshold;
    }

    private static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
}
