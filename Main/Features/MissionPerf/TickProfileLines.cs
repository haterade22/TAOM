using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TAOM.Features.MissionPerf;

/// <summary>
/// Every line the Patch97 tick profiler writes, and nothing else builds them.
///
/// Data lines (<c>[TickProfile]</c>, <c>[Hitch]</c>, <c>[PerfContext]</c>, <c>[TickSummary]</c>) are a
/// contract with plan 029's log parser: invariant culture, ms with two decimals, KB as whole bytes / 1024,
/// <c>t</c> as whole seconds since the mission's OnCreated, lowercase booleans, <c>na</c> for an
/// unavailable allocation counter or an unreadable value, <c>top=none</c> when no behaviour ran.
/// <c>TickProfileLinesTests</c> pins each one literally.
///
/// Status lines carry <see cref="StatusTag"/> (<c>[TickProfiler]</c>, which does not contain the
/// substring <c>[TickProfile]</c>) and never a data tag, so the parser cannot count one as a malformed
/// data line. An exception message is quoted with its square brackets turned into parentheses for the
/// same reason.
/// </summary>
public static class TickProfileLines
{
    public const string StatusTag = "[TickProfiler]";

    public const string OffLine =
        StatusTag + " off: 'Enable Tick Profiler' is off at game start (or MCM was not ready); no per-behaviour transpilers installed";

    public const string WaitUnboundLine =
        StatusTag + " WaitTickCompletion could not be bound; waitTickMs reads 0 and the wait lands in otherMs";

    public const string NotInstalledLine =
        StatusTag + " on in MCM but the install at game start failed, so nothing is measured; see the [TickProfiler] install line and [PatchApply]";

    private const string Ms = "0.00";

    public static string BuildTickProfile(double tSeconds, TickWindow window, bool allocAvailable)
    {
        var sb = new StringBuilder(256);
        sb.Append("[TickProfile] t=+").Append(Seconds(tSeconds)).Append("s frames=").Append(Int(window.Frames));
        sb.Append(" wallMs=").Append(Num(window.WallMs));
        AppendPhases(sb, window.PreDisplayMs, window.MissionTickMs, window.PreTickMs, window.WaitTickMs, window.AgentTickMs, window.OtherMs);
        sb.Append(" allocKB=").Append(Kb(window.AllocBytes, allocAvailable));
        AppendFullTop(sb, window.Top, allocAvailable);
        return sb.ToString();
    }

    public static string BuildHitch(double tSeconds, HitchFrame frame, bool allocAvailable)
    {
        var sb = new StringBuilder(256);
        sb.Append("[Hitch] t=+").Append(Seconds(tSeconds)).Append("s frameMs=").Append(Num(frame.FrameMs));
        AppendPhases(sb, frame.PreDisplayMs, frame.MissionTickMs, frame.PreTickMs, frame.WaitTickMs, frame.AgentTickMs, frame.OtherMs);
        sb.Append(" gc0=").Append(Int(frame.Gc0)).Append(" gc1=").Append(Int(frame.Gc1)).Append(" gc2=").Append(Int(frame.Gc2));
        sb.Append(" allocKB=").Append(Kb(frame.AllocBytes, allocAvailable));
        sb.Append(" top=");
        if (frame.Top.Count == 0)
        {
            sb.Append("none");
        }
        else
        {
            for (var i = 0; i < frame.Top.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(frame.Top[i].Name).Append(':').Append(Num(frame.Top[i].Ms));
            }
        }
        return sb.ToString();
    }

    /// <summary>The mission-end line: every closed frame of the mission (the sum of its windows), the
    /// hitch count, the worst hitch and its <c>t</c> (<c>na</c> with no hitch), and the top behaviours
    /// over the whole mission.</summary>
    public static string BuildTickSummary(TickSummary summary, bool allocAvailable)
    {
        var sb = new StringBuilder(256);
        sb.Append("[TickSummary] frames=").Append(Int(summary.Frames));
        sb.Append(" wallMs=").Append(Num(summary.WallMs));
        AppendPhases(sb, summary.PreDisplayMs, summary.MissionTickMs, summary.PreTickMs, summary.WaitTickMs, summary.AgentTickMs, summary.OtherMs);
        sb.Append(" allocKB=").Append(Kb(summary.AllocBytes, allocAvailable));
        sb.Append(" hitches=").Append(Int(summary.Hitches));
        sb.Append(" worstHitchMs=").Append(Num(summary.WorstHitchMs));
        sb.Append(" worstHitchT=").Append(summary.Hitches > 0 ? "+" + Seconds(summary.WorstHitchTSeconds) + "s" : "na");
        AppendFullTop(sb, summary.Top, allocAvailable);
        return sb.ToString();
    }

    public static string BuildPerfContext(PerfContext c) => string.Format(
        CultureInfo.InvariantCulture,
        "[PerfContext] build={0} jitOptimized={1} clr={2} serverGC={3} latency={4} missionInProcess={5} scene={6} agents={7} textureQuality={8} shadowQuality={9} particleDetail={10} ragdolls={11} memLoad={12} availPhysMB={13} tickProfiler={14} diag={15}",
        c.Build, Bool(c.JitOptimized), c.Clr, Bool(c.ServerGc), c.Latency, Int(c.MissionInProcess),
        string.IsNullOrEmpty(c.Scene) ? "unknown" : c.Scene, Int(c.Agents),
        OrNa(c.TextureQuality), OrNa(c.ShadowQuality), OrNa(c.ParticleDetail), OrNa(c.Ragdolls),
        OrNa(c.MemLoadPercent), c.AvailPhysMb < 0 ? "na" : c.AvailPhysMb.ToString(CultureInfo.InvariantCulture),
        c.TickProfiler ? "on" : "off", c.Diag.Count == 0 ? "none" : string.Join(",", c.Diag));

    /// <summary>The diagnostics that will run, in contract order, from their raw toggles: the stall
    /// watchdog and the exit sampler also need the master <paramref name="battleLoad"/> toggle, and the
    /// stall bundle needs the watchdog (their consumers gate on both); the other three stand alone.</summary>
    public static IReadOnlyList<string> DiagTokens(bool battleLoad, bool stallWatchdog, bool stallBundle,
        bool exitSampler, bool freezeSampler, bool memSampler, bool missionPerf)
    {
        var tokens = new List<string>(7);
        if (battleLoad) tokens.Add("battleLoad");
        if (battleLoad && stallWatchdog) tokens.Add("stallWatchdog");
        if (battleLoad && stallWatchdog && stallBundle) tokens.Add("stallBundle");
        if (battleLoad && exitSampler) tokens.Add("exitSampler");
        if (freezeSampler) tokens.Add("freezeSampler");
        if (memSampler) tokens.Add("memSampler");
        if (missionPerf) tokens.Add("missionPerf");
        return tokens;
    }

    public static string BuildInstallLine(bool applied, int onTickSites, int onPreTickSites, int expectedPreTickSites, bool allocAvailable) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} install: category {1}, Mission.OnTick sites {2}/2, Mission.OnPreTick sites {3}/{4}, allocation counter {5}",
            StatusTag, applied ? "applied" : "failed", onTickSites, onPreTickSites, expectedPreTickSites,
            allocAvailable ? "available" : "na");

    public static string BuildSiteCountWarning(string method, string target, int count, int expected = 1) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} {1}: {2} matched {3} times, expected {4}; {5}",
            StatusTag, method, target, count, expected, LeftVanilla(method));

    public static string BuildHelperMismatchWarning(string method, string helper, string target) =>
        StatusTag + " " + method + ": helper " + helper + " does not fit " + target + "; " + LeftVanilla(method);

    public static string BuildFault(string where, Exception ex) =>
        StatusTag + " " + where + " failed, measuring stopped: " + ex.GetType().Name + ": " + Quote(ex.Message);

    /// <summary>The first context read that threw in a mission; the field it names reads na, -1 or unknown.</summary>
    public static string BuildContextReadFault(string field, Exception ex) =>
        StatusTag + " context read of " + field + " failed, that field falls back to na, -1 or unknown: "
        + ex.GetType().Name + ": " + Quote(ex.Message);

    /// <summary>The memory status read returned false (it never throws), so the two memory fields read na.</summary>
    public const string MemoryReadFailedLine =
        StatusTag + " memory status read failed, memLoad and availPhysMB fall back to na";

    /// <summary>One line per MCM knob whose raw value the settings provider replaced with its default
    /// (a hand-edited settings file; the MCM page clamps its own input). Empty when both were used as read.</summary>
    public static IReadOnlyList<string> SettingFallbackLines(int rawTopN, int topN, int rawHitchMs, double hitchMs)
    {
        var lines = new List<string>(2);
        if (rawTopN != topN)
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "{0} MCM 'Tick Profiler Top Behaviours' reads {1}, out of range, so {2} is used", StatusTag, rawTopN, topN));
        if (rawHitchMs != hitchMs)
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "{0} MCM 'Hitch Threshold (ms)' reads {1}, out of range, so {2:0.##} ms is used", StatusTag, rawHitchMs, hitchMs));
        return lines;
    }

    public const string RestartNeededLine =
        StatusTag + " on in MCM but it was off at game start, so its patches are not installed and nothing is measured; restart the game to measure";

    /// <summary>The per-mission configuration header, written when a mission starts measuring: its knobs,
    /// the hitch-line cap, and the call sites swapped right now (a later re-patch reruns the transpilers).</summary>
    public static string BuildMissionStartLine(int missionInProcess, int topN, double hitchThresholdMs,
        int onTickSites, int onPreTickSites, int expectedPreTickSites) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} mission {1}: measuring, top {2} behaviours per line, hitch threshold {3:0.##} ms, first {4} hitch frames written in full, sites Mission.OnTick {5}/2 Mission.OnPreTick {6}/{7}",
            StatusTag, missionInProcess, topN, hitchThresholdMs, MissionTickProfiler.MaxHitchLinesPerMission,
            onTickSites, onPreTickSites, expectedPreTickSites);

    /// <summary>A mission of an installed profiler whose toggle reads off at the mission's start.</summary>
    public static string BuildMissionOffLine(int missionInProcess) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} mission {1}: not measuring, 'Enable Tick Profiler' is off in MCM; its patches stay installed and only call through until a restart",
            StatusTag, missionInProcess);

    /// <summary>A mission of an installed profiler, toggled on, that does not measure because a hook it needs is
    /// not in place now (the install itself succeeded at game start). Read again at the next mission start.</summary>
    public static string BuildHooksMissingLine(int missionInProcess, IReadOnlyList<string> missing) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} mission {1}: not measuring, required hooks missing: {2}; another mod's transpiler, a PatchShield strip or a failed patch apply left them out, and the next mission checks again",
            StatusTag, missionInProcess, Quote(string.Join(", ", missing)));

    /// <summary>The same loss found while the mission was measuring (a rewrite that lowered the site count):
    /// measuring stops there. The frames closed before the stop still get their <c>[TickSummary]</c>.</summary>
    public static string BuildHooksLostLine(int missionInProcess, double tSeconds, string missing) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} mission {1}: measuring stopped at t=+{2}s, required hooks missing: {3}; they were in place at this mission's start, so a later patch on the method took them out, and the next mission checks again",
            StatusTag, missionInProcess, Seconds(tSeconds), Quote(missing));

    /// <summary>A transpiler that threw: its method stays vanilla, with the consequence for the profiler.</summary>
    public static string BuildRewriteFault(string method, Exception ex) =>
        StatusTag + " " + method + " rewrite failed: " + ex.GetType().Name + ": " + Quote(ex.Message)
        + "; " + LeftVanilla(method);

    /// <summary>Written once, on the first hitch frame past the per-mission line cap.</summary>
    public static string BuildHitchCapLine(int cap, double tSeconds) =>
        StatusTag + " hitch line cap reached at t=+" + Seconds(tSeconds) + "s: the first " + Int(cap)
        + " slow frames of this mission were written in full; later ones are counted only in the mission summary's hitches= and worstHitchMs=";

    /// <summary>A measured mission that ended before closing a frame, so it has no summary line.</summary>
    public static string BuildNoFramesLine(int generation) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} mission end for generation {1}: no frame closed while measuring, so no mission summary",
            StatusTag, generation);

    private static string LeftVanilla(string method) => method + " left vanilla, so " + method switch
    {
        "Mission.OnTick" => "no mission times behaviours by type (with 'Enable Hitch Probe' on, missions still measure in probe mode)",
        "Mission.OnPreTick" => "preTickMs reads 0 and that time lands in otherMs (the hitch probe still times the wait)",
        _ => "per-type attribution records nothing for it (the hitch probe's totals stay)",
    };

    /// <summary>An older mission's end arriving after a newer mission began; the newer one keeps measuring.</summary>
    public static string BuildStaleEndLine(int generation, int currentGeneration) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} mission end for generation {1} ignored: generation {2} is current and keeps measuring",
            StatusTag, generation, currentGeneration);

    private static void AppendPhases(StringBuilder sb, double preDisplay, double missionTick, double preTick,
        double wait, double agent, double other)
    {
        sb.Append(" preDisplayMs=").Append(Num(preDisplay));
        sb.Append(" missionTickMs=").Append(Num(missionTick));
        sb.Append(" preTickMs=").Append(Num(preTick));
        sb.Append(" waitTickMs=").Append(Num(wait));
        sb.Append(" agentTickMs=").Append(Num(agent));
        sb.Append(" otherMs=").Append(Num(other));
    }

    private static void AppendFullTop(StringBuilder sb, IReadOnlyList<BehaviorTotal> top, bool allocAvailable)
    {
        sb.Append(" top=");
        if (top.Count == 0)
        {
            sb.Append("none");
            return;
        }
        for (var i = 0; i < top.Count; i++)
        {
            var b = top[i];
            if (i > 0)
                sb.Append(',');
            sb.Append(b.Name).Append(':').Append(Num(b.Ms)).Append('/').Append(Int(b.Calls)).Append('/')
                .Append(Num(b.MaxMs)).Append('/').Append(Kb(b.AllocBytes, allocAvailable));
        }
    }

    internal static string Num(double value) => value.ToString(Ms, CultureInfo.InvariantCulture);

    internal static string Seconds(double value) => value.ToString("0", CultureInfo.InvariantCulture);

    internal static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Kb(long bytes, bool available) =>
        available ? (bytes / 1024).ToString(CultureInfo.InvariantCulture) : "na";

    internal static string OrNa(int value) => value < 0 ? "na" : value.ToString(CultureInfo.InvariantCulture);

    private static string Bool(bool value) => value ? "true" : "false";

    internal static string Quote(string? message) =>
        (message ?? string.Empty).Replace('[', '(').Replace(']', ')').Replace('\r', ' ').Replace('\n', ' ');
}

/// <summary>The values of one <c>[PerfContext]</c> line, in line order. Ints use -1, and
/// <see cref="AvailPhysMb"/> -1, for "unreadable".</summary>
public sealed class PerfContext
{
    public PerfContext(string build, bool jitOptimized, string clr, bool serverGc, string latency, int missionInProcess,
        string scene, int agents, int textureQuality, int shadowQuality, int particleDetail, int ragdolls,
        int memLoadPercent, long availPhysMb, bool tickProfiler, IReadOnlyList<string> diag)
    {
        Build = build;
        JitOptimized = jitOptimized;
        Clr = clr;
        ServerGc = serverGc;
        Latency = latency;
        MissionInProcess = missionInProcess;
        Scene = scene;
        Agents = agents;
        TextureQuality = textureQuality;
        ShadowQuality = shadowQuality;
        ParticleDetail = particleDetail;
        Ragdolls = ragdolls;
        MemLoadPercent = memLoadPercent;
        AvailPhysMb = availPhysMb;
        TickProfiler = tickProfiler;
        Diag = diag;
    }

    public string Build { get; }
    public bool JitOptimized { get; }
    public string Clr { get; }
    public bool ServerGc { get; }
    public string Latency { get; }
    public int MissionInProcess { get; }
    public string Scene { get; }
    public int Agents { get; }
    public int TextureQuality { get; }
    public int ShadowQuality { get; }
    public int ParticleDetail { get; }
    public int Ragdolls { get; }
    public int MemLoadPercent { get; }
    public long AvailPhysMb { get; }
    public bool TickProfiler { get; }
    public IReadOnlyList<string> Diag { get; }
}
