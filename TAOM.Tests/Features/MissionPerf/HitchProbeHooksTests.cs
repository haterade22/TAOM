using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MissionPerf;
using TAOM.Features.MissionPerf.Hooks;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The Patch98 brackets, driven directly (they touch no engine object): each one adds its elapsed time to
/// the open frame only while a mission measures; the spawn bracket times only the outermost call and counts
/// every one; the script bracket works from any thread and names its thread once; the wait self-check
/// writes one reason line when the wait bracket never runs inside the first 30 measured pre-ticks; a
/// finalizer that runs without its prefix (PatchShield strips prefixes, never finalizers) records nothing,
/// writes one warning per method per process and is counted for the mission-end line. Each enter hands its
/// finalizer a <see cref="ProbeState"/>, the way Harmony's <c>__state</c> does; <c>HitchProbePrefixGuardTests</c>
/// runs the same hooks through real Harmony.
/// </summary>
[TestClass]
public class HitchProbeHooksTests
{
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        HitchProbeHooks.ResetForTests();
        MissionTickProfilerHooks.ResetForTests();
        _logger = Substitute.For<IModLogger>();
        MissionTickProfilerHooks.Profiler = new MissionTickProfiler(Stopwatch.Frequency);
        MissionTickProfilerHooks.Logger = _logger;
        MissionTickProfilerHooks.Profiler.BeginMission(Stopwatch.GetTimestamp(), Environment.CurrentManagedThreadId,
            measuring: true, hitchThresholdMs: 1000, behaviorTiming: false);
    }

    [TestCleanup]
    public void Cleanup()
    {
        HitchProbeHooks.ResetForTests();
        MissionTickProfilerHooks.ResetForTests();
    }

    private static MissionTickProfiler Profiler => MissionTickProfilerHooks.Profiler!;

    private static void Boundary() => Profiler.CloseFrame(Stopwatch.GetTimestamp(), 0, 0, 0, 0);

    private string[] Lines(string level) => _logger.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == level)
        .Select(c => (string)c.GetArguments()[0]!)
        .ToArray();

    private static void OnThread(Action action)
    {
        var worker = new Thread(() => action());
        worker.Start();
        worker.Join();
    }

    private static void PreTickFrame()
    {
        HitchProbeHooks.OnPreTickEnter(out var preTick);
        HitchProbeHooks.OnPreTickExit(ref preTick);
    }

    [TestMethod]
    public void PreTickBracket_Measuring_AddsPreTickAllToTheOpenFrame()
    {
        Boundary();
        HitchProbeHooks.OnPreTickEnter(out var preTick);
        Thread.Sleep(10);
        HitchProbeHooks.OnPreTickExit(ref preTick);
        Boundary();

        Assert.IsTrue(Profiler.SummarizeExtras(8).PreTickAllMs >= 8d);
    }

    [TestMethod]
    public void PreTickEnter_SamplerOn_MarksTheOpenFrame()
    {
        var adapter = Substitute.For<IAnimationLoadingAdapter>();
        adapter.IsAnyAnimationLoadingFromDisk().Returns(true);
        long now = 0;
        var sampler = new AnimLoadingSampler(adapter, () => now, 1_000_000);
        Assert.IsNotNull(sampler.MeasureCost());
        Assert.IsTrue(sampler.Enabled);
        HitchProbeHooks.Sampler = sampler;
        Profiler.AnimSampling = true;

        Boundary();
        PreTickFrame();
        Boundary();

        var w = Profiler.TakeExtrasWindow(8);
        Assert.AreEqual(1, w.LoadingFrames);
        Assert.AreEqual(1, w.Frames);
    }

    [TestMethod]
    public void WaitBracket_ProbeMode_AddsWait()
    {
        Boundary();
        HitchProbeHooks.OnPreTickEnter(out var preTick);
        HitchProbeHooks.OnWaitEnter(out var wait);
        Thread.Sleep(10);
        HitchProbeHooks.OnWaitExit(ref wait);
        HitchProbeHooks.OnPreTickExit(ref preTick);
        Boundary();

        Assert.IsTrue(Profiler.TakeWindow(8).WaitTickMs >= 8d);
    }

    [TestMethod]
    public void WaitBracket_WaitSwapActive_AddsNothing()
    {
        HitchProbeHooks.WaitSwapActive = true;
        Boundary();
        HitchProbeHooks.OnWaitEnter(out var wait);
        Thread.Sleep(5);
        HitchProbeHooks.OnWaitExit(ref wait);
        Boundary();

        Assert.AreEqual(0d, Profiler.TakeWindow(8).WaitTickMs);
    }

    [TestMethod]
    public void WaitNeverSeen_ThirtyFrames_LogsTheReasonLineOnce()
    {
        for (var i = 0; i < 31; i++)
            PreTickFrame();

        Assert.AreEqual(HitchProbeLines.WaitUnseenLine, Lines(nameof(IModLogger.LogWarning)).Single());
    }

    [TestMethod]
    public void PreTickFault_TurnsTheClipSampleOff_SoLaterMissionsReadNaNotZero()
    {
        var adapter = Substitute.For<IAnimationLoadingAdapter>();
        var sampler = new AnimLoadingSampler(adapter, Stopwatch.GetTimestamp, Stopwatch.Frequency);
        sampler.MeasureCost();
        Assert.IsTrue(sampler.Enabled);
        HitchProbeHooks.Sampler = sampler;
        Profiler.AnimSampling = true;
        _logger.When(l => l.LogWarning(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log broke"));

        for (var i = 0; i < 30; i++)
            PreTickFrame();

        Assert.IsFalse(Profiler.AnimSampling, "The pre-tick bracket is off, so no frame is sampled any more.");
        Profiler.BeginMission(Stopwatch.GetTimestamp(), Environment.CurrentManagedThreadId, measuring: true,
            hitchThresholdMs: 1000, behaviorTiming: false);
        MissionTickProfilerHooks.ConfigureProbeMission(measuring: true, behaviorTiming: false);
        Assert.IsFalse(Profiler.AnimSampling, "A later mission must not claim a clip sample the dead bracket never takes.");
        StringAssert.StartsWith(Lines(nameof(IModLogger.LogError)).Single(), "[TickProfiler] pre-tick ");
    }

    [TestMethod]
    public void SpawnBracket_Nested_TimesOnlyTheOutermost_AndCountsBoth()
    {
        Boundary();
        var outer = Stopwatch.StartNew();
        HitchProbeHooks.OnSpawnEnter(out var outerSpawn);
        Thread.Sleep(10);
        HitchProbeHooks.OnSpawnEnter(out var innerSpawn);
        Thread.Sleep(10);
        HitchProbeHooks.OnSpawnExit(ref innerSpawn);
        HitchProbeHooks.OnSpawnExit(ref outerSpawn);
        outer.Stop();
        Boundary();

        var w = Profiler.TakeExtrasWindow(8);
        Assert.AreEqual(2, w.Spawns);
        // Bounded by the test's own clock around the outer call, not a fixed 40 ms: Thread.Sleep rounds up to the
        // timer tick, so a fixed bound flakes on a busy runner; adding the inner call again would exceed it.
        Assert.IsTrue(w.SpawnMs >= 18d && w.SpawnMs <= outer.Elapsed.TotalMilliseconds + 0.5d,
            "The outer bracket only, not the inner added again: " + w.SpawnMs + " ms of " + outer.Elapsed.TotalMilliseconds);
    }

    [TestMethod]
    public void SpawnBracket_OffMainThread_CountsWithoutTiming_AndLogsOnce()
    {
        Boundary();
        OnThread(() =>
        {
            HitchProbeHooks.OnSpawnEnter(out var first);
            HitchProbeHooks.OnSpawnExit(ref first);
            HitchProbeHooks.OnSpawnEnter(out var second);
            HitchProbeHooks.OnSpawnExit(ref second);
        });
        Boundary();

        Assert.AreEqual(1, Lines(nameof(IModLogger.LogWarning)).Count(l => l.StartsWith("[TickProfiler] Mission.SpawnAgent ran off the main thread")));
        Assert.AreEqual(1, Lines(nameof(IModLogger.LogWarning)).Length);
        var w = Profiler.TakeExtrasWindow(8);
        Assert.AreEqual(0, w.Spawns);
        Assert.AreEqual(0d, w.SpawnMs);
        Assert.AreEqual(2, Profiler.SummarizeExtras(8).OffMainSpawns);
    }

    [TestMethod]
    public void SpawnExit_WithoutEnter_RecordsNothing_AndWarnsOnce()
    {
        Boundary();
        var none = ProbeState.PrefixMissing;
        HitchProbeHooks.OnSpawnExit(ref none);
        HitchProbeHooks.OnSpawnExit(ref none);
        Boundary();

        var w = Profiler.TakeExtrasWindow(8);
        Assert.AreEqual(0, w.Spawns);
        Assert.AreEqual(0d, w.SpawnMs);
        Assert.AreEqual(HitchProbeLines.SpawnPrefixMissingLine, Lines(nameof(IModLogger.LogWarning)).Single());
        Assert.AreEqual(1, _logger.ReceivedCalls().Count());
    }

    [TestMethod]
    public void SpawnExit_WithoutEnter_WhileAnOuterSpawnIsOpen_LeavesTheOuterCallItsOwnTiming()
    {
        // A nested call whose prefix was stripped: its exit is the lone one and must not close the outer call.
        Boundary();
        HitchProbeHooks.OnSpawnEnter(out var outer);
        var none = ProbeState.PrefixMissing;
        HitchProbeHooks.OnSpawnExit(ref none);
        Thread.Sleep(10);
        HitchProbeHooks.OnSpawnExit(ref outer);
        Boundary();

        Assert.AreEqual(HitchProbeLines.SpawnPrefixMissingLine, Lines(nameof(IModLogger.LogWarning)).Single());
        var w = Profiler.TakeExtrasWindow(8);
        Assert.AreEqual(1, w.Spawns);
        Assert.IsTrue(w.SpawnMs >= 8d, "The outer call closed at its own exit, after the sleep, not at the lone one: " + w.SpawnMs + " ms");
    }

    [TestMethod]
    public void ScriptBracket_OffMainThread_AddsTotals_AndLogsTheThreadLineOnce()
    {
        var mainId = Environment.CurrentManagedThreadId;
        var workerId = 0;
        Boundary();
        OnThread(() =>
        {
            workerId = Environment.CurrentManagedThreadId;
            HitchProbeHooks.OnScriptTickEnter(out var first);
            HitchProbeHooks.OnScriptTickExit(ref first);
            HitchProbeHooks.OnScriptTickEnter(out var second);
            HitchProbeHooks.OnScriptTickExit(ref second);
        });
        Boundary();

        Assert.AreEqual(HitchProbeLines.BuildScriptThreadLine(false, workerId, mainId), Lines(nameof(IModLogger.LogWarning)).Single());
        Assert.IsTrue(HitchProbeHooks.ScriptOffMain);
        Assert.AreEqual(2, Profiler.TakeExtrasWindow(8).ScriptCalls);
    }

    [TestMethod]
    public void ScriptBracket_MainThread_LogsTheThreadLineOnce()
    {
        var mainId = Environment.CurrentManagedThreadId;
        Boundary();
        HitchProbeHooks.OnScriptTickEnter(out var first);
        HitchProbeHooks.OnScriptTickExit(ref first);
        HitchProbeHooks.OnScriptTickEnter(out var second);
        HitchProbeHooks.OnScriptTickExit(ref second);
        Boundary();

        Assert.AreEqual(HitchProbeLines.BuildScriptThreadLine(true, mainId, mainId), Lines(nameof(IModLogger.LogInfo)).Single());
        Assert.IsFalse(HitchProbeHooks.ScriptOffMain);
        Assert.AreEqual(2, Profiler.TakeExtrasWindow(8).ScriptCalls);
    }

    [TestMethod]
    public void ScriptBracket_TwoThreadsOverlapping_EachRecordsItsOwnCallAndItsOwnTime()
    {
        // native may call TickComponents from several threads at once: the open flag and start stamp are per thread.
        Boundary();
        HitchProbeHooks.OnScriptTickEnter(out var main);
        Thread.Sleep(20);
        OnThread(() =>
        {
            HitchProbeHooks.OnScriptTickEnter(out var worker);
            HitchProbeHooks.OnScriptTickExit(ref worker);
        });
        HitchProbeHooks.OnScriptTickExit(ref main);
        Boundary();

        var w = Profiler.TakeExtrasWindow(8);
        Assert.AreEqual(2, w.ScriptCalls, "The worker's exit did not close the main thread's call.");
        Assert.IsTrue(w.ScriptTickMs >= 18d, "The main thread's call is timed from its own enter, not the worker's: " + w.ScriptTickMs + " ms");
    }

    [TestMethod]
    public void OnTickBracket_Measuring_AddsOnTick()
    {
        Boundary();
        HitchProbeHooks.OnTickEnter(out var onTick);
        Thread.Sleep(10);
        HitchProbeHooks.OnTickExit(ref onTick);
        Boundary();

        Assert.IsTrue(Profiler.SummarizeExtras(8).OnTickMs >= 8d);
    }

    [TestMethod]
    public void Brackets_NotMeasuring_RecordNothing()
    {
        Profiler.BeginMission(Stopwatch.GetTimestamp(), Environment.CurrentManagedThreadId, measuring: false,
            hitchThresholdMs: 1000, behaviorTiming: false);
        Boundary();
        HitchProbeHooks.OnPreTickEnter(out var preTick);
        HitchProbeHooks.OnWaitEnter(out var wait);
        HitchProbeHooks.OnWaitExit(ref wait);
        HitchProbeHooks.OnPreTickExit(ref preTick);
        HitchProbeHooks.OnTickEnter(out var onTick);
        HitchProbeHooks.OnTickExit(ref onTick);
        HitchProbeHooks.OnSpawnEnter(out var spawn);
        HitchProbeHooks.OnSpawnExit(ref spawn);
        HitchProbeHooks.OnScriptTickEnter(out var script);
        HitchProbeHooks.OnScriptTickExit(ref script);
        Boundary();

        var m = Profiler.SummarizeExtras(8);
        Assert.AreEqual(0, m.Spawns);
        Assert.AreEqual(0d, m.OnTickMs);
        Assert.AreEqual(0d, m.PreTickAllMs);
        Assert.AreEqual(0d, m.ScriptTickMs);
        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
    }

    // --- A finalizer that runs with no prefix before it. PatchShield strips an owner's prefixes, postfixes and
    // transpilers after a missing-API exception and never its finalizers, and a prefix before ours that throws
    // stops ours from running, so the five exits can run on their own (D13 shields Mission.OnTick, OnPreTick and
    // SpawnAgent again, and `com.taom.mod` is not a protected owner). The exit tells by the state its call's
    // enter left, which Harmony starts at PrefixMissing in every call. ------------------------------------------

    private delegate void ExitHook(ref ProbeState state);

    private static void ExitEveryBracketWithoutItsPrefix()
    {
        var none = ProbeState.PrefixMissing;
        HitchProbeHooks.OnPreTickExit(ref none);
        HitchProbeHooks.OnWaitExit(ref none);
        HitchProbeHooks.OnTickExit(ref none);
        HitchProbeHooks.OnScriptTickExit(ref none);
        HitchProbeHooks.OnSpawnExit(ref none);
    }

    private static int _sink;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DummyOnTick() => _sink++;

    [TestMethod]
    public void Exits_WithoutTheirPrefix_RecordNothing_AndThrowNothing()
    {
        Boundary();
        ExitEveryBracketWithoutItsPrefix();
        Boundary();

        var m = Profiler.SummarizeExtras(8);
        Assert.AreEqual(0d, m.PreTickAllMs, "pre-tick");
        Assert.AreEqual(0d, m.OnTickMs, "on-tick");
        Assert.AreEqual(0d, m.ScriptTickMs, "script tick");
        Assert.AreEqual(0d, m.SpawnMs, "spawn time");
        Assert.AreEqual(0, m.Spawns, "spawn count");
        Assert.AreEqual(0d, Profiler.TakeWindow(8).WaitTickMs, "wait");
        Assert.AreEqual(0, Profiler.TakeExtrasWindow(8).ScriptCalls, "script calls");
    }

    [TestMethod]
    public void Exits_WithoutTheirPrefix_WarnOncePerMethodPerProcess_EachWithItsOwnLine()
    {
        var exitsAndLines = new (ExitHook Exit, string Line)[]
        {
            (HitchProbeHooks.OnPreTickExit, HitchProbeLines.PreTickPrefixMissingLine),
            (HitchProbeHooks.OnWaitExit, HitchProbeLines.WaitPrefixMissingLine),
            (HitchProbeHooks.OnTickExit, HitchProbeLines.OnTickPrefixMissingLine),
            (HitchProbeHooks.OnScriptTickExit, HitchProbeLines.ScriptPrefixMissingLine),
            (HitchProbeHooks.OnSpawnExit, HitchProbeLines.SpawnPrefixMissingLine),
        };

        foreach (var (exit, _) in exitsAndLines)
        {
            var none = ProbeState.PrefixMissing;
            exit(ref none);
            exit(ref none);
            exit(ref none);
        }

        // In call order, so a line wired to the wrong exit fails here.
        CollectionAssert.AreEqual(exitsAndLines.Select(e => e.Line).ToArray(), Lines(nameof(IModLogger.LogWarning)));
        Assert.AreEqual(exitsAndLines.Length, _logger.ReceivedCalls().Count(), "Only the five warnings, no other level.");
    }

    [TestMethod]
    public void Exits_WithoutTheirPrefix_AreCountedEveryTime_AndTakenOnce()
    {
        var none = ProbeState.PrefixMissing;
        for (var i = 0; i < 3; i++)
            HitchProbeHooks.OnWaitExit(ref none);
        HitchProbeHooks.OnTickExit(ref none);
        for (var i = 0; i < 2; i++)
            HitchProbeHooks.OnSpawnExit(ref none);

        CollectionAssert.AreEqual(new[] { 0, 3, 1, 0, 2 }, HitchProbeHooks.TakePrefixMissingCalls(),
            "Every lone exit is counted, the first one that warns included, in bracket order.");
        Assert.IsNull(HitchProbeHooks.TakePrefixMissingCalls(), "Taking the counts drains them.");
    }

    [TestMethod]
    public void Exits_WithTheirPrefix_CountNothingMissing()
    {
        HitchProbeHooks.OnTickEnter(out var onTick);
        HitchProbeHooks.OnTickExit(ref onTick);
        HitchProbeHooks.OnSpawnEnter(out var spawn);
        HitchProbeHooks.OnSpawnExit(ref spawn);

        Assert.IsNull(HitchProbeHooks.TakePrefixMissingCalls());
    }

    [TestMethod]
    public void Exit_RunAgainForTheSameCall_RecordsItOnce_AndIsNeitherCountedNorWarned()
    {
        // Harmony reruns every finalizer for one call when a later finalizer throws on the normal path.
        Boundary();
        var wall = Stopwatch.StartNew();
        HitchProbeHooks.OnTickEnter(out var onTick);
        Thread.Sleep(10);
        HitchProbeHooks.OnTickExit(ref onTick);
        HitchProbeHooks.OnTickExit(ref onTick);
        wall.Stop();
        Boundary();

        var ms = Profiler.SummarizeExtras(8).OnTickMs;
        Assert.IsTrue(ms >= 8d && ms <= wall.Elapsed.TotalMilliseconds + 0.5d, "Recorded once, not twice: " + ms + " ms of " + wall.Elapsed.TotalMilliseconds);
        Assert.AreEqual(0, _logger.ReceivedCalls().Count(), "The second run is a rerun, not a missing prefix.");
        Assert.IsNull(HitchProbeHooks.TakePrefixMissingCalls());
    }

    [TestMethod]
    public void ExitWithoutPrefix_LoggerThrows_ThrowsNothing_AndTriesTheLineOnce()
    {
        _logger.When(l => l.LogWarning(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log broke"));

        var none = ProbeState.PrefixMissing;
        HitchProbeHooks.OnTickExit(ref none);
        HitchProbeHooks.OnTickExit(ref none);

        Assert.AreEqual(1, _logger.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IModLogger.LogWarning)));
    }

    [TestMethod]
    public void Exits_AfterEntersThatSawNoMeasuringMission_NeitherRecordNorWarn()
    {
        // A mission starts measuring (BeginMission) while earlier-entered calls are still in flight: the enters
        // returned early, the exits find the profiler measuring. That is not a missing prefix.
        Profiler.BeginMission(Stopwatch.GetTimestamp(), Environment.CurrentManagedThreadId, measuring: false,
            hitchThresholdMs: 1000, behaviorTiming: false);
        HitchProbeHooks.OnPreTickEnter(out var preTick);
        HitchProbeHooks.OnWaitEnter(out var wait);
        HitchProbeHooks.OnTickEnter(out var onTick);
        HitchProbeHooks.OnScriptTickEnter(out var script);
        HitchProbeHooks.OnSpawnEnter(out var spawn);
        Profiler.BeginMission(Stopwatch.GetTimestamp(), Environment.CurrentManagedThreadId, measuring: true,
            hitchThresholdMs: 1000, behaviorTiming: false);
        Boundary();

        HitchProbeHooks.OnSpawnExit(ref spawn);
        HitchProbeHooks.OnScriptTickExit(ref script);
        HitchProbeHooks.OnTickExit(ref onTick);
        HitchProbeHooks.OnWaitExit(ref wait);
        HitchProbeHooks.OnPreTickExit(ref preTick);
        Boundary();

        var m = Profiler.SummarizeExtras(8);
        Assert.AreEqual(0d, m.PreTickAllMs, "pre-tick");
        Assert.AreEqual(0d, m.OnTickMs, "on-tick");
        Assert.AreEqual(0d, m.ScriptTickMs, "script tick");
        Assert.AreEqual(0, m.Spawns, "spawn count");
        Assert.AreEqual(0d, Profiler.TakeWindow(8).WaitTickMs, "wait");
        Assert.AreEqual(0, _logger.ReceivedCalls().Count(), "Every exit had its enter: no warning.");
        Assert.IsNull(HitchProbeHooks.TakePrefixMissingCalls());
    }

    [TestMethod]
    public void PreTickPair_AfterTheBracketFaultedOff_WritesNothingMore()
    {
        // A part turned off by a fault returns from its enter early; its exit is still a paired exit, not a
        // missing prefix. The 30th pair's wait-unseen warning throws (the logger breaks), which turns the part off.
        _logger.When(l => l.LogWarning(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log broke"));
        for (var i = 0; i < 30; i++)
            PreTickFrame();
        StringAssert.StartsWith(Lines(nameof(IModLogger.LogError)).Single(), "[TickProfiler] pre-tick ");
        var linesSoFar = _logger.ReceivedCalls().Count();

        PreTickFrame();

        Assert.AreEqual(linesSoFar, _logger.ReceivedCalls().Count(), "An off part returns from both halves without a line.");
    }

    [TestMethod]
    public void SpawnExit_OnAThreadWithNoEnter_WarnsAtOnce_AndLeavesTheMainThreadsSpawnItsTiming()
    {
        Boundary();
        HitchProbeHooks.OnSpawnEnter(out var main);
        Thread.Sleep(10);
        OnThread(() =>
        {
            var none = ProbeState.PrefixMissing;
            HitchProbeHooks.OnSpawnExit(ref none);
        });
        Assert.AreEqual(HitchProbeLines.SpawnPrefixMissingLine, Lines(nameof(IModLogger.LogWarning)).Single(),
            "The worker's exit is the one with no enter: it warns at once and takes nothing from the main thread's.");

        Thread.Sleep(10);
        HitchProbeHooks.OnSpawnExit(ref main);
        Boundary();

        Assert.AreEqual(1, Lines(nameof(IModLogger.LogWarning)).Length, "The main thread's own exit found its enter.");
        var w = Profiler.TakeExtrasWindow(8);
        Assert.AreEqual(1, w.Spawns);
        Assert.IsTrue(w.SpawnMs >= 18d, "The main thread's spawn is closed and timed whole: " + w.SpawnMs + " ms");
    }

    [TestMethod]
    public void ScriptExit_OnAThreadWithNoEnter_WarnsAtOnce_AndLeavesTheMainThreadsCallItsCount()
    {
        Boundary();
        HitchProbeHooks.OnScriptTickEnter(out var main);
        OnThread(() =>
        {
            var none = ProbeState.PrefixMissing;
            HitchProbeHooks.OnScriptTickExit(ref none);
        });
        Assert.AreEqual(HitchProbeLines.ScriptPrefixMissingLine, Lines(nameof(IModLogger.LogWarning)).Single(),
            "The worker's exit is the one with no enter: it warns at once and takes nothing from the main thread's.");

        HitchProbeHooks.OnScriptTickExit(ref main);
        Boundary();

        Assert.AreEqual(1, Lines(nameof(IModLogger.LogWarning)).Length, "The main thread's own exit found its enter.");
        Assert.AreEqual(1, Profiler.TakeExtrasWindow(8).ScriptCalls);
    }

    [TestMethod]
    public void StrippedPrefix_FinalizerRunsAlone_WarnsOnceAndRecordsNothing()
    {
        // PatchShield's strip, through real Harmony: the owner's prefix goes, the finalizer stays.
        const string id = "taom.tests.hitchprobe.stripped";
        var harmony = new Harmony(id);
        try
        {
            var target = typeof(HitchProbeHooksTests).GetMethod(nameof(DummyOnTick), BindingFlags.NonPublic | BindingFlags.Static)!;
            var patch = typeof(Mission_OnTick_HitchProbe_Patch);
            harmony.Patch(target, prefix: new HarmonyMethod(patch.GetMethod("Prefix")), finalizer: new HarmonyMethod(patch.GetMethod("Finalizer")));

            Boundary();
            DummyOnTick();
            Boundary();
            var timedOnce = Profiler.SummarizeExtras(8).OnTickMs;
            Assert.AreEqual(0, _logger.ReceivedCalls().Count(), "Both halves attached: the pair is paired.");

            harmony.Unpatch(target, HarmonyPatchType.Prefix, id);
            Assert.AreEqual(0, Harmony.GetPatchInfo(target).Prefixes.Count, "The prefix is gone.");
            Assert.AreEqual(1, Harmony.GetPatchInfo(target).Finalizers.Count, "The finalizer stays: PatchShield never strips one.");
            DummyOnTick();
            DummyOnTick();
            Boundary();

            Assert.AreEqual(timedOnce, Profiler.SummarizeExtras(8).OnTickMs, "The finalizer alone adds no duration.");
            Assert.AreEqual(HitchProbeLines.OnTickPrefixMissingLine, Lines(nameof(IModLogger.LogWarning)).Single());
            Assert.AreEqual(1, _logger.ReceivedCalls().Count());
            CollectionAssert.AreEqual(new[] { 0, 0, 2, 0, 0 }, HitchProbeHooks.TakePrefixMissingCalls(),
                "Both calls with no prefix are counted, though only the first one warns.");
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }
}
