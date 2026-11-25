using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Core.Validation;

namespace TAOM.Features.FactionPower;

/// <summary>
/// The arithmetic of the faction power slider, engine-free. A percent of 100 is the exact current behaviour: every
/// entry point returns before it touches the number. ExplainedNumber factors sum, so a plain AddFactor(m - 1) would give
/// base * (1 + feats + m - 1) and not the final number times m; <see cref="MultiplyResult"/> scales the final number.
/// </summary>
public static class FactionPowerMath
{
    public const int MinPercent = 50;
    public const int MaxPercent = 200;
    public const int NeutralPercent = 100;

    /// <summary>
    /// Vanilla's militia retirement rate: DefaultSettlementMilitiaModel.CalculateMilitiaChangeInternal line 98,
    /// <c>(0f - militia) * 0.025f</c>, added as the "Retired" row. Militia has no cap, so it settles where the daily inflow
    /// equals 0.025 times the militia: the equilibrium is 40 times the inflow.
    /// </summary>
    public const float MilitiaRetireRate = 0.025f;

    private const float MinFrameScale = 0.01f;

    public static bool IsValidPercent(int percent) => percent >= MinPercent && percent <= MaxPercent;

    public static float ToMultiplier(int percent) => percent / 100f;

    public static void MultiplyResult(ref ExplainedNumber number, float multiplier, TextObject? text)
    {
        if (!FiniteFloatValidator.IsFiniteInRange(multiplier, MinPercent / 100f, MaxPercent / 100f) || multiplier == 1f)
            return;

        float scale = 1f + number.SumOfFactors;
        if (!(scale > MinFrameScale))
            return;

        number.AddFactor((multiplier - 1f) * scale, text);
    }

    private static bool IsApplicable(float multiplier) =>
        FiniteFloatValidator.IsFiniteInRange(multiplier, MinPercent / 100f, MaxPercent / 100f) && multiplier != 1f;

    /// <summary>
    /// The extra militia per day that moves the equilibrium from 40 x inflow to 40 x inflow x m: the inflow is scaled and the
    /// retirement rate is kept. The inflow is the day's change plus what retires: the "Retired" row is added to the base
    /// number, so it reaches the result times (1 + <paramref name="sumOfFactors"/>). Zero for a multiplier of 1, a
    /// non-finite input or an inflow that is not positive.
    /// </summary>
    public static float MilitiaDelta(float changeResult, float militia, float sumOfFactors, float multiplier)
    {
        if (!IsApplicable(multiplier) || !FiniteFloatValidator.IsFinite(changeResult) || !FiniteFloatValidator.IsFinite(militia)
            || !FiniteFloatValidator.IsFinite(sumOfFactors))
            return 0f;

        float inflow = changeResult + MilitiaRetireRate * militia * (1f + sumOfFactors);
        return inflow > 0f ? (multiplier - 1f) * inflow : 0f;
    }

    /// <summary>
    /// Adds a signed amount that lands as that many points in the final result. ExplainedNumber's result is
    /// base + base x factors, so a plain Add is multiplied by the factors; this divides them out first. Unchanged for a
    /// non-finite amount or a frame scale near zero.
    /// </summary>
    public static void AddResultFrameDelta(ref ExplainedNumber number, float delta, TextObject? text)
    {
        if (delta == 0f || !FiniteFloatValidator.IsFinite(delta))
            return;

        float scale = 1f + number.SumOfFactors;
        if (!(scale > MinFrameScale))
            return;

        number.Add(delta / scale, text);
    }

    /// <summary>
    /// The auto-resolve damage multiplier of a striker's realm: the square root of the multiplier (k = 0.5). Numbers already
    /// move strength by about m^1.6 through levers 1 to 3, so a full factor here would make 200 percent about six times as
    /// strong. With k = 0.5: 50 percent gives 0.23 of today's strength, 75 gives 0.55, 125 gives 1.60, 150 gives 2.34 and
    /// 200 gives 4.29, when every roster fills. Exactly 1 for a multiplier of 1.
    /// </summary>
    public static float StrikeMultiplier(float multiplier) =>
        IsApplicable(multiplier) ? (float)Math.Sqrt(multiplier) : 1f;

    /// <summary>
    /// The wage multiplier of a realm above 100 percent: 1 / m, so a full 200 percent party costs what a full 100 percent
    /// party does. Exactly 1 at or below 100 percent: a weaker realm pays normal wages.
    /// </summary>
    public static float WageMultiplier(float multiplier) =>
        IsApplicable(multiplier) && multiplier > 1f ? 1f / multiplier : 1f;

    /// <summary>A whole count (the militia spawned after a defended siege) times the multiplier, rounded. Unchanged for a multiplier of 1 or out of range.</summary>
    public static int ScaleCount(int count, float multiplier) =>
        IsApplicable(multiplier) ? (int)Math.Round(count * multiplier) : count;
}
