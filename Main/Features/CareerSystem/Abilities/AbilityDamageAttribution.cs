namespace TAOM.Features.CareerSystem.Abilities;

/// <summary>
/// Issue #383 — pure math for the combat-log "+N from ability" line. The ability's
/// damage buff is applied as <c>DamageMultiplierBonus += f</c> (a driven property the
/// engine bakes into the final number), so the buff's share of a hit is recovered as
/// <c>damagedHp * f / (1 + f)</c>. The bonus value comes from TAOM's own applied buff
/// state (CareerAbilityBuffTracker), never derived by reflection. Exact while the
/// ability buff is the sole multiplicative term on that path — the passive Damage
/// effect goes through CalculateDamageAmplification, a different mechanism this line
/// deliberately does not claim.
/// </summary>
public static class AbilityDamageAttribution
{
    public static float ComputeBonusDamage(float damagedHp, float damageBonusFraction)
    {
        // Positive-requirement gates: NaN/±Inf/non-positive inputs all fail (NaN-gate rule).
        if (!(damagedHp > 0f) || float.IsInfinity(damagedHp)) return 0f;
        if (!(damageBonusFraction > 0f) || float.IsInfinity(damageBonusFraction)) return 0f;

        return damagedHp * damageBonusFraction / (1f + damageBonusFraction);
    }

    public static bool ShouldReport(float bonusDamage, float minReportable)
        => bonusDamage >= minReportable && !float.IsNaN(bonusDamage);
}
