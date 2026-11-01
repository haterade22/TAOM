using System;
using System.Collections.Generic;
using System.Globalization;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>
/// The log lines a fitted layout writes at match start: each planner attempt, the fit totals, one line per lane, one per
/// slid or dropped tower, and how long planning and building the towers took. Pure text, invariant culture, so a test pins the format.
/// </summary>
internal static class FordsLayoutFitLines
{
    private const string Prefix = "[FordsOfWar][Layout] ";

    internal static IEnumerable<string> Of(IReadOnlyList<FordsLayoutAttempt> attempts, FordsLayoutFit fit)
    {
        foreach (var attempt in attempts)
            yield return Prefix + "attempt " + attempt.Summary;

        if (fit.Moved + fit.Dropped + fit.Unfixed > 0)
            yield return $"[FordsOfWar] layout fitted to the scene: {fit.Moved} point(s) moved onto the navmesh, {fit.Dropped} dropped, {fit.Unfixed} left on bad ground";

        foreach (var lane in fit.Lanes)
        {
            yield return Prefix + "lane=" + I(lane.Lane) + " waypoints=" + I(lane.Waypoints) + " kept=" + I(lane.Kept) + " moved=" + I(lane.Moved)
                + " dropped=" + I(lane.Dropped) + " maxGap=" + lane.MaxGap.ToString("F1", CultureInfo.InvariantCulture)
                + " badSegments=" + I(lane.BadSegments);
        }

        foreach (var move in fit.MovedTowers)
            yield return Prefix + "tower " + move.Id + " slid " + move.Distance.ToString("F1", CultureInfo.InvariantCulture) + " m along its lane";

        if (fit.DroppedTowers.Count > 0)
            yield return Prefix + "dropped towers: " + string.Join(", ", fit.DroppedTowers);
    }

    /// <summary>How long the layout took at match start: planning it (generate, fit, retry) and building its tower entities.</summary>
    internal static string Timing(TimeSpan plan, TimeSpan towers) =>
        Prefix + "timing plan=" + Ms(plan) + " ms towers=" + Ms(towers) + " ms";

    private static string Ms(TimeSpan span) => span.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture);

    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
}
