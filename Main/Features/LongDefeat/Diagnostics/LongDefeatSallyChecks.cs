using System.Globalization;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Diagnostics;

/// <summary>
/// The <c>sally</c>, <c>sally-return</c> and <c>sally-contact</c> verdicts of #1079 (phase2-plan.md section 6, spike S9). Pure: the director reads the lord and the points, these turn the numbers into
/// pass, fail or skip. A gate on a float is written as the requirement, so a NaN fails it. Contact is a pass or a skip, never a fail: the judge reports it and does not require it.
/// </summary>
internal static class LongDefeatSallyChecks
{
    /// <summary>The lord must stand this close to the sally point (on the ground) right after the move out.</summary>
    internal const float ArrivalMetres = 2f;

    /// <summary>
    /// The move out at a wave's start: he stands within 2 m of the sally point with a horse under him, no player death was counted by the move, and for a remount the step was done and the old body was not
    /// reported killed or knocked out (risk 2: which callback a faded main agent raises, and with what state).
    /// </summary>
    internal static CheckVerdict Out(int wave, string how, LdLordFacts after, LdPlace point, LdRemount? remount, int deathsBefore, int deathsAfter)
    {
        var dist = after.Present ? point.FlatDistanceTo(after.At) : float.NaN;
        var stepOk = remount == null || (remount.Done && !remount.OldLost);
        var ok = after.Present && after.HasMount && dist <= ArrivalMetres && deathsAfter == deathsBefore && stepOk;
        var old = remount == null ? "" : $" old_state={(remount.OldState.Length == 0 ? "-" : remount.OldState)} old_lost={Yes(remount.OldLost)}";
        return LongDefeatChecks.Verdict(ok, $"w={Num(wave)} how={how} mount={Yes(after.HasMount)} dist={F1(dist)} deaths={Num(deathsBefore)}->{Num(deathsAfter)}{old}");
    }

    /// <summary>The move home at a clear: he stands within the keep radius of the home point, and no player death was counted since he went out.</summary>
    internal static CheckVerdict Return(int wave, LdLordFacts after, LdPlace home, float keepRadius, int deathsAtOut, int deathsNow)
    {
        var dist = after.Present ? home.FlatDistanceTo(after.At) : float.NaN;
        var ok = after.Present && dist <= keepRadius && deathsNow == deathsAtOut;
        return LongDefeatChecks.Verdict(ok, $"w={Num(wave)} dist={F1(dist)} keep_radius={F1(keepRadius)} deaths={Num(deathsAtOut)}->{Num(deathsNow)}");
    }

    /// <summary>The first hit by or on the lord or his horse inside the window and the radius.</summary>
    internal static CheckVerdict Contact(int wave, float seconds, string counts) =>
        new(CheckResult.Pass, $"w={Num(wave)} t={F1(seconds)} {counts}");

    /// <summary>No contact came: a skip that names why, so the line reads in the log without failing the run.</summary>
    internal static CheckVerdict NoContact(int wave, string reason, float seconds, string counts) =>
        new(CheckResult.Skip, $"w={Num(wave)} reason={reason} t={F1(seconds)} {counts}");

    /// <summary>A wave that could not sally, or a return that did not apply: the reason is on the line.</summary>
    internal static CheckVerdict Skipped(int wave, string reason) => new(CheckResult.Skip, $"w={Num(wave)} reason={reason}");

    /// <summary>The old body of a remount was reported lost after the step that had passed: the planned removal was not a clean one.</summary>
    internal static CheckVerdict OldBodyLost(int wave, LdBodyEvent e) =>
        new(CheckResult.Fail, $"w={Num(wave)} reason=old-body-reported-lost callback={e.Callback} old_state={e.State}");

    private static string Yes(bool v) => v ? "yes" : "no";

    private static string Num(int v) => v.ToString(CultureInfo.InvariantCulture);

    private static string F1(float v) => v.ToString("F1", CultureInfo.InvariantCulture);
}
