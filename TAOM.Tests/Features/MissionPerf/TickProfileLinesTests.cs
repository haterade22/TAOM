using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionPerf;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The line contract of the Patch97 tick profiler. The three data literals are shared with plan 029's
/// log parser (its PINNED_TICK_PROFILE, PINNED_HITCH and PINNED_PERF_CONTEXT): change them only
/// together with its tests. Status lines carry <c>[TickProfiler]</c>, which does not contain the
/// substring <c>[TickProfile]</c>, so the parser never mistakes one for a malformed data line.
/// </summary>
[TestClass]
public class TickProfileLinesTests
{
    private const string PinnedTickProfile =
        "[TickProfile] t=+65s frames=300 wallMs=5000.00 preDisplayMs=12.50 missionTickMs=812.40 preTickMs=40.10 waitTickMs=95.00 agentTickMs=1500.00 otherMs=4040.00 allocKB=2048 top=BehaviorTreeMissionLogic:410.20/300/3.10/512,AdvancedCombatBehavior:120.00/900/1.50/64";

    private const string PinnedHitch =
        "[Hitch] t=+72s frameMs=812.35 preDisplayMs=0.40 missionTickMs=5.20 preTickMs=0.30 waitTickMs=790.00 agentTickMs=795.10 otherMs=16.45 gc0=1 gc1=1 gc2=0 allocKB=96 top=BehaviorTreeMissionLogic:2.10,AdvancedCombatBehavior:1.30,MissionPerfHeartbeatBehavior:0.05";

    private const string PinnedPerfContext =
        "[PerfContext] build=Debug jitOptimized=false clr=4.0.30319.42000 serverGC=false latency=Interactive missionInProcess=1 scene=battle_terrain_029 agents=0 textureQuality=1 shadowQuality=2 particleDetail=1 ragdolls=3 memLoad=61 availPhysMB=12034 tickProfiler=on diag=battleLoad,stallWatchdog,stallBundle,exitSampler,freezeSampler,memSampler,missionPerf";

    // The mission-end summary added by the plan's 2026-10-03 amendment; inputs below.
    private const string PinnedTickSummary =
        "[TickSummary] frames=4500 wallMs=75000.00 preDisplayMs=187.50 missionTickMs=12186.00 preTickMs=601.50 waitTickMs=1425.00 agentTickMs=22500.00 otherMs=60600.00 allocKB=30720 hitches=3 worstHitchMs=1104.20 worstHitchT=+72s top=BehaviorTreeMissionLogic:6153.00/4500/3.10/7680,AdvancedCombatBehavior:1800.00/13500/1.50/960";

    private const string PinnedInstallLine =
        "[TickProfiler] install: category applied, Mission.OnTick sites 2/2, Mission.OnPreTick sites 2/2, allocation counter available";

    private static TickWindow SampleWindow(IReadOnlyList<BehaviorTotal>? top = null) => new TickWindow(
        300, 5000, 12.5, 812.4, 40.1, 95, 1500, 4040, 2_097_152,
        top ?? new[]
        {
            new BehaviorTotal("BehaviorTreeMissionLogic", 410.2, 300, 3.1, 524_288),
            new BehaviorTotal("AdvancedCombatBehavior", 120, 900, 1.5, 65_536),
        });

    private static HitchFrame SampleHitch(IReadOnlyList<BehaviorTotal>? top = null) => new HitchFrame(
        812.35, 0.4, 5.2, 0.3, 790, 795.1, 16.45, 1, 1, 0, 98_304,
        top ?? new[]
        {
            new BehaviorTotal("BehaviorTreeMissionLogic", 2.1, 1, 2.1, 0),
            new BehaviorTotal("AdvancedCombatBehavior", 1.3, 3, 0.5, 0),
            new BehaviorTotal("MissionPerfHeartbeatBehavior", 0.05, 1, 0.05, 0),
        });

    private static TickSummary SampleSummary(int hitches = 3) => new TickSummary(
        4500, 75000, 187.5, 12186, 601.5, 1425, 22500, 60600, 31_457_280, hitches, hitches > 0 ? 1104.2 : 0,
        hitches > 0 ? 72.4 : 0,
        new[]
        {
            new BehaviorTotal("BehaviorTreeMissionLogic", 6153, 4500, 3.1, 7_864_320),
            new BehaviorTotal("AdvancedCombatBehavior", 1800, 13500, 1.5, 983_040),
        });

    private static PerfContext SampleContext(int quality = 1, int ragdolls = 3, int memLoad = 61, long availPhysMb = 12034,
        IReadOnlyList<string>? diag = null) => new PerfContext(
        "Debug", false, "4.0.30319.42000", false, "Interactive", 1, "battle_terrain_029", 0,
        quality, quality == 1 ? 2 : quality, quality, ragdolls, memLoad, availPhysMb, true,
        diag ?? TickProfileLines.DiagTokens(true, true, true, true, true, true, true));

    [TestMethod]
    public void BuildTickProfile_SampleWindow_MatchesThePinnedLiteral()
        => Assert.AreEqual(PinnedTickProfile, TickProfileLines.BuildTickProfile(65.2, SampleWindow(), allocAvailable: true));

    [TestMethod]
    public void BuildHitch_SampleFrame_MatchesThePinnedLiteral()
        => Assert.AreEqual(PinnedHitch, TickProfileLines.BuildHitch(72.4, SampleHitch(), allocAvailable: true));

    [TestMethod]
    public void BuildPerfContext_SampleContext_MatchesThePinnedLiteral()
        => Assert.AreEqual(PinnedPerfContext, TickProfileLines.BuildPerfContext(SampleContext()));

    [TestMethod]
    public void BuildTickSummary_SampleMission_MatchesThePinnedLiteral()
        => Assert.AreEqual(PinnedTickSummary, TickProfileLines.BuildTickSummary(SampleSummary(), allocAvailable: true));

    [TestMethod]
    public void BuildTickSummary_NoHitches_WritesWorstHitchNa()
    {
        var line = TickProfileLines.BuildTickSummary(SampleSummary(hitches: 0), allocAvailable: false);
        StringAssert.Contains(line, " allocKB=na hitches=0 worstHitchMs=0.00 worstHitchT=na top=");
        StringAssert.Contains(line, "BehaviorTreeMissionLogic:6153.00/4500/3.10/na,");
    }

    [TestMethod]
    public void BuildTickProfile_NoAllocationCounter_WritesNaForEveryKb()
    {
        var line = TickProfileLines.BuildTickProfile(65.2, SampleWindow(), allocAvailable: false);
        StringAssert.Contains(line, " allocKB=na top=");
        StringAssert.Contains(line, "BehaviorTreeMissionLogic:410.20/300/3.10/na,AdvancedCombatBehavior:120.00/900/1.50/na");
        StringAssert.Contains(TickProfileLines.BuildHitch(72.4, SampleHitch(), allocAvailable: false), " allocKB=na top=");
    }

    [TestMethod]
    public void BuildTickProfile_NoBehaviours_WritesTopNone()
        => StringAssert.EndsWith(
            TickProfileLines.BuildTickProfile(65.2, SampleWindow(Array.Empty<BehaviorTotal>()), allocAvailable: true),
            " allocKB=2048 top=none");

    [TestMethod]
    public void BuildHitch_NoBehaviours_WritesTopNone()
        => StringAssert.EndsWith(
            TickProfileLines.BuildHitch(72.4, SampleHitch(Array.Empty<BehaviorTotal>()), allocAvailable: true),
            " allocKB=96 top=none");

    [TestMethod]
    public void Build_CommaDecimalCulture_StillWritesInvariantPoints()
    {
        var saved = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            Assert.AreEqual(PinnedTickProfile, TickProfileLines.BuildTickProfile(65.2, SampleWindow(), allocAvailable: true));
            Assert.AreEqual(PinnedHitch, TickProfileLines.BuildHitch(72.4, SampleHitch(), allocAvailable: true));
            Assert.AreEqual(PinnedTickSummary, TickProfileLines.BuildTickSummary(SampleSummary(), allocAvailable: true));
            StringAssert.Contains(TickProfileLines.BuildMissionStartLine(2, 8, 250.5, 2, 2, 2), "hitch threshold 250.5 ms");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = saved;
        }
    }

    [TestMethod]
    public void BuildPerfContext_UnreadableValues_WriteNa()
    {
        var line = TickProfileLines.BuildPerfContext(SampleContext(quality: -1, ragdolls: -1, memLoad: -1, availPhysMb: -1));
        StringAssert.Contains(line,
            " textureQuality=na shadowQuality=na particleDetail=na ragdolls=na memLoad=na availPhysMB=na tickProfiler=on ");
    }

    [TestMethod]
    public void BuildPerfContext_NoDiagnostics_WritesNone()
        => StringAssert.EndsWith(
            TickProfileLines.BuildPerfContext(SampleContext(diag: TickProfileLines.DiagTokens(false, false, false, false, false, false, false))),
            " tickProfiler=on diag=none");

    [TestMethod]
    public void DiagTokens_AllOn_ListsSevenInContractOrder()
        => CollectionAssert.AreEqual(
            new[] { "battleLoad", "stallWatchdog", "stallBundle", "exitSampler", "freezeSampler", "memSampler", "missionPerf" },
            new List<string>(TickProfileLines.DiagTokens(true, true, true, true, true, true, true)));

    [TestMethod]
    public void DiagTokens_SomeOn_ListsOnlyThoseInOrder()
        => CollectionAssert.AreEqual(
            new[] { "battleLoad", "stallWatchdog", "freezeSampler", "missionPerf" },
            new List<string>(TickProfileLines.DiagTokens(true, true, false, false, true, false, true)));

    [TestMethod]
    public void DiagTokens_MasterOff_OmitsTheThreeDiagnosticsItGates()
        => CollectionAssert.AreEqual(
            new[] { "freezeSampler", "memSampler", "missionPerf" },
            new List<string>(TickProfileLines.DiagTokens(false, true, true, true, true, true, true)));

    [TestMethod]
    public void DiagTokens_WatchdogOff_OmitsItsBundle()
        => CollectionAssert.AreEqual(
            new[] { "battleLoad", "exitSampler" },
            new List<string>(TickProfileLines.DiagTokens(true, false, true, true, false, false, false)));

    [TestMethod]
    public void BuildInstallLine_AllSitesFound_MatchesThePinnedLiteral()
        => Assert.AreEqual(PinnedInstallLine, TickProfileLines.BuildInstallLine(true, 2, 2, 2, true));

    [TestMethod]
    public void StatusLines_MatchTheirPinnedLiterals()
    {
        var pins = new (string Actual, string Expected)[]
        {
            (TickProfileLines.OffLine,
                "[TickProfiler] off: 'Enable Tick Profiler' is off at game start (or MCM was not ready); no patches installed"),
            (TickProfileLines.WaitUnboundLine,
                "[TickProfiler] WaitTickCompletion could not be bound; waitTickMs reads 0 and the wait lands in otherMs"),
            (TickProfileLines.NotInstalledLine,
                "[TickProfiler] on in MCM but the install at game start failed, so nothing is measured; see the [TickProfiler] install line and [PatchApply]"),
            (TickProfileLines.RestartNeededLine,
                "[TickProfiler] on in MCM but it was off at game start, so no patches are installed and nothing is measured; restart the game to measure"),
            (TickProfileLines.BuildMissionOffLine(3),
                "[TickProfiler] mission 3: not measuring, 'Enable Tick Profiler' is off in MCM; its patches stay installed and only call through until a restart"),
            (TickProfileLines.BuildMissionStartLine(2, 8, 250, 2, 2, 2),
                "[TickProfiler] mission 2: measuring, top 8 behaviours per line, hitch threshold 250 ms, first 100 hitch frames written in full, sites Mission.OnTick 2/2 Mission.OnPreTick 2/2"),
            (TickProfileLines.BuildHooksMissingLine(3, new[] { "Mission.OnTick call sites 0/2", "Mission.OnPreTick frame-boundary prefix (Patch97)" }),
                "[TickProfiler] mission 3: not measuring, required hooks missing: Mission.OnTick call sites 0/2, Mission.OnPreTick frame-boundary prefix (Patch97); another mod's transpiler, a PatchShield strip or a failed patch apply left them out, and the next mission checks again"),
            (TickProfileLines.BuildHooksLostLine(3, 65.4, "Mission.OnTick call sites 0/2"),
                "[TickProfiler] mission 3: measuring stopped at t=+65s, required hooks missing: Mission.OnTick call sites 0/2; they were in place at this mission's start, so a later patch on the method took them out, and the next mission checks again"),
            (TickProfileLines.BuildSiteCountWarning("Mission.OnTick", "MissionBehavior.OnMissionTick", 0),
                "[TickProfiler] Mission.OnTick: MissionBehavior.OnMissionTick matched 0 times, expected 1; Mission.OnTick left vanilla, so no mission is measured"),
            (TickProfileLines.BuildSiteCountWarning("Mission.OnPreTick", "MissionBehavior.OnPreMissionTick", 2),
                "[TickProfiler] Mission.OnPreTick: MissionBehavior.OnPreMissionTick matched 2 times, expected 1; Mission.OnPreTick left vanilla, so waitTickMs and preTickMs read 0 and that time lands in otherMs"),
            (TickProfileLines.BuildHelperMismatchWarning("Mission.OnPreTick", "MissionTickProfilerHooks.TimedPreMissionTick", "MissionBehavior.OnPreMissionTick"),
                "[TickProfiler] Mission.OnPreTick: helper MissionTickProfilerHooks.TimedPreMissionTick does not fit MissionBehavior.OnPreMissionTick; Mission.OnPreTick left vanilla, so waitTickMs and preTickMs read 0 and that time lands in otherMs"),
            (TickProfileLines.BuildRewriteFault("Mission.OnTick", new InvalidOperationException("boom [x]")),
                "[TickProfiler] Mission.OnTick rewrite failed: InvalidOperationException: boom (x); Mission.OnTick left vanilla, so no mission is measured"),
            (TickProfileLines.BuildRewriteFault("Other.Method", new InvalidOperationException("boom")),
                "[TickProfiler] Other.Method rewrite failed: InvalidOperationException: boom; Other.Method left vanilla, so the profiler records nothing for it"),
            (TickProfileLines.BuildFault("frame boundary", new InvalidOperationException("boom [x]")),
                "[TickProfiler] frame boundary failed, measuring stopped: InvalidOperationException: boom (x)"),
            (TickProfileLines.BuildContextReadFault("scene", new NullReferenceException("gone")),
                "[TickProfiler] context read of scene failed, that field falls back to na, -1 or unknown: NullReferenceException: gone"),
            (TickProfileLines.BuildStaleEndLine(4, 5),
                "[TickProfiler] mission end for generation 4 ignored: generation 5 is current and keeps measuring"),
            (TickProfileLines.BuildHitchCapLine(100, 412.3),
                "[TickProfiler] hitch line cap reached at t=+412s: the first 100 slow frames of this mission were written in full; later ones are counted only in the mission summary's hitches= and worstHitchMs="),
            (TickProfileLines.BuildNoFramesLine(4),
                "[TickProfiler] mission end for generation 4: no frame closed while measuring, so no mission summary"),
            (TickProfileLines.MemoryReadFailedLine,
                "[TickProfiler] memory status read failed, memLoad and availPhysMB fall back to na"),
        };
        foreach (var (actual, expected) in pins)
            Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void SettingFallbackLines_BothInRange_ReturnsNone()
        => Assert.AreEqual(0, TickProfileLines.SettingFallbackLines(8, 8, 250, 250d).Count);

    [TestMethod]
    public void SettingFallbackLines_BothOutOfRange_NamesEachSettingItsRawValueAndTheValueUsed()
        => CollectionAssert.AreEqual(
            new[]
            {
                "[TickProfiler] MCM 'Tick Profiler Top Behaviours' reads 0, out of range, so 8 is used",
                "[TickProfiler] MCM 'Hitch Threshold (ms)' reads 10, out of range, so 250 ms is used",
            },
            new List<string>(TickProfileLines.SettingFallbackLines(0, 8, 10, 250d)));

    [TestMethod]
    public void SettingFallbackLines_OnlyThresholdOutOfRange_ReturnsOnlyItsLine()
        => CollectionAssert.AreEqual(
            new[] { "[TickProfiler] MCM 'Hitch Threshold (ms)' reads 5000, out of range, so 250 ms is used" },
            new List<string>(TickProfileLines.SettingFallbackLines(8, 8, 5000, 250d)));

    [TestMethod]
    public void StatusLines_NeverContainADataTag()
    {
        var lines = new[]
        {
            TickProfileLines.OffLine,
            TickProfileLines.WaitUnboundLine,
            TickProfileLines.NotInstalledLine,
            TickProfileLines.RestartNeededLine,
            TickProfileLines.BuildMissionOffLine(3),
            TickProfileLines.BuildInstallLine(false, 0, 1, 1, false),
            TickProfileLines.BuildInstallLine(true, 2, 2, 2, true),
            TickProfileLines.BuildSiteCountWarning("Mission.OnTick", "MissionBehavior.OnMissionTick", 0),
            TickProfileLines.BuildHelperMismatchWarning("Mission.OnPreTick", "MissionTickProfilerHooks.TimedPreMissionTick", "MissionBehavior.OnPreMissionTick"),
            TickProfileLines.BuildFault("frame boundary", new InvalidOperationException("[Hitch] [TickProfile] in a message")),
            TickProfileLines.BuildRewriteFault("Mission.OnTick", new InvalidOperationException("[Hitch] [TickSummary] in a message")),
            TickProfileLines.BuildMissionStartLine(3, 8, 250, 2, 2, 2),
            TickProfileLines.BuildHooksMissingLine(3, new[] { "Mission.OnTick call sites 0/2" }),
            // An unreadable patch info is named with the exception's message, which is arbitrary text.
            TickProfileLines.BuildHooksMissingLine(3, new[] { "Mission.OnTick transpiler (Patch97) (patch info unreadable: IOException: [Hitch] [TickProfile] in a\r\nmessage)" }),
            TickProfileLines.BuildHooksLostLine(3, 65.4, "Mission.OnTick call sites 0/2"),
            TickProfileLines.BuildStaleEndLine(4, 5),
            TickProfileLines.BuildContextReadFault("scene", new NullReferenceException("[PerfContext] broke")),
            TickProfileLines.BuildHitchCapLine(100, 412.3),
            TickProfileLines.BuildNoFramesLine(4),
            TickProfileLines.MemoryReadFailedLine,
            TickProfileLines.SettingFallbackLines(0, 8, 10, 250d)[0],
            TickProfileLines.SettingFallbackLines(0, 8, 10, 250d)[1],
        };
        var dataTags = new[] { "[TickProfile]", "[Hitch]", "[PerfContext]", "[MissionPerf]", "[TickSummary]" };

        Assert.AreEqual("[TickProfiler]", TickProfileLines.StatusTag);
        foreach (var line in lines)
        {
            StringAssert.StartsWith(line, TickProfileLines.StatusTag + " ");
            foreach (var tag in dataTags)
                Assert.IsFalse(line.Contains(tag), $"Status line carries data tag {tag}: {line}");
        }
    }
}
