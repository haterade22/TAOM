using System;
using System.Diagnostics;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Features.MissionPerf.Hooks;

/// <summary>
/// Installs Patch98 (the hitch probe) once per process. Its one caller is
/// <see cref="MissionTickProfilerInstaller.InstallIfEnabled"/>, after the Patch97 work or its skip, so the
/// once-per-process guard is SubModule's game-init block. Patch98 holds the frame boundary for both modes, so it
/// installs when either toggle is on. The clip-loading sampler is built here over the stateless engine adapter
/// (the hooks are static, outside the container, like the profiler's logger); its cost is measured later, at the
/// first measured mission. Logs one line either way; contains its own failures.
/// </summary>
internal static class HitchProbeInstaller
{
    internal const string Category = "Patch98_HitchProbe";
    private const int CostMeterFrames = 2000;

    /// <summary>Patch98 applied at game start.</summary>
    internal static bool ProbeInstalled;

    /// <summary>The hitch probe was on and the tick profiler off at game start, so Install ran for the probe
    /// alone and the profiler's install never ran (whether or not Patch98 then applied): a mission that finds
    /// the profiler on in MCM needs a restart, not a failed install. With both toggles off, Install returns
    /// before setting this and it stays false; <see cref="ProfilerNeedsRestart"/>'s Profiler == null covers
    /// that case.</summary>
    internal static bool ProfilerOffAtGameStart;

    internal static void Install(IBattleLoadDiagnosticsSettingsProvider settings, IModLogger logger,
        Func<string, bool> tryPatchCategory, bool profilerRequested, bool waitSwapLive)
    {
        try
        {
            var probeOn = settings.HitchProbeEnabled;
            if (!probeOn && !profilerRequested)
            {
                logger.LogInfo(HitchProbeLines.ProbeOffLine);
                return;
            }

            ProfilerOffAtGameStart = !profilerRequested;
            MissionTickProfilerHooks.Logger ??= logger;
            MissionTickProfilerHooks.Profiler ??= new MissionTickProfiler(Stopwatch.Frequency);
            HitchProbeHooks.Sampler = new AnimLoadingSampler(new AnimationLoadingAdapter(), Stopwatch.GetTimestamp, Stopwatch.Frequency);
            var bookkeepingUs = ProbeCostMeter.MeasureBookkeepingMicroseconds(CostMeterFrames);
            ProbeInstalled = tryPatchCategory(Category);
            HitchProbeHooks.WaitSwapActive = waitSwapLive;
            MissionTickProfilerHooks.Installed = MissionTickProfilerHooks.Installed && ProbeInstalled;
            logger.LogInfo(HitchProbeLines.BuildProbeInstallLine(ProbeInstalled,
                probeOn && profilerRequested ? "both" : probeOn ? "hitch probe" : "tick profiler", bookkeepingUs));
        }
        catch (Exception ex)
        {
            ProbeInstalled = false;
            MissionTickProfilerHooks.Installed = false;
            try { logger.LogError(HitchProbeLines.BuildHookFault("probe install", ex)); }
            catch { /* diagnostic only */ }
        }
    }

    /// <summary>Whether a mission that finds "Enable Tick Profiler" on but not installed needs a restart
    /// (it was off at game start, so its install never ran) rather than reporting a failed install.</summary>
    internal static bool ProfilerNeedsRestart =>
        MissionTickProfilerHooks.Profiler == null || ProfilerOffAtGameStart;

    /// <summary>Test-only: the statics back to their initial values. Never called from production code.</summary>
    internal static void ResetForTests()
    {
        ProbeInstalled = false;
        ProfilerOffAtGameStart = false;
    }
}
