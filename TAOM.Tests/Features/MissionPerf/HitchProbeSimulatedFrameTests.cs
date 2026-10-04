using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.MissionPerf;
using TAOM.Features.MissionPerf.Hooks;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// One simulated battle frame through all five REAL Patch98 classes, attached by Harmony to dummy methods that
/// stand in for the engine's: the pre-tick with the wait inside it, the on-tick, the script tick and two spawns.
/// Each dummy burns a known time on the probe's own clock, so each bracket's own column must read at least that,
/// the counts must be exact, and the frame must write no warning and no error. This is the default-run check of
/// which hook each class calls: <c>HitchProbeHooksTests</c> calls every hook by hand, <c>HitchProbePrefixGuardTests</c>
/// attaches only the on-tick and spawn classes, and the overhead benchmark, the one other place that attaches the
/// wait, pre-tick and script classes, is opt-in. Without this test, a class that called the wrong hook (the script
/// finalizer calling the spawn exit, say) would pass every default test.
/// </summary>
[TestClass]
public class HitchProbeSimulatedFrameTests
{
    private const string HarmonyId = "taom.tests.hitchprobe.simulatedframe";

    // What each stand-in spends, in ms of the Stopwatch the probe reads. All different, so a column that received
    // another bracket's time does not pass by accident.
    private const int WaitMs = 1;
    private const int PreTickOwnMs = 2;
    private const int OnTickMs = 3;
    private const int ScriptMs = 4;
    private const int SpawnMs = 5;

    private IModLogger _logger = null!;
    private Harmony _harmony = null!;

    // Spins on the clock the probe stamps with, so a bracket around it reads at least this long (a sleep may return early).
    private static void Burn(int ms)
    {
        var until = Stopwatch.GetTimestamp() + ms * Stopwatch.Frequency / 1000;
        while (Stopwatch.GetTimestamp() < until) { }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DummyWait() => Burn(WaitMs);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DummyPreTick()
    {
        DummyWait();
        Burn(PreTickOwnMs);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DummyOnTick() => Burn(OnTickMs);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DummyTickComponents() => Burn(ScriptMs);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DummySpawn() => Burn(SpawnMs);

    [TestInitialize]
    public void Setup()
    {
        HitchProbeHooks.ResetForTests();
        MissionTickProfilerHooks.ResetForTests();
        _logger = Substitute.For<IModLogger>();
        MissionTickProfilerHooks.Profiler = new MissionTickProfiler(Stopwatch.Frequency);
        MissionTickProfilerHooks.Logger = _logger;
        Profiler.BeginMission(Stopwatch.GetTimestamp(), Environment.CurrentManagedThreadId,
            measuring: true, hitchThresholdMs: double.MaxValue, behaviorTiming: false);
        _harmony = new Harmony(HarmonyId);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _harmony.UnpatchAll(HarmonyId);
        HitchProbeHooks.ResetForTests();
        MissionTickProfilerHooks.ResetForTests();
    }

    private static MissionTickProfiler Profiler => MissionTickProfilerHooks.Profiler!;

    private string[] Lines(string level) => _logger.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == level)
        .Select(c => (string)c.GetArguments()[0]!)
        .ToArray();

    private void Attach(string dummy, Type patchClass) => _harmony.Patch(
        typeof(HitchProbeSimulatedFrameTests).GetMethod(dummy, BindingFlags.NonPublic | BindingFlags.Static)!,
        prefix: new HarmonyMethod(patchClass.GetMethod("Prefix")),
        finalizer: new HarmonyMethod(patchClass.GetMethod("Finalizer")));

    [TestMethod]
    public void OneSimulatedFrame_AllFiveRealBrackets_EachFillsItsOwnColumn_AndWritesNoWarning()
    {
        Attach(nameof(DummyWait), typeof(Mission_WaitTickCompletion_HitchProbe_Patch));
        Attach(nameof(DummyPreTick), typeof(Mission_OnPreTick_HitchProbe_Patch));
        Attach(nameof(DummyOnTick), typeof(Mission_OnTick_HitchProbe_Patch));
        Attach(nameof(DummyTickComponents), typeof(ManagedScriptHolder_TickComponents_HitchProbe_Patch));
        Attach(nameof(DummySpawn), typeof(Mission_SpawnAgent_HitchProbe_Patch));

        // The real pre-tick prefix runs the frame boundary: this call's only stamps the clock. The boundary after
        // the frame is what the next frame's pre-tick prefix would run, and it closes this one.
        DummyPreTick();
        DummyOnTick();
        DummyTickComponents();
        DummySpawn();
        DummySpawn();
        MissionTickProfilerHooks.OnFrameBoundary();

        var mission = Profiler.Summarize(8);
        var extras = Profiler.SummarizeExtras(8);
        var window = Profiler.TakeExtrasWindow(8);
        Assert.AreEqual(1, mission.Frames, "The pre-tick prefix's boundary stamped, the one after the frame closed it.");
        Assert.IsTrue(mission.WaitTickMs >= WaitMs, "wait bracket, waitTickMs: " + mission.WaitTickMs);
        Assert.IsTrue(extras.PreTickAllMs >= WaitMs + PreTickOwnMs, "pre-tick bracket, preTickAllMs: " + extras.PreTickAllMs);
        Assert.IsTrue(extras.OnTickMs >= OnTickMs, "on-tick bracket, onTickMs: " + extras.OnTickMs);
        Assert.AreEqual(1, window.ScriptCalls, "script bracket, calls");
        Assert.IsTrue(window.ScriptTickMs >= ScriptMs, "script bracket, scriptTickMs: " + window.ScriptTickMs);
        Assert.AreEqual(2, window.Spawns, "spawn bracket, spawns");
        Assert.IsTrue(window.SpawnMs >= 2 * SpawnMs, "spawn bracket, spawnMs: " + window.SpawnMs);
        var problems = Lines(nameof(IModLogger.LogWarning)).Concat(Lines(nameof(IModLogger.LogError))).ToArray();
        Assert.AreEqual(0, problems.Length, string.Join("\n", problems));
    }
}
