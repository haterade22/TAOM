using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>Which axis of the playable rectangle the bases sit on: its longer side, or its shorter one.</summary>
internal enum FordsBaseAxis
{
    Long,
    Short,
}

/// <summary>
/// What a fit may cost before the planner calls it unplayable: the share of layout items dropped (camps excluded: no match
/// uses them), how many towers moved
/// farther than <see cref="FarMoveMetres"/>, and the share of lane segments the segment check called blocked. A rejected
/// fit never passes.
/// </summary>
internal sealed record FordsLayoutLimits(float MaxDroppedFraction = 0.10f, int MaxFarTowerMoves = 0, float MaxBadSegmentFraction = 0.05f,
    float FarMoveMetres = 30f);

/// <summary>One candidate layout the planner tried: how it was built, what the fit made of it and how it scored (lower is better).</summary>
internal sealed record FordsLayoutAttempt(float SpreadFactor, FordsBaseAxis Axis, MapLayout Candidate, FordsLayoutFit Fit, float Score, bool Passed,
    string Summary);

/// <summary>
/// What the planner settled on. <see cref="Chosen"/> is the first passing attempt; when none passed it is the best-scoring one,
/// its fit as the fitter left it: only the fitter rejects a layout (a lane lost every tower of one side), so a layout that just
/// misses the limits is still playable and the caller warns. It is null only when the builder gave no layout at all.
/// <see cref="Summary"/> is the chosen attempt's line, or every attempt's line after "no layout passed".
/// </summary>
internal sealed record FordsLayoutPlan(FordsLayoutAttempt? Chosen, IReadOnlyList<FordsLayoutAttempt> Attempts)
{
    public bool Passed => Chosen is { Passed: true };

    public string Summary => Passed ? Chosen!.Summary : "no layout passed; " + string.Join("; ", Attempts.Select(a => a.Summary));
}

/// <summary>
/// The retry ladder for a generated layout: the configured lane spread, then 0.7 and 0.5 of it, and, when the boundary's
/// long side is less than 1.25 times its short side (so the long axis is a coin toss), the same three spreads on the other
/// axis. The first candidate whose fit passes <see cref="FordsLayoutLimits"/> wins; otherwise the best-scoring one is returned
/// as fitted. Pure: the director supplies the builder and the fitter (a scene adapter call).
/// </summary>
internal static class FordsLayoutPlanner
{
    /// <summary>A boundary whose long/short ratio is under this has no clear long axis, so the short one is worth trying.</summary>
    public const float AlternateAxisBelowRatio = 1.25f;

    private const float RejectedPenalty = 1000f;
    private const float DroppedWeight = 100f;
    private const float BadSegmentWeight = 100f;
    private const float FarMoveWeight = 10f;

    private static readonly float[] SpreadFactors = { 1f, 0.7f, 0.5f };

    public static FordsLayoutPlan Plan(Func<float, FordsBaseAxis, MapLayout?> build, Func<MapLayout, FordsLayoutFit> fit, float aspectRatio,
        FordsLayoutLimits? limits = null)
    {
        limits ??= new FordsLayoutLimits();
        var axes = aspectRatio < AlternateAxisBelowRatio ? new[] { FordsBaseAxis.Long, FordsBaseAxis.Short } : new[] { FordsBaseAxis.Long };
        var attempts = new List<FordsLayoutAttempt>();
        foreach (var axis in axes)
        {
            foreach (var factor in SpreadFactors)
            {
                var candidate = build(factor, axis);
                if (candidate == null)
                    continue;

                var attempt = Judge(factor, axis, candidate, fit(candidate), limits);
                attempts.Add(attempt);
                if (attempt.Passed)
                    return new FordsLayoutPlan(attempt, attempts);
            }
        }

        return new FordsLayoutPlan(attempts.Count == 0 ? null : attempts.Aggregate((lowest, next) => next.Score < lowest.Score ? next : lowest),
            attempts);
    }

    private static FordsLayoutAttempt Judge(float factor, FordsBaseAxis axis, MapLayout candidate, FordsLayoutFit fit, FordsLayoutLimits limits)
    {
        var items = candidate.Lanes.Sum(l => l.Path.Count) + candidate.Towers.Count + candidate.CapturePoints.Count;
        var campsDropped = Math.Max(0, candidate.Camps.Count - fit.Layout.Camps.Count);
        var dropped = fit.Dropped - campsDropped;
        var droppedFraction = items == 0 ? 0f : dropped / (float)items;
        var farMoves = fit.MovedTowers.Count(m => m.Distance > limits.FarMoveMetres);
        var segments = fit.Lanes.Sum(l => Math.Max(0, l.Kept - 1));
        var bad = fit.Lanes.Sum(l => l.BadSegments);
        var badFraction = segments == 0 ? 0f : bad / (float)segments;

        var passed = !fit.Rejected && droppedFraction <= limits.MaxDroppedFraction && farMoves <= limits.MaxFarTowerMoves
            && badFraction <= limits.MaxBadSegmentFraction;
        var score = (fit.Rejected ? RejectedPenalty : 0f) + droppedFraction * DroppedWeight + farMoves * FarMoveWeight + badFraction * BadSegmentWeight;

        var inv = CultureInfo.InvariantCulture;
        var summary = $"spread x{factor.ToString("F2", inv)} on the {axis.ToString().ToLowerInvariant()} axis: "
            + $"dropped {dropped}/{items} ({(droppedFraction * 100f).ToString("F1", inv)}%)"
            + (campsDropped > 0 ? $", camps dropped {campsDropped}/{candidate.Camps.Count} (not counted)" : "")
            + $", towers moved over {limits.FarMoveMetres.ToString("F0", inv)} m {farMoves}, "
            + $"bad segments {bad}/{segments}" + (fit.Rejected ? $", rejected ({fit.Reason})" : "") + (passed ? " => pass" : " => fail");
        return new FordsLayoutAttempt(factor, axis, candidate, fit, score, passed, summary);
    }
}
