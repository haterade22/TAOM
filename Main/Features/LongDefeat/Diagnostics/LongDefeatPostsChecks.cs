using System.Globalization;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Diagnostics;

/// <summary>
/// The <c>posts</c> verdict of #1079 (phase3-plan.md section 6, Slice 7), one line per tower and wave, written at the wave's clear or the run's end. Pass needs: at least as many archers standing at the wave's start as the
/// fill owed (<see cref="PostsWatch.Expected"/>; posts the cap left empty are named in <c>short=</c> and not failed, a spawn that failed is in <c>failed=</c> and leaves the tower short of the expectation); at the held sample, 30 s into the wave, every archer still standing within
/// <see cref="PostsRules.HoldMetres"/> and <see cref="PostsRules.HoldHeight"/> of his own post; and by the clear at least one arrow loosed by the tower's archers (<c>OnAgentShootMissile</c>). A fill that left nothing
/// to prove, a sample with no one standing, a wave that ended before the sample, and a run that ended before any arrow are skips, not passes: each proves nothing. A float is written as the requirement, so a NaN fails it. Every word of the detail is <c>name=value</c>: the log's
/// sanitiser (<c>OutcomeLog.SanitiseDetail</c>) glues a bare word to the value before it, and the pass pattern in <c>1079.toml</c> reads the logged text.
/// </summary>
internal static class LongDefeatPostsChecks
{
    internal static CheckVerdict Posts(PostsWatch w, bool endedRun)
    {
        var detail = Describe(w, endedRun);
        if (w.Expected <= 0)
            return Skip(detail, "no-archers");

        var held = w.SampleHeld == w.SampleStanding && PostsRules.Held(w.MaxDrift, w.MaxDz);
        if (w.Manned < w.Expected || (w.Sampled && !held))
            return new CheckVerdict(CheckResult.Fail, detail);
        if (!w.Sampled)
            return Skip(detail, "short-wave");
        if (w.SampleStanding <= 0)
            return Skip(detail, "none-standing");
        if (w.Shots < 1)
            return endedRun ? Skip(detail, "window-cut") : new CheckVerdict(CheckResult.Fail, detail);

        return new CheckVerdict(CheckResult.Pass, detail);
    }

    private static CheckVerdict Skip(string detail, string reason) => new(CheckResult.Skip, detail + " reason=" + reason);

    private static string Describe(PostsWatch w, bool endedRun) =>
        $"tower={w.TowerId} w={N(w.Wave)} level={N(w.Level)} variant={w.Variant} formation={N(w.Formation)} posts={N(w.Posts)} expected={N(w.Expected)} manned={N(w.Manned)} "
        + $"held={N(w.SampleHeld)} sampled={N(w.SampleStanding)} max_drift={F1(w.MaxDrift)} max_dz={F1(w.MaxDz)} shots={N(w.Shots)} split={N(w.MovedBack)} out={N(w.MovedOut)} short={N(w.Short)} failed={N(w.Failed)}"
        + (endedRun ? " ended=run" : string.Empty);

    private static string N(int v) => v.ToString(CultureInfo.InvariantCulture);

    private static string F1(float v) => v.ToString("F1", CultureInfo.InvariantCulture);
}
