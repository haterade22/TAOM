using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Diagnostics;

/// <summary>
/// The verdicts of the engines a wave start brings back (#1160), key <c>w&lt;N&gt;</c> for the wave. <c>engine-restore</c> is written at the wave start that restored one or met a problem: each engine must
/// read not destroyed, enabled, at its full hit points, at its start (under <see cref="StartSlack"/> along its path) and in its lane, and a tower must have its ladder queues off and its wait point at
/// the start (faults <c>ladders</c> and <c>wait</c>). <c>engine-use</c> is written at that wave's clear: each restored engine must have moved <see cref="MinTravel"/> along its path, arrived, or fallen
/// again after it moved that far (<c>fell</c>). An engine that fell before it moved (<c>fell-early</c>) shows nothing about the attackers' use, and one whose task was complete at the restore (a ram whose
/// gate is down, <c>done</c>) has no use for them; both count for nothing, and a wave of only those is a skip (<c>reason=unjudged</c>). When the run ends inside the wave an engine still at its start makes
/// a skip, not a fail. Every word of the detail is <c>name=value</c> (the sanitiser rule in <see cref="LongDefeatPostsChecks"/>).
/// </summary>
internal static class LongDefeatEngineChecks
{
    internal const string RestoreId = "taom.long-defeat.engine-restore";
    internal const string UseId = "taom.long-defeat.engine-use";
    internal const float MinTravel = 5f;
    internal const float StartSlack = 1f;

    internal static CheckVerdict Restored(int wave, LdEngineRestore restore)
    {
        var faults = restore.Restored.Select(e => (Name: Name(e), Faults: Faults(e))).ToList();
        var detail = $"w={N(wave)} restored={N(restore.Restored.Count)}" + string.Concat(faults.Select(f => " " + f.Name + "=" + (f.Faults.Count == 0 ? "ok" : string.Join(",", f.Faults))));
        if (restore.Problem != null)
            detail += " problem=" + string.Join("_", restore.Problem.Split((char[]?)null, System.StringSplitOptions.RemoveEmptyEntries));
        return LongDefeatChecks.Verdict(restore.Problem == null && faults.All(f => f.Faults.Count == 0), detail);
    }

    internal static CheckVerdict Used(int wave, IReadOnlyList<LdEngineState> restored, IReadOnlyList<LdEngineState> now, bool runEnded)
    {
        var words = restored.Select(r => Use(r, now.Where(e => e.Kind == r.Kind && e.Index == r.Index).Select(e => (LdEngineState?)e).FirstOrDefault())).ToList();
        var detail = $"w={N(wave)} restored={N(restored.Count)}" + string.Concat(words.Select(w => " " + w.Word));
        var judged = words.Where(w => w.Verdict != UseVerdict.Unjudged).ToList();
        if (judged.Count == 0)
            return new CheckVerdict(CheckResult.Skip, detail + " reason=unjudged");

        var ok = judged.All(w => w.Verdict == UseVerdict.Used);
        if (!ok && runEnded)
            return new CheckVerdict(CheckResult.Skip, detail + " reason=run-ended");
        return LongDefeatChecks.Verdict(ok, detail);
    }

    private enum UseVerdict
    {
        Used,
        Unjudged,
        Idle,
    }

    private static (string Word, UseVerdict Verdict) Use(LdEngineState restored, LdEngineState? now)
    {
        var name = Name(restored);
        if (restored.Done)
            return (name + "=done", UseVerdict.Unjudged);
        if (now is not { } e)
            return (name + "=gone", UseVerdict.Idle);

        var meters = F1(e.Traveled);
        if (e.Destroyed)
            return e.Traveled >= MinTravel ? (name + "=fell," + meters, UseVerdict.Used) : (name + "=fell-early," + meters, UseVerdict.Unjudged);
        if (e.Arrived)
            return (name + "=arrived," + meters, UseVerdict.Used);
        return e.Traveled >= MinTravel ? (name + "=used," + meters, UseVerdict.Used) : (name + "=idle," + meters, UseVerdict.Idle);
    }

    // Written as requirements, so a NaN reading fails each one it touches.
    private static List<string> Faults(LdEngineState e)
    {
        var faults = new List<string>();
        if (e.Destroyed)
            faults.Add("destroyed");
        if (e.Disabled)
            faults.Add("disabled");
        if (!(e.MaxHitPoints > 0f && e.HitPoints >= e.MaxHitPoints))
            faults.Add("hp");
        if (!(e.Traveled < StartSlack))
            faults.Add("moved");
        if (!e.InLane)
            faults.Add("lane");
        if (!e.QueuesOff)
            faults.Add("ladders");
        if (!e.WaitAtStart)
            faults.Add("wait");
        return faults;
    }

    private static string Name(LdEngineState e) => e.Kind + N(e.Index);

    private static string N(int v) => v.ToString(CultureInfo.InvariantCulture);

    private static string F1(float v) => v.ToString("F1", CultureInfo.InvariantCulture);
}
