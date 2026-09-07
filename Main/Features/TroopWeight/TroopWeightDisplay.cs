using System;
using System.Globalization;
using TAOM.Core.Validation;

namespace TAOM.Features.TroopWeight;

/// <summary>
/// Pure presentation arithmetic for the 2026-09-06 "usage frame" reframe. Engine-free; unit-tested.
///
/// The elite tax is ENFORCED by deflating the party-size limit
/// (<see cref="TroopWeightService.ApplyPartySizeWeightPenalty"/>) and that has not changed. It is now
/// DISPLAYED on the other side of the fraction: a capacity readout shows
/// <c>weighted-used / true-base</c> (19 / 20) instead of <c>raw-used / deflated</c> (10 / 11), because
/// players read a shrinking denominator as "my party got smaller", which is the opposite of the intent
/// ("this troop takes more space").
///
/// The two frames are the same cap:
/// <code>raw &gt; deflated  ⟺  raw &gt; base − surplus  ⟺  raw + surplus &gt; base  ⟺  weighted &gt; base</code>
/// which is why vanilla's own over-capacity warning flags (all of which compare raw against the deflated
/// limit) stay correct without being touched. Nothing here may be used to make a gameplay decision.
/// </summary>
public static class TroopWeightDisplay
{
    /// <summary>
    /// The numerator: the weighted body cost, falling back to the raw count whenever weighting produced
    /// nothing larger (feature off, all weight-1 troops, or an empty/failed roster walk returning 0 —
    /// <see cref="TroopWeightService.CalculateWeightedRosterCount"/> returns 0f on error, and rendering
    /// "0 / 20" for a real party would be a worse lie than rendering the raw count).
    /// </summary>
    public static int DisplayUsed(int rawCount, int weightedCount)
        => weightedCount > rawCount ? weightedCount : rawCount;

    /// <summary>
    /// The denominator: the party's TRUE (pre-deflation) size limit. Falls back to the deflated limit when
    /// no penalty is in effect, and — importantly — when <see cref="ITroopWeightService.GetTrueBaseSizeLimit"/>
    /// has no cached base yet and hands back the deflated value itself. Never invents a larger number.
    /// </summary>
    public static int DisplayLimit(int deflatedLimit, int trueBaseLimit)
        => trueBaseLimit > deflatedLimit ? trueBaseLimit : deflatedLimit;

    /// <summary>
    /// The per-row multiplier tag ("2", "1.5") shown next to a heavy troop so the header's weighted total
    /// is self-explanatory rather than looking like a miscount. Empty for weight 1.0 (the default for every
    /// unlisted troop — tagging those would put a "x1" on almost every row), for anything at or below 1.0,
    /// and for non-finite weights. Invariant culture: this is a bare number substituted into a localized
    /// template, not prose.
    /// </summary>
    public static string FormatWeightMultiplier(float weight)
    {
        if (!FiniteFloatValidator.IsFinite(weight))
            return string.Empty;
        if (!(weight > 1.0f))
            return string.Empty;

        // Integer weights are what TAOM ships (2.0 / 3.0 / 4.0); render them without a trailing ".0".
        return Math.Abs(weight - (float)Math.Round(weight)) < 0.001f
            ? ((int)Math.Round(weight)).ToString(CultureInfo.InvariantCulture)
            : weight.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
