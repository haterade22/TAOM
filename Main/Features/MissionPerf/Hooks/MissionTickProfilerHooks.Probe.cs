using System;
using TAOM.Core.Logging;

namespace TAOM.Features.MissionPerf.Hooks;

/// <summary>Plan 041's additions to the profiler hooks (the class sits at the ADR-002 line limit): the
/// <c>[HitchDetail]</c> line beside <c>[Hitch]</c>, the per-mission probe flags, the first-tick sampler cost
/// and mission header, and <c>[TickSummaryExtra]</c>.</summary>
public static partial class MissionTickProfilerHooks
{
    /// <summary><c>[Hitch]</c> (or, past the per-mission cap, the one cap line) and right after it, with the same
    /// <c>t</c>, <c>[HitchDetail]</c>. Only Patch98's prefix closes frames, so the brackets the detail describes
    /// are installed whenever a hitch arrives here.</summary>
    private static void LogHitch(double t, HitchFrame? hitch)
    {
        if (hitch == null)
        {
            Logger?.LogInfo(TickProfileLines.BuildHitchCapLine(MissionTickProfiler.MaxHitchLinesPerMission, t));
            return;
        }
        Logger?.LogInfo(TickProfileLines.BuildHitch(t, hitch, AllocationCounter.Available));
        if (hitch.Detail != null)
            Logger?.LogInfo(HitchProbeLines.BuildHitchDetail(t, hitch.Detail));
    }

    /// <summary>After <see cref="BeginMission"/>: what this mission attributes and samples. Spawn and script
    /// attribution need behaviour timing, every attribution site swapped and no helper fault this process;
    /// per-component script attribution also needs the OnTick delegate bound (its swap exists only then) and
    /// the script tick on the main thread.</summary>
    internal static void ConfigureProbeMission(bool measuring, bool behaviorTiming)
    {
        var profiler = Profiler;
        if (profiler == null)
            return;
        var attributing = behaviorTiming && !MissionAttributionHooks.Off;
        profiler.SpawnAttribution = attributing && MissionAttributionInstaller.SpawnSites == 2;
        profiler.ScriptBlockTiming = attributing && MissionAttributionInstaller.ScriptSites == MissionAttributionInstaller.ScriptExpected;
        profiler.ScriptAttribution = profiler.ScriptBlockTiming && MissionAttributionHooks.ScriptTickCall != null
            && !HitchProbeHooks.ScriptOffMain;
        profiler.AnimSampling = measuring && (HitchProbeHooks.Sampler?.Enabled ?? false);
    }

    /// <summary>A mission's first tick, after <c>[PerfContext]</c>: on the first measured mission, the clip-loading
    /// sampler's one cost measurement; then the mission header, and a warning when the probe is on in MCM but
    /// not installed. With both toggles off it writes one reason line when only Patch98 is installed (the
    /// profiler's own off line covers an installed profiler; the game-start <c>probe off:</c> line covers neither).</summary>
    internal static void OnMissionFirstTick(IModLogger logger, int missionNumber, double hitchMs, bool measuring,
        bool behaviorTiming, bool profilerToggleOn, bool probeToggleOn)
    {
        var profiler = Profiler;
        var sampler = HitchProbeHooks.Sampler;
        if (measuring && profiler != null && sampler != null)
        {
            var cost = sampler.MeasureCost();
            if (cost != null)
                logger.LogInfo(cost);
            var fault = sampler.TakePendingFault();
            if (fault != null)
                logger.LogWarning(fault);
            profiler.AnimSampling = sampler.Enabled;
        }
        if (!profilerToggleOn && !probeToggleOn)
        {
            if (HitchProbeInstaller.ProbeInstalled && !Installed)
                logger.LogInfo(HitchProbeLines.BuildProbeMissionOffLine(missionNumber));
            return;
        }
        logger.LogInfo(HitchProbeLines.BuildMissionHeader(missionNumber,
            behaviorTiming ? "full" : measuring ? "probe" : "off", hitchMs, measuring && (profiler?.SpawnAttribution ?? false),
            measuring && (profiler?.ScriptAttribution ?? false), measuring && (profiler?.AnimSampling ?? false)));
        if (probeToggleOn && !HitchProbeInstaller.ProbeInstalled)
            logger.LogWarning(HitchProbeLines.ProbeNotInstalledLine);
    }

    /// <summary>Right after <c>[TickSummary]</c>, the current mission's <c>[TickSummaryExtra]</c>; nothing when no
    /// frame closed, where <see cref="WriteSummary"/> has just said the mission has no summary. Then, when a probe
    /// finalizer ran without its prefix since the last such line, one WARNING with the count per method: it is
    /// written with or without a closed frame, since a missing <c>Mission.OnPreTick</c> prefix is what stops any
    /// frame from closing, and an older mission's end takes none of them.</summary>
    internal static void WriteSummaryExtra(int generation, int topN)
    {
        var profiler = Profiler;
        var logger = Logger;
        if (profiler == null || logger == null || generation != profiler.Generation)
            return;
        try
        {
            var extras = profiler.SummarizeExtras(topN);
            if (extras.Frames > 0)
                logger.LogInfo(HitchProbeLines.BuildTickSummaryExtra(extras));
            var missing = HitchProbeHooks.TakePrefixMissingCalls();
            if (missing != null)
                logger.LogWarning(HitchProbeLines.BuildPrefixMissingCounts(generation, missing));
        }
        catch (Exception ex) { Fault(profiler, "mission summary", ex); }
    }

    /// <summary>Test-only: every static back to its declared initial value. Never called from production code.</summary>
    internal static void ResetForTests()
    {
        Profiler = null;
        Logger = null;
        WaitTickCompletionCall = null;
        Installed = false;
        OnTickSites = 0;
        OnPreTickSites = 0;
        _agentTickStart = 0;
    }
}
