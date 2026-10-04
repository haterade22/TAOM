using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using static TAOM.Features.MissionPerf.TickProfileLines;

namespace TAOM.Features.MissionPerf;

/// <summary>
/// Every line the Patch98 hitch probe and the attribution transpilers write, and nothing else builds them.
///
/// Data lines (<c>[SpawnProfile]</c>, <c>[ScriptProfile]</c>, <c>[AnimLoad]</c>, <c>[HitchDetail]</c>,
/// <c>[TickSummaryExtra]</c>) follow <see cref="TickProfileLines"/>' number rules: invariant culture, ms with
/// two decimals, <c>t</c> as whole seconds since the profiler behaviour's OnCreated, <c>na</c> for a value not
/// measured (NaN, or a negative count), <c>none</c> for an empty list. No tag contains another data tag as
/// a substring. <c>HitchProbeLinesTests</c> pins each one literally.
///
/// Status lines start with <see cref="TickProfileLines.StatusTag"/> and never quote a data tag; an exception
/// message has its square brackets turned into parentheses.
/// </summary>
public static class HitchProbeLines
{
    private const string Tag = TickProfileLines.StatusTag;

    private const string ProbeTargets =
        "Mission.OnPreTick,Mission.WaitTickCompletion,Mission.OnTick,ManagedScriptHolder.TickComponents,Mission.SpawnAgent";

    public const string ProbeOffLine =
        Tag + " probe off: 'Enable Hitch Probe' and 'Enable Tick Profiler' are off at game start (or MCM was not ready); no probe patches installed, no hitch lines this session";

    public const string ScriptDelegateUnboundLine =
        Tag + " ScriptComponentBehavior.OnTick could not be bound; per-component script attribution is off, the script totals stay";

    public const string ProbeNotInstalledLine =
        Tag + " probe on in MCM but its patches are not installed: it was off at game start (restart the game to measure) or its install failed (see the probe install line and [PatchApply])";

    /// <summary>"Enable Tick Profiler" is on but this mission times no behaviour: plan 028's line when nothing
    /// measures; when the hitch probe measures the mission anyway, a line saying so (review 041).</summary>
    public static string ProfilerNotTimingLine(bool restartNeeded, bool probeMeasuring) => probeMeasuring
        ? Tag + (restartNeeded
            ? " on in MCM but it was off at game start, so per-type timing is not installed; the hitch probe still measures this mission; restart the game for per-type timing"
            : " on in MCM but its install at game start failed, so per-type timing is off; the hitch probe still measures this mission; see the [TickProfiler] install line and [PatchApply]")
        : restartNeeded ? TickProfileLines.RestartNeededLine : TickProfileLines.NotInstalledLine;

    /// <summary>An installed profiler whose toggle reads off at the mission's start: plan 028's line, or, when
    /// the hitch probe measures the mission, a line saying so.</summary>
    public static string BuildProfilerOffLine(int missionInProcess, bool probeMeasuring) => probeMeasuring
        ? string.Format(CultureInfo.InvariantCulture,
            "{0} mission {1}: no per-type timing, 'Enable Tick Profiler' is off in MCM; the hitch probe still measures this mission, and the profiler's patches only call through until a restart",
            Tag, missionInProcess)
        : TickProfileLines.BuildMissionOffLine(missionInProcess);

    /// <summary>"Enable Tick Profiler" is on but a hook it needs is missing at this mission's start: plan 028's line
    /// when nothing measures; when the hitch probe measures the mission anyway, a line saying so. The caller passes
    /// false when the missing hooks include Patch98's frame boundary, which closes the probe's frames too.</summary>
    public static string BuildHooksMissingLine(int missionInProcess, IReadOnlyList<string> missing, bool probeMeasuring) => probeMeasuring
        ? string.Format(CultureInfo.InvariantCulture,
            "{0} mission {1}: no per-type timing, required hooks missing: {2}; the hitch probe still measures this mission; another mod's transpiler, a PatchShield strip or a failed patch apply left them out, and the next mission checks again",
            Tag, missionInProcess, Quote(string.Join(", ", missing)))
        : TickProfileLines.BuildHooksMissingLine(missionInProcess, missing);

    /// <summary>Patch98 installed but both toggles off at this mission's start (the profiler not installed).</summary>
    public static string BuildProbeMissionOffLine(int missionInProcess) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} mission {1}: not measuring, 'Enable Hitch Probe' and 'Enable Tick Profiler' are off in MCM; the probe's patches stay installed and only call through until a restart",
            Tag, missionInProcess);

    /// <summary>A fault in an attribution helper's own bookkeeping: one latch turns every helper off.</summary>
    public static string BuildAttributionFault(string part, Exception ex) =>
        Tag + " " + part + " hook failed, all per-type attribution (spawn callbacks, script components, script blocks) is off for this process: "
        + ex.GetType().Name + ": " + Quote(ex.Message);

    public const string WaitUnseenLine =
        Tag + " probe: Mission.WaitTickCompletion's bracket did not run inside Mission.OnPreTick in the first 30 frames (inlined by the JIT, or skipped by another patch); waitTickMs reads 0 and preTickMs includes the wait";

    // A bracket's finalizer ran with no prefix before it, which the state Harmony carries from prefix to finalizer
    // says (HitchProbeHooks.Pair): PatchShield strips a shielded owner's prefixes after a missing-API exception and
    // never its finalizers, and a prefix that throws before ours keeps ours from running (a prefix that only
    // returns false does not: ours takes no parameter that could make Harmony skip it). One WARNING per method per
    // process, saying what that bracket's columns read while the prefix stays gone; the mission end counts the calls.
    private const string PrefixMissingCause =
        " the hitch probe bracket's prefix did not run before its finalizer (it was removed, or an earlier prefix threw): that call is not measured, and while the prefix stays gone ";

    private const string PrefixMissingTail =
        "; this line is written once per process, each measured mission's end counts these calls";

    public const string PreTickPrefixMissingLine =
        Tag + " Mission.OnPreTick:" + PrefixMissingCause + "no frame boundary runs, no frame closes and no hitch is detected" + PrefixMissingTail;

    public const string WaitPrefixMissingLine =
        Tag + " Mission.WaitTickCompletion:" + PrefixMissingCause + "in probe mode waitTickMs reads 0 and preTickMs includes the wait" + PrefixMissingTail;

    public const string OnTickPrefixMissingLine =
        Tag + " Mission.OnTick:" + PrefixMissingCause + "onTickMs reads 0 and, in probe mode, so does missionTickMs" + PrefixMissingTail;

    public const string ScriptPrefixMissingLine =
        Tag + " ManagedScriptHolder.TickComponents:" + PrefixMissingCause + "scriptTickMs and calls read 0" + PrefixMissingTail;

    public const string SpawnPrefixMissingLine =
        Tag + " Mission.SpawnAgent:" + PrefixMissingCause + "spawns and spawnMs read 0" + PrefixMissingTail;

    private static readonly string[] BracketMethods =
    {
        "Mission.OnPreTick", "Mission.WaitTickCompletion", "Mission.OnTick", "ManagedScriptHolder.TickComponents", "Mission.SpawnAgent",
    };

    /// <summary>The mission-end total of finalizer calls that had no prefix before them since the last such line
    /// (or game start): the methods that had any, in <see cref="ProbeBracket"/> order, each with its count. The
    /// caller writes it only when some count is above 0.</summary>
    public static string BuildPrefixMissingCounts(int generation, IReadOnlyList<int> calls)
    {
        var sb = new StringBuilder(256);
        sb.Append(Tag).Append(" mission end for generation ").Append(Int(generation));
        sb.Append(": hitch probe finalizers ran without their prefix since the last such line (or game start), so these calls were not measured: ");
        var first = true;
        for (var i = 0; i < BracketMethods.Length && i < calls.Count; i++)
        {
            if (calls[i] <= 0)
                continue;
            if (!first)
                sb.Append(", ");
            sb.Append(BracketMethods[i]).Append(' ').Append(Int(calls[i]));
            first = false;
        }
        return sb.ToString();
    }

    public static string BuildSpawnProfile(double tSeconds, ExtrasWindow w)
    {
        var sb = new StringBuilder(160);
        sb.Append("[SpawnProfile] t=+").Append(Seconds(tSeconds)).Append("s spawns=").Append(Int(w.Spawns));
        sb.Append(" spawnMs=").Append(Num(w.SpawnMs)).Append(" top=");
        AppendTop(sb, w.SpawnTop, withMax: false);
        return sb.ToString();
    }

    public static string BuildScriptProfile(double tSeconds, ExtrasWindow w)
    {
        var sb = new StringBuilder(200);
        sb.Append("[ScriptProfile] t=+").Append(Seconds(tSeconds)).Append("s calls=").Append(Int(w.ScriptCalls));
        sb.Append(" scriptTickMs=").Append(Num(w.ScriptTickMs));
        sb.Append(" scriptParallelMs=").Append(NumOrNa(w.ScriptParallelMs));
        sb.Append(" occasionalMs=").Append(NumOrNa(w.OccasionalMs)).Append(" top=");
        AppendTop(sb, w.ScriptTop, withMax: true);
        return sb.ToString();
    }

    public static string BuildAnimLoad(double tSeconds, ExtrasWindow w) =>
        "[AnimLoad] t=+" + Seconds(tSeconds) + "s loadingFrames=" + Int(w.LoadingFrames) + " frames=" + Int(w.Frames);

    public static string BuildHitchDetail(double tSeconds, HitchDetailFrame d)
    {
        var sb = new StringBuilder(220);
        sb.Append("[HitchDetail] t=+").Append(Seconds(tSeconds)).Append("s spawnMs=").Append(Num(d.SpawnMs));
        sb.Append(" scriptTickMs=").Append(Num(d.ScriptTickMs));
        sb.Append(" scriptParallelMs=").Append(NumOrNa(d.ScriptParallelMs));
        sb.Append(" animLoading=").Append(d.AnimLoading < 0 ? "na" : Int(d.AnimLoading));
        sb.Append(" mode=").Append(d.Mode).Append(" spawns=").Append(Int(d.Spawns));
        sb.Append(" occasionalMs=").Append(NumOrNa(d.OccasionalMs));
        sb.Append(" onTickMs=").Append(Num(d.OnTickMs)).Append(" preTickAllMs=").Append(Num(d.PreTickAllMs));
        return sb.ToString();
    }

    public static string BuildTickSummaryExtra(MissionExtras m)
    {
        var sb = new StringBuilder(400);
        sb.Append("[TickSummaryExtra] spawnMs=").Append(Num(m.SpawnMs));
        sb.Append(" scriptTickMs=").Append(Num(m.ScriptTickMs));
        sb.Append(" animLoadingFrames=").Append(OrNa(m.AnimLoadingFrames));
        sb.Append(" hitchesWithAnimLoading=").Append(OrNa(m.HitchesWithAnimLoading));
        sb.Append(" mode=").Append(m.Mode).Append(" frames=").Append(Int(m.Frames));
        sb.Append(" spawns=").Append(Int(m.Spawns)).Append(" preFrameSpawns=").Append(Int(m.PreFrameSpawns));
        sb.Append(" preFrameSpawnMs=").Append(Num(m.PreFrameSpawnMs)).Append(" offMainSpawns=").Append(Int(m.OffMainSpawns));
        sb.Append(" scriptParallelMs=").Append(NumOrNa(m.ScriptParallelMs));
        sb.Append(" occasionalMs=").Append(NumOrNa(m.OccasionalMs));
        sb.Append(" onTickMs=").Append(Num(m.OnTickMs)).Append(" preTickAllMs=").Append(Num(m.PreTickAllMs));
        sb.Append(" spawnTop=");
        AppendTop(sb, m.SpawnTop, withMax: false);
        sb.Append(" scriptTop=");
        AppendTop(sb, m.ScriptTop, withMax: true);
        return sb.ToString();
    }

    /// <summary>The game-start line: whether Patch98 applied, which toggle asked for it, and the measured
    /// bookkeeping cost per simulated frame against the 0.5% target.</summary>
    public static string BuildProbeInstallLine(bool applied, string enabledBy, double bookkeepingUs) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} probe install: category {1}, enabled by {2}, targets {3}, bookkeeping {4:0.00} us per frame ({5:0.00}% of a 10 ms frame, target 0.50%)",
            Tag, applied ? "applied" : "failed", enabledBy, ProbeTargets, bookkeepingUs, bookkeepingUs / 10000d * 100d);

    public static string BuildAttributionInstallLine(int spawnSites, int scriptSites, int scriptExpected, bool scriptDelegateBound) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} attribution install: Mission.SpawnAgent sites {1}/2, ManagedScriptHolder.TickComponents sites {2}/{3}, script tick delegate {4}",
            Tag, spawnSites, scriptSites, scriptExpected, scriptDelegateBound ? "bound" : "unbound");

    /// <summary>The per-mission configuration header: the mode and what this mission measures.</summary>
    public static string BuildMissionHeader(int missionInProcess, string mode, double hitchThresholdMs,
        bool spawnAttribution, bool scriptAttribution, bool animSampling) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} mission {1}: mode {2}, hitch threshold {3:0} ms, spawn attribution {4}, script attribution {5}, anim-loading sample {6}",
            Tag, missionInProcess, mode, hitchThresholdMs, OnOff(spawnAttribution), OnOff(scriptAttribution), OnOff(animSampling));

    public static string BuildScriptThreadLine(bool onMain, int threadId, int mainThreadId) =>
        string.Format(CultureInfo.InvariantCulture,
            onMain
                ? "{0} script tick runs on the main thread (managed thread {1}, main {2})"
                : "{0} script tick runs on another thread (managed thread {1}, main {2}); per-component script attribution is off for this process, the totals stay",
            Tag, threadId, mainThreadId);

    public static string BuildSpawnOffMainLine(int threadId) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} Mission.SpawnAgent ran off the main thread (managed thread {1}); off-main spawns are counted in the mission summary but not timed",
            Tag, threadId);

    public static string BuildAnimCostLine(double medianUs, int calls, double budgetUs, bool on) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} anim-loading sample: MBAnimation.IsAnyAnimationLoadingFromDisk median {1:0.00} us over {2} calls (budget {3:0.00} us); {4}",
            Tag, medianUs, calls, budgetUs, on ? "sampling every frame" : "over budget, sampling is off for this process");

    public static string BuildAnimFault(Exception ex) =>
        Tag + " anim-loading sample failed, sampling is off for this process: " + ex.GetType().Name + ": " + Quote(ex.Message);

    /// <summary>The part a pre-tick bracket fault names: the clip-loading sample is taken there, so it stops too.</summary>
    public const string PreTickHookPart = "pre-tick (with the anim-loading sample, which samples there)";

    public static string BuildHookFault(string part, Exception ex) =>
        Tag + " " + part + " hook failed, its timing is off for this process: " + ex.GetType().Name + ": " + Quote(ex.Message);

    private static void AppendTop(StringBuilder sb, IReadOnlyList<BehaviorTotal>? top, bool withMax)
    {
        if (top == null || top.Count == 0)
        {
            sb.Append("none");
            return;
        }
        for (var i = 0; i < top.Count; i++)
        {
            var b = top[i];
            if (i > 0)
                sb.Append(',');
            sb.Append(b.Name).Append(':').Append(Num(b.Ms)).Append('/').Append(Int(b.Calls));
            if (withMax)
                sb.Append('/').Append(Num(b.MaxMs));
        }
    }

    private static string NumOrNa(double value) => double.IsNaN(value) ? "na" : Num(value);

    private static string OnOff(bool value) => value ? "on" : "off";
}
