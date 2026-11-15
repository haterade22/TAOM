using System;
using TAOM.Core.Validation;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.BattleBalance;

/// <summary>
/// How much troop quality counts in auto-resolve (#1283, docs/features/battle-balance.md "Auto-resolve: numbers over quality").
/// Vanilla's strike damage is 40 x (striker power / struck power)^0.7 (<c>DefaultCombatSimulationModel.SimulateHit</c>). In a
/// field battle each side strikes about N^0.6 times a round, so a side's strength goes as N^1.6 x P^1.4; rescaling the
/// ratio to an exponent e gives N^1.6 x P^(2e), and at the shipped 0.5 numbers outweigh quality. A siege assault on a town
/// with more than 100 defenders, or a castle with more than 30, gives the attackers a strike count that does not grow with
/// their number (<c>GetSimulationTicksForBattleRound</c>), so there only the per-strike weight changes. The model passes in
/// the two powers.
/// </summary>
public static class AutoResolveQualityMath
{
    /// <summary>The exponent vanilla raises the power ratio to (<c>DefaultCombatSimulationModel.SimulateHit</c>, line 21).</summary>
    public const float VanillaPowerRatioExponent = 0.7f;

    /// <summary>
    /// The multiplier that turns vanilla's ratio^0.7 into ratio^exponent: ratio^(exponent - 0.7). 1 when either power is not
    /// positive and finite, or the exponent is outside [0, 1] (the provider rejects such a value at load; this is the gate).
    /// </summary>
    public static float DamageFactor(float strikerPower, float struckPower, float exponent)
    {
        if (!FiniteFloatValidator.IsFiniteInRange(exponent, 0f, 1f))
            return 1f;
        if (!(strikerPower > 0f) || !(struckPower > 0f) || !FiniteFloatValidator.IsFinite(strikerPower) || !FiniteFloatValidator.IsFinite(struckPower))
            return 1f;

        double factor = Math.Pow(strikerPower / (double)struckPower, exponent - VanillaPowerRatioExponent);
        return FiniteFloatValidator.IsFinite((float)factor) && factor > 0.0 ? (float)factor : 1f;
    }

    /// <summary>
    /// The addend for <c>ExplainedNumber.AddFactor</c> that multiplies the FINAL number by <paramref name="multiplier"/>.
    /// ExplainedNumber adds factors against the base (result = base x (1 + sum)), so the addend is
    /// (multiplier - 1) x result / base, the translation <c>RefugeDamageReduction</c> uses. Exact only while nothing clamps the
    /// number; SimulateHit sets no limit. 0 when there is nothing to scale.
    /// </summary>
    public static float BaseRelativeAddend(float baseNumber, float resultNumber, float multiplier)
    {
        if (!(baseNumber > 0f) && !(baseNumber < 0f))
            return 0f;
        if (!FiniteFloatValidator.IsFinite(resultNumber) || !FiniteFloatValidator.IsFinite(multiplier) || multiplier == 1f)
            return 0f;

        float addend = (multiplier - 1f) * (resultNumber / baseNumber);
        return FiniteFloatValidator.IsFinite(addend) ? addend : 0f;
    }

    /// <summary>
    /// Multiplies the final strike damage by <see cref="DamageFactor"/>: the striker's power first, the struck troop's second.
    /// At the vanilla 0.7, or for an invalid input, the number does not change.
    /// </summary>
    public static void Apply(ref ExplainedNumber result, float strikerPower, float struckPower, float exponent) =>
        result.AddFactor(BaseRelativeAddend(result.BaseNumber, result.ResultNumber, DamageFactor(strikerPower, struckPower, exponent)));
}
