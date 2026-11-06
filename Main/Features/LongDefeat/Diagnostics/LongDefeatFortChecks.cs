using System;
using System.Globalization;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Diagnostics;

/// <summary>
/// The fortification verdicts of #1079 (phase3-plan.md section 6): <c>field-care</c> so far. Pure: the director reads the field and the purse, these turn the numbers into pass, fail or skip. A gate
/// on a float is written as the requirement, so a NaN fails it.
/// </summary>
internal static class LongDefeatFortChecks
{
    /// <summary>The mean may differ from the sum by this share of the limit: <c>Agent.Health</c>'s setter ceils (E23), so a soldier gains up to one whole point, about 1% of a 100 health limit and half that on average.</summary>
    internal const float CareTolerance = 0.01f;

    /// <summary>
    /// A heal must raise the mean by more than this (twice <see cref="CareTolerance"/>) for a pass to mean anything: a write that did nothing misses the expected mean by exactly the heal's rise, so a smaller
    /// rise sits inside the tolerance and a blank write would pass.
    /// </summary>
    private const float ProvableRise = 2f * CareTolerance;

    /// <summary>
    /// Heal <paramref name="n"/> of the run. <paramref name="rest"/> is the field's own count and read-back around the write: the mean health share of the standing defenders after must equal
    /// the mean before plus <paramref name="share"/> of what was missing, within <see cref="CareTolerance"/>. <paramref name="reread"/> is the next tick's reading of the same mean, or null when the run
    /// ended first (the only exemption): a reading that is not a mean (negative for nothing read, NaN) fails, and one that is must still hold the heal. <paramref name="paid"/> is the gold of the care
    /// purchases this heal carries and <paramref name="purseFell"/> what the purse's care sink grew by; they must be equal and not zero. A garrison missing too little for the heal to show is a skip.
    /// </summary>
    internal static CheckVerdict FieldCare(int n, float share, LdRest rest, float? reread, int paid, long purseFell)
    {
        var expected = rest.MeanBefore + share * (1f - rest.MeanBefore);
        var detail = $"c={Num(n)} share={F2(share)} count={Num(rest.Count)} before={F3(rest.MeanBefore)} after={F3(rest.MeanAfter)} expected={F3(expected)} "
            + $"reread={(reread is { } r ? F3(r) : "none")} paid={Num(paid)} purse={Num(purseFell)}";
        if (rest.Count > 0 && share * (1f - rest.MeanBefore) <= ProvableRise)
            return new CheckVerdict(CheckResult.Skip, detail + " reason=too-little-missing");

        var healed = rest.Count > 0 && Math.Abs(rest.MeanAfter - expected) <= CareTolerance;
        var held = reread is not { } read || (read >= 0f && Math.Abs(read - rest.MeanAfter) <= CareTolerance);
        var books = paid > 0 && purseFell == paid;
        return LongDefeatChecks.Verdict(healed && held && books, detail);
    }

    private static string Num(long v) => v.ToString(CultureInfo.InvariantCulture);

    private static string F2(float v) => v.ToString("F2", CultureInfo.InvariantCulture);

    private static string F3(float v) => v.ToString("F3", CultureInfo.InvariantCulture);
}
