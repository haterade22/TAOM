using System;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionPerf;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The contract of the Patch98 hitch probe's lines: five data lines read by plan 029's generic-tag parser
/// and the <c>[TickProfiler]</c> status lines, each pinned literally (plan 041, Design "Line contract").
/// </summary>
[TestClass]
public class HitchProbeLinesTests
{
    private const string PinnedSpawnProfile =
        "[SpawnProfile] t=+10s spawns=412 spawnMs=286.40 top=AdvancedCombatBehavior:40.20/412,WargMissionBehavior:12.75/412";
    private const string PinnedScriptProfileFull =
        "[ScriptProfile] t=+65s calls=300 scriptTickMs=310.50 scriptParallelMs=120.25 occasionalMs=8.00 top=TaomHowdahMachine:95.10/1200/2.40,SiegeTower:40.00/300/0.90";
    private const string PinnedScriptProfileProbe =
        "[ScriptProfile] t=+65s calls=300 scriptTickMs=310.50 scriptParallelMs=na occasionalMs=na top=none";
    private const string PinnedAnimLoad = "[AnimLoad] t=+65s loadingFrames=3 frames=300";
    private const string PinnedHitchDetailFull =
        "[HitchDetail] t=+72s spawnMs=0.00 scriptTickMs=4.10 scriptParallelMs=1.25 animLoading=1 mode=full spawns=0 occasionalMs=0.30 onTickMs=6.30 preTickAllMs=790.60";
    private const string PinnedHitchDetailProbe =
        "[HitchDetail] t=+72s spawnMs=12.00 scriptTickMs=4.10 scriptParallelMs=na animLoading=na mode=probe spawns=3 occasionalMs=na onTickMs=20.50 preTickAllMs=790.60";
    private const string PinnedSummaryExtraFull =
        "[TickSummaryExtra] spawnMs=1843.20 scriptTickMs=6020.75 animLoadingFrames=41 hitchesWithAnimLoading=3 mode=full frames=18000 spawns=1313 preFrameSpawns=2 preFrameSpawnMs=3.50 offMainSpawns=0 scriptParallelMs=2400.50 occasionalMs=160.00 onTickMs=41000.25 preTickAllMs=9800.00 spawnTop=AdvancedCombatBehavior:210.40/1313 scriptTop=TaomHowdahMachine:1900.20/72000/4.80";
    private const string PinnedSummaryExtraProbe =
        "[TickSummaryExtra] spawnMs=1843.20 scriptTickMs=6020.75 animLoadingFrames=na hitchesWithAnimLoading=na mode=probe frames=18000 spawns=1313 preFrameSpawns=0 preFrameSpawnMs=0.00 offMainSpawns=0 scriptParallelMs=na occasionalMs=na onTickMs=41000.25 preTickAllMs=9800.00 spawnTop=none scriptTop=none";

    private static readonly string[] DataTags =
    {
        "[TickProfile]", "[Hitch]", "[PerfContext]", "[MissionPerf]", "[TickSummary]",
        "[SpawnProfile]", "[ScriptProfile]", "[AnimLoad]", "[HitchDetail]", "[TickSummaryExtra]",
    };

    private static ExtrasWindow SpawnWindow(bool withTop = true) => new ExtrasWindow(412, 286.4,
        withTop
            ? new[] { new BehaviorTotal("AdvancedCombatBehavior", 40.2, 412, 1, 0), new BehaviorTotal("WargMissionBehavior", 12.75, 412, 1, 0) }
            : Array.Empty<BehaviorTotal>(),
        0, 0, double.NaN, double.NaN, Array.Empty<BehaviorTotal>(), 300, 0, false);

    private static ExtrasWindow ScriptWindow(bool full) => new ExtrasWindow(0, 0, Array.Empty<BehaviorTotal>(),
        300, 310.5, full ? 120.25 : double.NaN, full ? 8 : double.NaN,
        full
            ? new[] { new BehaviorTotal("TaomHowdahMachine", 95.1, 1200, 2.4, 0), new BehaviorTotal("SiegeTower", 40, 300, 0.9, 0) }
            : Array.Empty<BehaviorTotal>(),
        300, 3, true);

    private static HitchDetailFrame FullDetail() => new HitchDetailFrame(0, 4.1, 1.25, 1, "full", 0, 0.3, 6.3, 790.6);

    private static HitchDetailFrame ProbeDetail() => new HitchDetailFrame(12, 4.1, double.NaN, -1, "probe", 3, double.NaN, 20.5, 790.6);

    private static MissionExtras FullMission() => new MissionExtras(1843.2, 6020.75, 41, 3, "full", 18000, 1313, 2, 3.5, 0,
        2400.5, 160, 41000.25, 9800,
        new[] { new BehaviorTotal("AdvancedCombatBehavior", 210.4, 1313, 1, 0) },
        new[] { new BehaviorTotal("TaomHowdahMachine", 1900.2, 72000, 4.8, 0) });

    private static MissionExtras ProbeMission() => new MissionExtras(1843.2, 6020.75, -1, -1, "probe", 18000, 1313, 0, 0, 0,
        double.NaN, double.NaN, 41000.25, 9800, Array.Empty<BehaviorTotal>(), Array.Empty<BehaviorTotal>());

    [TestMethod]
    public void BuildSpawnProfile_SampleWindow_MatchesThePinnedLiteral()
        => Assert.AreEqual(PinnedSpawnProfile, HitchProbeLines.BuildSpawnProfile(10.4, SpawnWindow()));

    [TestMethod]
    public void BuildSpawnProfile_NoAttribution_WritesTopNone()
    {
        // Without attribution the profiler's window carries an empty top (TakeExtrasWindow), which reads none.
        Assert.AreEqual("[SpawnProfile] t=+10s spawns=412 spawnMs=286.40 top=none",
            HitchProbeLines.BuildSpawnProfile(10.4, SpawnWindow(withTop: false)));
    }

    [TestMethod]
    public void BuildScriptProfile_FullWindow_MatchesThePinnedLiteral()
        => Assert.AreEqual(PinnedScriptProfileFull, HitchProbeLines.BuildScriptProfile(65.2, ScriptWindow(full: true)));

    [TestMethod]
    public void BuildScriptProfile_ProbeWindow_MatchesThePinnedLiteral()
        => Assert.AreEqual(PinnedScriptProfileProbe, HitchProbeLines.BuildScriptProfile(65.2, ScriptWindow(full: false)));

    [TestMethod]
    public void BuildAnimLoad_SampleWindow_MatchesThePinnedLiteral()
        => Assert.AreEqual(PinnedAnimLoad, HitchProbeLines.BuildAnimLoad(65.2, ScriptWindow(full: true)));

    [TestMethod]
    public void BuildHitchDetail_FullFrame_MatchesThePinnedLiteral()
        => Assert.AreEqual(PinnedHitchDetailFull, HitchProbeLines.BuildHitchDetail(72.4, FullDetail()));

    [TestMethod]
    public void BuildHitchDetail_ProbeFrameWithoutSampler_MatchesThePinnedLiteral()
        => Assert.AreEqual(PinnedHitchDetailProbe, HitchProbeLines.BuildHitchDetail(72.4, ProbeDetail()));

    [TestMethod]
    public void BuildTickSummaryExtra_FullMission_MatchesThePinnedLiteral()
        => Assert.AreEqual(PinnedSummaryExtraFull, HitchProbeLines.BuildTickSummaryExtra(FullMission()));

    [TestMethod]
    public void BuildTickSummaryExtra_ProbeMissionWithoutSampler_MatchesThePinnedLiteral()
        => Assert.AreEqual(PinnedSummaryExtraProbe, HitchProbeLines.BuildTickSummaryExtra(ProbeMission()));

    [TestMethod]
    public void BuildProbeInstallLine_Applied_MatchesThePinnedLiteral()
        => Assert.AreEqual(
            "[TickProfiler] probe install: category applied, enabled by hitch probe, targets Mission.OnPreTick,Mission.WaitTickCompletion,Mission.OnTick,ManagedScriptHolder.TickComponents,Mission.SpawnAgent, bookkeeping 1.25 us per frame (0.01% of a 10 ms frame, target 0.50%)",
            HitchProbeLines.BuildProbeInstallLine(true, "hitch probe", 1.25));

    [TestMethod]
    public void BuildAttributionInstallLine_AllSitesFound_MatchesThePinnedLiteral()
        => Assert.AreEqual(
            "[TickProfiler] attribution install: Mission.SpawnAgent sites 2/2, ManagedScriptHolder.TickComponents sites 5/5, script tick delegate bound",
            HitchProbeLines.BuildAttributionInstallLine(2, 5, 5, true));

    [TestMethod]
    public void BuildMissionHeader_Probe_MatchesThePinnedLiteral()
        => Assert.AreEqual(
            "[TickProfiler] mission 1: mode probe, hitch threshold 250 ms, spawn attribution off, script attribution off, anim-loading sample on",
            HitchProbeLines.BuildMissionHeader(1, "probe", 250, false, false, true));

    [TestMethod]
    public void BuildAnimCostLine_UnderBudget_MatchesThePinnedLiteral()
        => Assert.AreEqual(
            "[TickProfiler] anim-loading sample: MBAnimation.IsAnyAnimationLoadingFromDisk median 3.20 us over 32 calls (budget 20.00 us); sampling every frame",
            HitchProbeLines.BuildAnimCostLine(3.2, 32, 20, true));

    [TestMethod]
    public void Build_CommaDecimalCulture_StillWritesInvariantPoints()
    {
        var saved = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            Assert.AreEqual(PinnedSpawnProfile, HitchProbeLines.BuildSpawnProfile(10.4, SpawnWindow()));
            Assert.AreEqual(PinnedScriptProfileFull, HitchProbeLines.BuildScriptProfile(65.2, ScriptWindow(full: true)));
            Assert.AreEqual(PinnedHitchDetailFull, HitchProbeLines.BuildHitchDetail(72.4, FullDetail()));
            Assert.AreEqual(PinnedSummaryExtraFull, HitchProbeLines.BuildTickSummaryExtra(FullMission()));
            StringAssert.Contains(HitchProbeLines.BuildProbeInstallLine(true, "both", 1.25), "bookkeeping 1.25 us per frame (0.01% ");
            StringAssert.Contains(HitchProbeLines.BuildAnimCostLine(3.2, 32, 20, true), "median 3.20 us");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = saved;
        }
    }

    [TestMethod]
    public void StatusLines_NeverContainADataTag()
    {
        var withTags = new InvalidOperationException("[Hitch] [HitchDetail] [TickSummaryExtra] in a message");
        var lines = new[]
        {
            HitchProbeLines.ProbeOffLine,
            HitchProbeLines.ScriptDelegateUnboundLine,
            HitchProbeLines.ProbeNotInstalledLine,
            HitchProbeLines.WaitUnseenLine,
            HitchProbeLines.PreTickPrefixMissingLine,
            HitchProbeLines.WaitPrefixMissingLine,
            HitchProbeLines.OnTickPrefixMissingLine,
            HitchProbeLines.ScriptPrefixMissingLine,
            HitchProbeLines.SpawnPrefixMissingLine,
            HitchProbeLines.BuildProbeInstallLine(true, "hitch probe", 1.25),
            HitchProbeLines.BuildProbeInstallLine(false, "both", 0),
            HitchProbeLines.BuildAttributionInstallLine(2, 5, 5, true),
            HitchProbeLines.BuildAttributionInstallLine(0, 3, 4, false),
            HitchProbeLines.BuildMissionHeader(1, "off", 250, false, false, false),
            HitchProbeLines.BuildScriptThreadLine(true, 1, 1),
            HitchProbeLines.BuildScriptThreadLine(false, 7, 1),
            HitchProbeLines.BuildSpawnOffMainLine(7),
            HitchProbeLines.BuildAnimCostLine(3.2, 32, 20, true),
            HitchProbeLines.BuildAnimCostLine(25, 32, 20, false),
            HitchProbeLines.BuildAnimFault(withTags),
            HitchProbeLines.BuildHookFault("spawn", withTags),
            HitchProbeLines.BuildAttributionFault("agent build", withTags),
            HitchProbeLines.ProfilerNotTimingLine(restartNeeded: true, probeMeasuring: true),
            HitchProbeLines.ProfilerNotTimingLine(restartNeeded: false, probeMeasuring: true),
            HitchProbeLines.BuildProfilerOffLine(3, probeMeasuring: true),
            HitchProbeLines.BuildHooksMissingLine(3, new[] { "Mission.OnTick transpiler (Patch97)" }, probeMeasuring: true),
            HitchProbeLines.BuildProbeMissionOffLine(3),
        };
        foreach (var line in lines)
        {
            StringAssert.StartsWith(line, "[TickProfiler] ");
            foreach (var tag in DataTags)
                Assert.IsFalse(line.Contains(tag), $"'{line}' contains {tag}");
        }
    }

    /// <summary>Review 041: with the hitch probe measuring, the tick profiler's not-timing lines must not say
    /// that nothing is measured; without it, they are plan 028's lines unchanged.</summary>
    [TestMethod]
    public void ProbeAwareStatusLines_MatchThePinnedLiterals()
    {
        var pins = new (string Actual, string Expected)[]
        {
            (HitchProbeLines.ProfilerNotTimingLine(restartNeeded: true, probeMeasuring: true),
                "[TickProfiler] on in MCM but it was off at game start, so per-type timing is not installed; the hitch probe still measures this mission; restart the game for per-type timing"),
            (HitchProbeLines.ProfilerNotTimingLine(restartNeeded: false, probeMeasuring: true),
                "[TickProfiler] on in MCM but its install at game start failed, so per-type timing is off; the hitch probe still measures this mission; see the [TickProfiler] install line and [PatchApply]"),
            (HitchProbeLines.ProfilerNotTimingLine(restartNeeded: true, probeMeasuring: false), TickProfileLines.RestartNeededLine),
            (HitchProbeLines.ProfilerNotTimingLine(restartNeeded: false, probeMeasuring: false), TickProfileLines.NotInstalledLine),
            (HitchProbeLines.BuildProfilerOffLine(3, probeMeasuring: true),
                "[TickProfiler] mission 3: no per-type timing, 'Enable Tick Profiler' is off in MCM; the hitch probe still measures this mission, and the profiler's patches only call through until a restart"),
            (HitchProbeLines.BuildProfilerOffLine(3, probeMeasuring: false), TickProfileLines.BuildMissionOffLine(3)),
            (HitchProbeLines.BuildHooksMissingLine(3, new[] { "Mission.OnTick transpiler (Patch97)", "Mission.OnTick call sites 0/2" }, probeMeasuring: true),
                "[TickProfiler] mission 3: no per-type timing, required hooks missing: Mission.OnTick transpiler (Patch97), Mission.OnTick call sites 0/2; the hitch probe still measures this mission; another mod's transpiler, a PatchShield strip or a failed patch apply left them out, and the next mission checks again"),
            (HitchProbeLines.BuildHooksMissingLine(3, new[] { "Mission.OnTick transpiler (Patch97)" }, probeMeasuring: false),
                TickProfileLines.BuildHooksMissingLine(3, new[] { "Mission.OnTick transpiler (Patch97)" })),
            (HitchProbeLines.BuildProbeMissionOffLine(3),
                "[TickProfiler] mission 3: not measuring, 'Enable Hitch Probe' and 'Enable Tick Profiler' are off in MCM; the probe's patches stay installed and only call through until a restart"),
            (HitchProbeLines.ProbeNotInstalledLine,
                "[TickProfiler] probe on in MCM but its patches are not installed: it was off at game start (restart the game to measure) or its install failed (see the probe install line and [PatchApply])"),
            (HitchProbeLines.BuildAttributionFault("agent build", new InvalidOperationException("boom [x]")),
                "[TickProfiler] agent build hook failed, all per-type attribution (spawn callbacks, script components, script blocks) is off for this process: InvalidOperationException: boom (x)"),
        };
        foreach (var (actual, expected) in pins)
            Assert.AreEqual(expected, actual);
    }

    /// <summary>A finalizer that ran with no prefix before it (PatchShield strips prefixes, never finalizers; an
    /// earlier prefix that throws stops ours from running): one line per method, each saying that call is not
    /// measured and what that bracket's columns read while the prefix stays gone.</summary>
    [TestMethod]
    public void PrefixMissingLines_MatchThePinnedLiterals()
    {
        var pins = new (string Actual, string Expected)[]
        {
            (HitchProbeLines.PreTickPrefixMissingLine,
                "[TickProfiler] Mission.OnPreTick: the hitch probe bracket's prefix did not run before its finalizer (it was removed, or an earlier prefix threw): that call is not measured, and while the prefix stays gone no frame boundary runs, no frame closes and no hitch is detected; this line is written once per process, each measured mission's end counts these calls"),
            (HitchProbeLines.WaitPrefixMissingLine,
                "[TickProfiler] Mission.WaitTickCompletion: the hitch probe bracket's prefix did not run before its finalizer (it was removed, or an earlier prefix threw): that call is not measured, and while the prefix stays gone in probe mode waitTickMs reads 0 and preTickMs includes the wait; this line is written once per process, each measured mission's end counts these calls"),
            (HitchProbeLines.OnTickPrefixMissingLine,
                "[TickProfiler] Mission.OnTick: the hitch probe bracket's prefix did not run before its finalizer (it was removed, or an earlier prefix threw): that call is not measured, and while the prefix stays gone onTickMs reads 0 and, in probe mode, so does missionTickMs; this line is written once per process, each measured mission's end counts these calls"),
            (HitchProbeLines.ScriptPrefixMissingLine,
                "[TickProfiler] ManagedScriptHolder.TickComponents: the hitch probe bracket's prefix did not run before its finalizer (it was removed, or an earlier prefix threw): that call is not measured, and while the prefix stays gone scriptTickMs and calls read 0; this line is written once per process, each measured mission's end counts these calls"),
            (HitchProbeLines.SpawnPrefixMissingLine,
                "[TickProfiler] Mission.SpawnAgent: the hitch probe bracket's prefix did not run before its finalizer (it was removed, or an earlier prefix threw): that call is not measured, and while the prefix stays gone spawns and spawnMs read 0; this line is written once per process, each measured mission's end counts these calls"),
        };
        foreach (var (actual, expected) in pins)
            Assert.AreEqual(expected, actual);
    }

    /// <summary>The mission-end total of those calls (the once-per-process warning above only says it began):
    /// the methods that had any, in bracket order, and nothing about the ones that had none.</summary>
    [TestMethod]
    public void PrefixMissingCounts_MatchThePinnedLiterals()
    {
        Assert.AreEqual(
            "[TickProfiler] mission end for generation 4: hitch probe finalizers ran without their prefix since the last such line (or game start), so these calls were not measured: Mission.OnTick 1204, Mission.SpawnAgent 17",
            HitchProbeLines.BuildPrefixMissingCounts(4, new[] { 0, 0, 1204, 0, 17 }));
        Assert.AreEqual(
            "[TickProfiler] mission end for generation 1: hitch probe finalizers ran without their prefix since the last such line (or game start), so these calls were not measured: Mission.OnPreTick 1, Mission.WaitTickCompletion 2, Mission.OnTick 3, ManagedScriptHolder.TickComponents 4, Mission.SpawnAgent 5",
            HitchProbeLines.BuildPrefixMissingCounts(1, new[] { 1, 2, 3, 4, 5 }));
        foreach (var tag in DataTags)
            Assert.IsFalse(HitchProbeLines.BuildPrefixMissingCounts(1, new[] { 1, 2, 3, 4, 5 }).Contains(tag), "A status line never quotes " + tag);
    }

    [TestMethod]
    public void DataTags_NeverContainAnotherDataTag()
    {
        foreach (var tag in new[] { "[SpawnProfile]", "[ScriptProfile]", "[AnimLoad]", "[HitchDetail]", "[TickSummaryExtra]" })
        {
            foreach (var other in DataTags)
            {
                if (other == tag)
                    continue;
                Assert.IsFalse(tag.Contains(other), $"{tag} contains {other}");
            }
        }
    }
}
