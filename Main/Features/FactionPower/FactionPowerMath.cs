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
}
