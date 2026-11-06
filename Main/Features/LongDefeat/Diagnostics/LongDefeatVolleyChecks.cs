using System;
using System.Globalization;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Diagnostics;

/// <summary>
/// The <c>volley</c> verdict of #1079 (phase3-plan.md section 6), one line per use. Pass needs: the missiles added (the mission's missile list growth) equal the shots the aim planned and are not none; one distinct attacker per arrow while attackers
/// lasted (<c>min(shots, candidates)</c>); no agent spawned by the fire; foe hits within the window at or over <see cref="HitShare"/> of the arrows (A16: a first guess for Mike to tune), with shield blocks
/// counted apart and left out of the share; and no friendly hit the engine did not cancel (A19). Null and wrong-side affectors (A24), the owner's multiplier (D25) and the first hit's base speed and impact speed (A25; an impact speed under the base speed is the arrow's air drag) are reported
/// and never judged. A window that a clear or the run's end cut short can pass when the share is already met, but never fails on it: it is a skip. A float is written as the requirement, so a NaN fails it.
/// </summary>
internal static class LongDefeatVolleyChecks
{
    /// <summary>The share of the arrows that must land on a foe (A16).</summary>
    internal const float HitShare = 0.5f;

    /// <summary>How long a use's arrows have to land: a flight is about two seconds (the owner's slot may be reused after), and the share is read after six.</summary>
    internal const float WindowSeconds = 6f;

    /// <summary>
    /// The verdict for <paramref name="use"/> and what its <paramref name="watch"/> counted in <paramref name="seconds"/>. <paramref name="ended"/> is null for a window that ran its length, <c>wave</c> or
    /// <c>run</c> for one the clear or the run's end cut.
    /// </summary>
    internal static CheckVerdict Volley(VolleyUse use, VolleyWatch watch, float seconds, string? ended)
    {
        var detail = Describe(use, watch, seconds, ended);
        var expectedDistinct = Math.Min(use.Added, use.Candidates);
        var fired = use.Added > 0 && use.Added == use.Shots;
        var named = use.Distinct == expectedDistinct;
        var safe = use.Spawned == 0 && watch.Friendly == 0;
        var landed = watch.Hits >= HitShare * use.Added;
        if (fired && named && safe && !landed && ended != null && seconds < WindowSeconds)
            return new CheckVerdict(CheckResult.Skip, detail + " reason=window-cut");

        return LongDefeatChecks.Verdict(fired && named && safe && landed, detail);
    }

    private static string Describe(VolleyUse u, VolleyWatch w, float seconds, string? ended)
    {
        var aim = u.Mode == VolleyAimMode.Crosshair
            ? " mode=crosshair" + $" aim={(u.ByBot ? "bot" : "person")} ray_gap={F1(u.RayGap ?? float.NaN)}"
            : " mode=auto";
        return $"w={N(u.Wave)} use={N(u.Use)} source={u.Source.Id}{aim} arrows={N(u.Arrows)} shots={N(u.Shots)} candidates={N(u.Candidates)} distinct={N(u.Distinct)} spawned={N(u.Spawned)} "
            + $"hits={N(w.Hits)} blocked={N(w.Blocked)} friendly={N(w.Friendly)} cancelled={N(w.Cancelled)} kills={N(w.Kills)} affector_null={N(w.AffectorNull)} affector_side={N(w.AffectorSide)} "
            + $"owner={(u.OwnerTroop.Length > 0 ? u.OwnerTroop : "none")} mult={F2(u.Multiplier)} base_speed={F1(w.BaseSpeed)} impact_speed={F1(w.ImpactSpeed)} window={F1(seconds)}"
            + (ended != null ? " ended=" + ended : "");
    }

    private static string N(int v) => v.ToString(CultureInfo.InvariantCulture);

    private static string F1(float v) => v.ToString("F1", CultureInfo.InvariantCulture);

    private static string F2(float v) => v.ToString("F2", CultureInfo.InvariantCulture);
}
