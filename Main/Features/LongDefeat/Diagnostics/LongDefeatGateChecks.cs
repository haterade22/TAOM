using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Diagnostics;

/// <summary>
/// The gate and fort-point verdicts of #1079 (phase3-plan.md section 6): <c>gate-level</c>, <c>gate-repair</c>, <c>gate-attacked</c> and <c>fort-points</c>. Pure: the director reads the field, these turn the
/// numbers into pass, fail or skip. A gate on a float is written as the requirement, so a NaN fails it.
/// </summary>
internal static class LongDefeatGateChecks
{
    /// <summary>The levelled maximum may differ from vanilla times the factor by this share (1%).</summary>
    internal const float MaxTolerance = 0.01f;

    /// <summary>A gate's share of hit points may move by this much across a level write (0.1%): the write keeps the share less one hit point.</summary>
    internal const float ShareTolerance = 0.001f;

    /// <summary>A repaired gate's hit points must reach this share of its maximum: the reset writes the maximum itself, so anything under is a reset that did not run.</summary>
    private const float WholeShare = 0.999f;

    /// <summary>A loss under this many hit points is float noise and no blow.</summary>
    private const float BlowFloor = 0.5f;

    private const int NamedBad = 5;

    /// <summary>
    /// After a level was bought (D18): each visible gate's maximum is the vanilla one times <paramref name="factor"/> within 1%, a standing gate's share of hit points held within 0.1% (a level never heals
    /// the missing share, and a broken gate stays at 0), and the damage-state index the next hit computes did not fall (E9b: a next hit that computes index 0 on a gate stored at 1 or more reads state -1 and throws; a level keeps the index from falling at all).
    /// </summary>
    internal static CheckVerdict GateLevel(int level, float factor, LdGateWrite write)
    {
        var ok = write.Gates.Count > 0 && factor > 0f && !float.IsInfinity(factor);
        var parts = new List<string>();
        foreach (var (g, i) in write.Gates.Select((g, i) => (g, i)))
        {
            var p = Prefix(i, write.Gates.Count);
            var wanted = g.Before.VanillaMax * factor;
            var maxOk = Math.Abs(g.After.MaxHitPoints - wanted) <= MaxTolerance * wanted;
            var shareOk = Math.Abs(Share(g.After) - Share(g.Before)) <= ShareTolerance;
            var indexOk = g.After.StateIndex >= g.Before.StateIndex;
            ok &= maxOk && shareOk && indexOk && !float.IsNaN(g.After.HitPoints);
            parts.Add($"{p}max={F1(g.After.MaxHitPoints)}/{F1(wanted)} {p}hp={F1(g.Before.HitPoints)}->{F1(g.After.HitPoints)} {p}share={F3(Share(g.Before))}->{F3(Share(g.After))} {p}index={g.Before.StateIndex}->{g.After.StateIndex}");
        }

        return LongDefeatChecks.Verdict(ok, $"l={level.ToString(CultureInfo.InvariantCulture)} factor={F2(factor)} gates={write.Gates.Count.ToString(CultureInfo.InvariantCulture)} {string.Join(" ", parts)}".TrimEnd());
    }

    /// <summary>
    /// After a repair (D17), for the gates it acted on: the reset was found by name, and each gate stands (not destroyed), is enabled, its state is Closed, it is not open, its hit points are at the levelled
    /// maximum (vanilla times <paramref name="factor"/> within 1%), its state index is 0, and the hidden gate the boot broke is still broken. <c>path</c> is <c>broken</c> when any repaired gate was destroyed, else
    /// <c>damaged</c>; <c>near</c> counts the defenders within <see cref="GateRules.NearRadius"/> of each gate (R3).
    /// </summary>
    internal static CheckVerdict GateRepair(int wave, float factor, LdGateWrite write)
    {
        var touched = write.Touched.ToList();
        var path = write.Path;
        var ok = write.ResetFound && write.HiddenStanding == 0 && touched.Count > 0 && factor > 0f && !float.IsInfinity(factor);
        var parts = new List<string>();
        foreach (var (g, i) in touched.Select((g, i) => (g, i)))
        {
            var p = Prefix(i, touched.Count);
            var wanted = g.Before.VanillaMax * factor;
            var a = g.After;
            ok &= !a.Destroyed && !g.Disabled && g.Closed && !a.Open && a.StateIndex == 0
                && Math.Abs(a.MaxHitPoints - wanted) <= MaxTolerance * wanted && a.HitPoints >= WholeShare * a.MaxHitPoints;
            parts.Add($"{p}hp={F1(a.HitPoints)}/{F1(a.MaxHitPoints)} {p}index={a.StateIndex} {p}disabled={Flag(g.Disabled)} {p}closed={Flag(g.Closed)} {p}open={Flag(a.Open)} {p}near={g.Near.ToString(CultureInfo.InvariantCulture)}");
        }

        var body = string.Join(" ", parts);
        var detail = $"w={wave.ToString(CultureInfo.InvariantCulture)} path={path} gates={touched.Count.ToString(CultureInfo.InvariantCulture)} {body}{(body.Length > 0 ? " " : "")}"
            + $"hidden_down={Flag(write.HiddenStanding == 0)} reset={(write.ResetFound ? "found" : "missing")}";
        return LongDefeatChecks.Verdict(ok, detail);
    }

    /// <summary>
    /// At the next clear or the run's end, for the watch begun by a repair: the gates lost hit points since (<paramref name="hitPointsNow"/> against the watch's baseline) or fell (pass), and no attacker was sampled
    /// behind a standing gate's plane (<c>through=0</c>); an untouched gate is a skip (<c>reason=no-blow</c>), because a wave whose ram died brings no blow at all (E30, R4). An attacker behind a gate that stood fails
    /// whether or not it was struck.
    /// </summary>
    internal static CheckVerdict GateAttacked(GateWatch watch, float hitPointsNow, bool fell, bool endedRun)
    {
        var lost = watch.Baseline - hitPointsNow;
        var detail = $"w={watch.Wave.ToString(CultureInfo.InvariantCulture)} path={watch.Path} start={F1(watch.Baseline)} now={F1(hitPointsNow)} lost={F1(lost)} fell={Flag(fell)} "
            + $"through={watch.Through.ToString(CultureInfo.InvariantCulture)} samples={watch.Samples.ToString(CultureInfo.InvariantCulture)}{(endedRun ? " ended=run" : "")}";
        if (float.IsNaN(lost) || float.IsNaN(hitPointsNow) || watch.Through > 0)
            return new CheckVerdict(CheckResult.Fail, detail);
        if (fell || lost >= BlowFloor)
            return new CheckVerdict(CheckResult.Pass, detail);

        return new CheckVerdict(CheckResult.Skip, detail + " reason=no-blow");
    }

    /// <summary>
    /// At the boot: every recorded tower post, tower volley spot and volley origin has a navmesh and lies inside the mission boundary (the sally points' test). <paramref name="points"/> is each point's name and answer;
    /// none recorded is a skip. A volley origin whose ray out over the parapet hits something within 2 m is named with <c>clear=no</c> (phase3-plan.md section 4, R5).
    /// </summary>
    internal static CheckVerdict FortPoints(IReadOnlyList<(string Name, LdPointFacts Facts)> points)
    {
        if (points.Count == 0)
            return new CheckVerdict(CheckResult.Skip, "points=0 reason=none-recorded");

        var bad = points.Where(p => !p.Facts.Usable).ToList();
        var named = string.Join(",", bad.Take(NamedBad).Select(p => $"{p.Name}(navmesh={Answer(p.Facts.Navmesh)} boundary={Answer(p.Facts.Boundary)}{(p.Facts.Clear ? "" : " clear=no")})"));
        return LongDefeatChecks.Verdict(bad.Count == 0,
            $"points={points.Count.ToString(CultureInfo.InvariantCulture)} failed={bad.Count.ToString(CultureInfo.InvariantCulture)}{(bad.Count > 0 ? " bad=" + named : "")}");
    }

    /// <summary>The key prefix of gate <paramref name="index"/> in a line: none for the one gate Helm's Deep has, <c>g0.</c>, <c>g1.</c> and so on when there are several.</summary>
    private static string Prefix(int index, int count) => count > 1 ? "g" + index.ToString(CultureInfo.InvariantCulture) + "." : "";

    /// <summary>The share of the maximum a gate holds: 0 for a destroyed gate or a maximum that is not a number.</summary>
    private static float Share(LdGateFacts g) => g.Destroyed || !(g.MaxHitPoints > 0f) ? 0f : g.HitPoints / g.MaxHitPoints;

    private static string Flag(bool v) => v ? "true" : "false";

    private static string Answer(bool v) => v ? "yes" : "no";

    private static string F1(float v) => v.ToString("F1", CultureInfo.InvariantCulture);

    private static string F2(float v) => v.ToString("F2", CultureInfo.InvariantCulture);

    private static string F3(float v) => v.ToString("F3", CultureInfo.InvariantCulture);
}
