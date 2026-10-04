using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MissionPerf;
using TAOM.Features.MissionPerf.Hooks;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The per-mission probe decisions that live in <c>MissionTickProfilerHooks.Probe.cs</c> and the restart
/// choice in <c>HitchProbeInstaller</c>, driven without a mission: which attribution a mission gets, the
/// first-tick header and reason lines, <c>[HitchDetail]</c> beside <c>[Hitch]</c>, and the mission-end
/// <c>[TickSummaryExtra]</c> (review 041).
/// </summary>
[TestClass]
public class HitchProbeMissionFlowTests
{
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        Reset();
        _logger = Substitute.For<IModLogger>();
        MissionTickProfilerHooks.Profiler = new MissionTickProfiler(Stopwatch.Frequency);
        MissionTickProfilerHooks.Logger = _logger;
    }

    [TestCleanup]
    public void Cleanup() => Reset();

    private static void Reset()
    {
        HitchProbeInstaller.ResetForTests();
        HitchProbeHooks.ResetForTests();
        MissionTickProfilerHooks.ResetForTests();
        MissionAttributionInstaller.ResetForTests();
    }

    private static MissionTickProfiler Profiler => MissionTickProfilerHooks.Profiler!;

    private string[] Lines(string level) => _logger.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == level)
        .Select(c => (string)c.GetArguments()[0]!)
        .ToArray();

    private static int Begin(bool measuring, bool behaviorTiming, double hitchMs = 1000) =>
        MissionTickProfilerHooks.BeginMission(Stopwatch.GetTimestamp(), measuring, hitchMs, behaviorTiming);

    private sealed class NotLoading : IAnimationLoadingAdapter
    {
        public bool IsAnyAnimationLoadingFromDisk() => false;
    }

    [TestMethod]
    public void ProfilerNeedsRestart_ProfilerOffAtGameStartAndPatch98Failed_IsTrue()
    {
        HitchProbeInstaller.ProfilerOffAtGameStart = true;
        HitchProbeInstaller.ProbeInstalled = false;

        Assert.IsTrue(HitchProbeInstaller.ProfilerNeedsRestart,
            "The profiler's install was never attempted this session, so a restart is the remedy, not a failed install.");
    }

    [TestMethod]
    public void ProfilerNeedsRestart_ProfilerOnAtGameStartButItsInstallFailed_IsFalse()
    {
        HitchProbeInstaller.ProfilerOffAtGameStart = false;
        HitchProbeInstaller.ProbeInstalled = true;

        Assert.IsFalse(HitchProbeInstaller.ProfilerNeedsRestart);
    }

    [TestMethod]
    public void ConfigureProbeMission_FullModeEverySiteSwapped_AttributesEverything()
    {
        MissionAttributionHooks.ScriptTickCall = (_, _) => { };
        MissionAttributionInstaller.SpawnSites = 2;
        MissionAttributionInstaller.ScriptSites = 5;
        Begin(measuring: true, behaviorTiming: true);

        MissionTickProfilerHooks.ConfigureProbeMission(measuring: true, behaviorTiming: true);

        Assert.IsTrue(Profiler.SpawnAttribution);
        Assert.IsTrue(Profiler.ScriptBlockTiming);
        Assert.IsTrue(Profiler.ScriptAttribution);
    }

    [TestMethod]
    public void ConfigureProbeMission_ScriptDelegateUnbound_TimesTheBlocksButAttributesNoComponent()
    {
        MissionAttributionHooks.ScriptTickCall = null;
        MissionAttributionInstaller.ScriptSites = 4;
        Begin(measuring: true, behaviorTiming: true);

        MissionTickProfilerHooks.ConfigureProbeMission(measuring: true, behaviorTiming: true);

        Assert.IsTrue(Profiler.ScriptBlockTiming, "The four TWParallel.For swaps are in place.");
        Assert.IsFalse(Profiler.ScriptAttribution, "No OnTick swap exists, so the header must not say script attribution on.");
    }

    [TestMethod]
    public void ConfigureProbeMission_AfterAnAttributionFault_AttributesAndTimesNothingByType()
    {
        MissionAttributionHooks.ScriptTickCall = (_, _) => { };
        MissionAttributionInstaller.SpawnSites = 2;
        MissionAttributionInstaller.ScriptSites = 5;
        MissionAttributionHooks.Off = true;
        Begin(measuring: true, behaviorTiming: true);

        MissionTickProfilerHooks.ConfigureProbeMission(measuring: true, behaviorTiming: true);

        Assert.IsFalse(Profiler.SpawnAttribution);
        Assert.IsFalse(Profiler.ScriptBlockTiming, "The helpers time nothing once off, so the block columns must read na, not 0.00.");
        Assert.IsFalse(Profiler.ScriptAttribution);
    }

    [TestMethod]
    public void OnMissionFirstTick_ProbeMeasuring_WritesTheProbeModeHeaderAndTheSamplerCostOnce()
    {
        HitchProbeInstaller.ProbeInstalled = true;
        HitchProbeHooks.Sampler = new AnimLoadingSampler(new NotLoading(), Stopwatch.GetTimestamp, Stopwatch.Frequency);
        Begin(measuring: true, behaviorTiming: false);
        MissionTickProfilerHooks.ConfigureProbeMission(measuring: true, behaviorTiming: false);

        MissionTickProfilerHooks.OnMissionFirstTick(_logger, 4, 250, measuring: true, behaviorTiming: false,
            profilerToggleOn: false, probeToggleOn: true);
        MissionTickProfilerHooks.OnMissionFirstTick(_logger, 5, 250, measuring: true, behaviorTiming: false,
            profilerToggleOn: false, probeToggleOn: true);

        var info = Lines(nameof(IModLogger.LogInfo));
        Assert.AreEqual(1, info.Count(l => l.StartsWith("[TickProfiler] anim-loading sample: ")), string.Join("\n", info));
        CollectionAssert.Contains(info, HitchProbeLines.BuildMissionHeader(4, "probe", 250, false, false, true));
        CollectionAssert.Contains(info, HitchProbeLines.BuildMissionHeader(5, "probe", 250, false, false, true));
        Assert.AreEqual(0, Lines(nameof(IModLogger.LogWarning)).Length);
    }

    [TestMethod]
    public void OnMissionFirstTick_BothTogglesOffWithTheProbeInstalled_SaysWhyItIsNotMeasuring()
    {
        HitchProbeInstaller.ProbeInstalled = true;
        Begin(measuring: false, behaviorTiming: false);

        MissionTickProfilerHooks.OnMissionFirstTick(_logger, 6, 250, measuring: false, behaviorTiming: false,
            profilerToggleOn: false, probeToggleOn: false);

        Assert.AreEqual(HitchProbeLines.BuildProbeMissionOffLine(6), Lines(nameof(IModLogger.LogInfo)).Single());
    }

    [TestMethod]
    public void OnMissionFirstTick_BothTogglesOffNothingInstalled_WritesNothing()
    {
        Begin(measuring: false, behaviorTiming: false);

        MissionTickProfilerHooks.OnMissionFirstTick(_logger, 6, 250, measuring: false, behaviorTiming: false,
            profilerToggleOn: false, probeToggleOn: false);

        Assert.AreEqual(0, _logger.ReceivedCalls().Count(), "The game-start probe off: line already says why.");
    }

    [TestMethod]
    public void OnMissionFirstTick_ProbeOnButNotInstalled_WarnsWithBothPossibleCauses()
    {
        Begin(measuring: false, behaviorTiming: false);

        MissionTickProfilerHooks.OnMissionFirstTick(_logger, 2, 250, measuring: false, behaviorTiming: false,
            profilerToggleOn: false, probeToggleOn: true);

        Assert.AreEqual(HitchProbeLines.ProbeNotInstalledLine, Lines(nameof(IModLogger.LogWarning)).Single());
        StringAssert.Contains(HitchProbeLines.ProbeNotInstalledLine, "off at game start");
        StringAssert.Contains(HitchProbeLines.ProbeNotInstalledLine, "install failed");
    }

    [TestMethod]
    public void WriteSummaryExtra_NoFrameClosed_WritesNothing()
    {
        var generation = Begin(measuring: true, behaviorTiming: false);

        MissionTickProfilerHooks.WriteSummary(generation, 8);
        MissionTickProfilerHooks.WriteSummaryExtra(generation, 8);

        var info = Lines(nameof(IModLogger.LogInfo));
        Assert.AreEqual(TickProfileLines.BuildNoFramesLine(generation), info.Single(),
            "A line saying there is no summary must not be followed by a summary line.");
    }

    [TestMethod]
    public void WriteSummaryExtra_ClosedFrames_WritesOneTickSummaryExtraLine()
    {
        var generation = Begin(measuring: true, behaviorTiming: false);
        MissionTickProfilerHooks.OnFrameBoundary();
        MissionTickProfilerHooks.OnFrameBoundary();

        MissionTickProfilerHooks.WriteSummaryExtra(generation, 8);

        StringAssert.StartsWith(Lines(nameof(IModLogger.LogInfo)).Single(), "[TickSummaryExtra] spawnMs=");
    }

    [TestMethod]
    public void WriteSummaryExtra_FinalizersRanWithoutTheirPrefix_WritesOneWarningWithTheCounts_ThenNothingMore()
    {
        var generation = Begin(measuring: true, behaviorTiming: false);
        MissionTickProfilerHooks.OnFrameBoundary();
        MissionTickProfilerHooks.OnFrameBoundary();
        var none = ProbeState.PrefixMissing;
        for (var i = 0; i < 3; i++)
            HitchProbeHooks.OnTickExit(ref none);
        HitchProbeHooks.OnSpawnExit(ref none);
        var firstOfEach = Lines(nameof(IModLogger.LogWarning)).Length;
        Assert.AreEqual(2, firstOfEach, "The once-per-process line of each method that had a lone exit.");

        MissionTickProfilerHooks.WriteSummaryExtra(generation, 8);

        var warnings = Lines(nameof(IModLogger.LogWarning));
        Assert.AreEqual(firstOfEach + 1, warnings.Length);
        Assert.AreEqual(HitchProbeLines.BuildPrefixMissingCounts(generation, new[] { 0, 0, 3, 0, 1 }), warnings.Last(),
            "Every lone exit is in the count, the two that wrote a line included.");
        StringAssert.StartsWith(Lines(nameof(IModLogger.LogInfo)).Single(), "[TickSummaryExtra] spawnMs=");

        MissionTickProfilerHooks.WriteSummaryExtra(generation, 8);

        Assert.AreEqual(firstOfEach + 1, Lines(nameof(IModLogger.LogWarning)).Length, "The counts were taken, so the next mission end has none to repeat.");
    }

    [TestMethod]
    public void WriteSummaryExtra_NoFrameClosedBecauseThePreTickPrefixIsGone_StillWritesTheCounts()
    {
        // The frame boundary runs from the OnPreTick prefix: with it gone no frame ever closes, so the mission has
        // no [TickSummary] and no [TickSummaryExtra], and the count is the only line that says why.
        var generation = Begin(measuring: true, behaviorTiming: false);
        var none = ProbeState.PrefixMissing;
        HitchProbeHooks.OnPreTickExit(ref none);
        HitchProbeHooks.OnPreTickExit(ref none);

        MissionTickProfilerHooks.WriteSummary(generation, 8);
        MissionTickProfilerHooks.WriteSummaryExtra(generation, 8);

        Assert.AreEqual(TickProfileLines.BuildNoFramesLine(generation), Lines(nameof(IModLogger.LogInfo)).Single());
        Assert.AreEqual(HitchProbeLines.BuildPrefixMissingCounts(generation, new[] { 2, 0, 0, 0, 0 }),
            Lines(nameof(IModLogger.LogWarning)).Last());
    }

    [TestMethod]
    public void WriteSummaryExtra_AnOlderMissionsEnd_TakesNoCounts()
    {
        var older = Begin(measuring: true, behaviorTiming: false);
        var none = ProbeState.PrefixMissing;
        HitchProbeHooks.OnTickExit(ref none);
        Begin(measuring: true, behaviorTiming: false);

        MissionTickProfilerHooks.WriteSummaryExtra(older, 8);

        CollectionAssert.AreEqual(new[] { 0, 0, 1, 0, 0 }, HitchProbeHooks.TakePrefixMissingCalls(),
            "A stale end writes and takes nothing: the current mission's end reports the calls.");
    }

    [TestMethod]
    public void OnFrameBoundary_Hitch_WritesHitchDetailRightAfterTheHitchWithTheSameT()
    {
        Begin(measuring: true, behaviorTiming: false, hitchMs: 1);
        MissionTickProfilerHooks.OnFrameBoundary();
        Thread.Sleep(5);
        MissionTickProfilerHooks.OnFrameBoundary();

        var info = Lines(nameof(IModLogger.LogInfo));
        Assert.AreEqual(2, info.Length, string.Join("\n", info));
        StringAssert.StartsWith(info[0], "[Hitch] t=+0s ");
        StringAssert.StartsWith(info[1], "[HitchDetail] t=+0s ");
        StringAssert.Contains(info[1], " mode=probe ");
    }

    // --- OnMissionCreated: the tick profiler's status at a mission's creation (deep review 2026-10-04) ---------
    // Plan 028's hook health check and plan 041's probe-aware lines met only in the integration merge, so these
    // drive the status writer without a mission, for every branch.

    private const string OnTickTranspiler = "Mission.OnTick transpiler (Patch97)";
    private const string FrameBoundary = "Mission.OnPreTick frame-boundary prefix (Patch98)";

    private void Created(bool measuring, bool behaviorTiming, bool profilerToggleOn, params string[] hookProblems) =>
        MissionTickProfilerHooks.OnMissionCreated(_logger, 3, 8, 250, measuring, behaviorTiming, profilerToggleOn, hookProblems);

    [TestMethod]
    public void OnMissionCreated_AHookMissingWhileTheProbeMeasures_SaysTheProbeStillMeasures()
    {
        Created(measuring: true, behaviorTiming: false, profilerToggleOn: true, OnTickTranspiler);

        var warning = Lines(nameof(IModLogger.LogWarning)).Single();
        StringAssert.Contains(warning, "[TickProfiler] mission 3: no per-type timing, required hooks missing: " + OnTickTranspiler);
        StringAssert.Contains(warning, "the hitch probe still measures this mission");
        Assert.IsFalse(warning.Contains("not measuring"), "The default-on probe measures this mission: " + warning);
    }

    [TestMethod]
    public void OnMissionCreated_TheFrameBoundaryMissingWhileTheProbeIsOn_SaysNotMeasuring()
    {
        // Patch98's OnPreTick prefix closes every frame in both modes, so without it the probe measures nothing either.
        Created(measuring: true, behaviorTiming: false, profilerToggleOn: true, OnTickTranspiler, FrameBoundary);

        StringAssert.Contains(Lines(nameof(IModLogger.LogWarning)).Single(),
            "[TickProfiler] mission 3: not measuring, required hooks missing: " + OnTickTranspiler + ", " + FrameBoundary + ";");
    }

    [TestMethod]
    public void OnMissionCreated_TheFrameBoundaryUncheckable_CountsAsMissing()
    {
        Created(measuring: true, behaviorTiming: false, profilerToggleOn: true, FrameBoundary + " (patch info unreadable: X)");

        StringAssert.Contains(Lines(nameof(IModLogger.LogWarning)).Single(), "mission 3: not measuring, required hooks missing: ");
    }

    [TestMethod]
    public void OnMissionCreated_AHookMissingAndTheProbeNotMeasuring_SaysNotMeasuring()
    {
        Created(measuring: false, behaviorTiming: false, profilerToggleOn: true, OnTickTranspiler);

        Assert.AreEqual(TickProfileLines.BuildHooksMissingLine(3, new[] { OnTickTranspiler }),
            Lines(nameof(IModLogger.LogWarning)).Single());
    }

    [TestMethod]
    public void OnMissionCreated_BehaviourTiming_WritesTheHeaderAndNoWarning()
    {
        MissionTickProfilerHooks.OnTickSites = 2;
        MissionTickProfilerHooks.OnPreTickSites = 2;

        Created(measuring: true, behaviorTiming: true, profilerToggleOn: true);

        Assert.AreEqual(TickProfileLines.BuildMissionStartLine(3, 8, 250, 2, 2, 1), Lines(nameof(IModLogger.LogInfo)).Single());
        Assert.AreEqual(0, Lines(nameof(IModLogger.LogWarning)).Length);
    }

    [TestMethod]
    [DataRow(true, true)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public void OnMissionCreated_OnButNeverTimingWithNoHookProblem_SaysWhyAndWhetherTheProbeMeasures(bool offAtGameStart, bool measuring)
    {
        HitchProbeInstaller.ProfilerOffAtGameStart = offAtGameStart;

        Created(measuring, behaviorTiming: false, profilerToggleOn: true);

        Assert.AreEqual(HitchProbeLines.ProfilerNotTimingLine(restartNeeded: offAtGameStart, probeMeasuring: measuring),
            Lines(nameof(IModLogger.LogWarning)).Single());
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void OnMissionCreated_InstalledButSwitchedOff_WritesTheOffLine(bool measuring)
    {
        MissionTickProfilerHooks.Installed = true;

        Created(measuring, behaviorTiming: false, profilerToggleOn: false);

        Assert.AreEqual(HitchProbeLines.BuildProfilerOffLine(3, measuring), Lines(nameof(IModLogger.LogInfo)).Single());
        Assert.AreEqual(0, Lines(nameof(IModLogger.LogWarning)).Length);
    }

    [TestMethod]
    public void OnMissionCreated_OffAndNeverInstalled_WritesNothing()
    {
        Created(measuring: true, behaviorTiming: false, profilerToggleOn: false);

        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
    }
}
