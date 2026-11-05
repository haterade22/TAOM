using System;

namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>
/// The in-game check ids of Marshal's Word, read by tools/ingame_checks/0.toml (the draft manifest shared with the Seat of
/// Seeing), and the two rules that turn a trend into a verdict (build-spec 5.6 and 6.5). The session writes the lines; a later stage adds its ids.
/// </summary>
internal static class MarshalChecks
{
    internal const string CapturedId = "taom.marshals-word.captured";
    internal const string DeclinedId = "taom.marshals-word.declined";
    internal const string StartedId = "taom.marshals-word.started";
    internal const string FinishedId = "taom.marshals-word.finished";
    internal const string DroppedId = "taom.marshals-word.dropped";
    internal const string ClearedId = "taom.marshals-word.cleared";
    internal const string PatienceId = "taom.marshals-word.patience";
    internal const string ArrayId = "taom.marshals-word.array";
    internal const string MarchId = "taom.marshals-word.march";
    internal const string SpreadId = "taom.marshals-word.march-spread";
    internal const string FaultId = "taom.marshals-word.fault";
    internal const string FireId = "taom.marshals-word.fire";
    internal const string WordId = "taom.marshals-word.word";
    internal const string VolleyHoldId = "taom.marshals-word.volley-hold";
    internal const string VolleyReleaseId = "taom.marshals-word.volley-release";
    internal const string MarkId = "taom.marshals-word.mark";
    internal const string MarkEffectId = "taom.marshals-word.mark-effect";

    /// <summary>Hot-path counters of the archers' callback: one increment per Hold and one per Release, no string and no I/O.</summary>
    internal const string HoldCounterId = "taom.marshals-word.fire-hold";
    internal const string ReleaseCounterId = "taom.marshals-word.fire-release";

    /// <summary>Hot-path counter of preview solves: one increment per preview, at most 30 a second with the mouse held, no string and no I/O.</summary>
    internal const string PreviewCounterId = "taom.marshals-word.array-preview";

    /// <summary>
    /// The seconds apart that companies of one march may arrive and still pass (build-spec 6.5): <c>SpreadBaseSeconds + SpreadShareOfLongest * longest</c>
    /// plus <c>max(0, longest - shortest / MinPace)</c>, which no pace can remove, where <c>longest</c> and <c>shortest</c> are the first pass's times to the slack.
    /// </summary>
    internal static double SpreadAllowance(double longest, double shortest) =>
        MarshalTuning.SpreadBaseSeconds + MarshalTuning.SpreadShareOfLongest * longest + Math.Max(0.0, longest - shortest / MarshalTuning.MinPace);

    /// <summary>
    /// True when the longest takes <c>SpreadMinLongest</c> seconds and the walks differ by more than <c>SpreadProofMargin</c> times the allowance,
    /// so that a group with no pace would fail unless its companies beat their first read by more than the margin covers; only then does a pass
    /// prove that the pace worked. A walk that is not a number proves nothing.
    /// </summary>
    internal static bool SpreadProvesPace(double longest, double shortest) =>
        longest >= MarshalTuning.SpreadMinLongest && longest - shortest > MarshalTuning.SpreadProofMargin * SpreadAllowance(longest, shortest);

    /// <summary>A march passes when its companies arrived within <see cref="SpreadAllowance"/> of each other. A spread that is not a number fails.</summary>
    internal static bool SpreadPasses(double spread, double longest, double shortest) =>
        !double.IsNaN(spread) && !double.IsInfinity(spread) && spread <= SpreadAllowance(longest, shortest);

    /// <summary>
    /// Patience is judged once <c>PatienceJudgeAfter</c> steps have finished, and fails when it ended more than one step in four. A broken
    /// arrival test would otherwise read as many finished steps.
    /// </summary>
    internal static bool PatienceTooOften(int finished, int endedByPatience) =>
        finished >= MarshalTuning.PatienceJudgeAfter && endedByPatience * MarshalTuning.PatienceFailShareDenominator > finished;
}
