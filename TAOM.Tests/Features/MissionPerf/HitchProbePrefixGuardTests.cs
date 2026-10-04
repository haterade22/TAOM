using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.MissionPerf;
using TAOM.Features.MissionPerf.Hooks;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The probe's finalizer guard against real Harmony, with the real Patch98 prefix and finalizer attached to dummy
/// methods, because the guard's whole job is the cases a hand-called hook cannot reach: a prefix stripped while an
/// outer call of the same method is still running (PatchShield strips from inside the call that threw), a later
/// finalizer that throws (Harmony then reruns every finalizer for the same call), and the two ways another patch
/// can stop our prefix from running (a prefix before ours that throws, or returns false).
/// </summary>
[TestClass]
public class HitchProbePrefixGuardTests
{
    private const string HarmonyId = "taom.tests.hitchprobe.guard";

    private IModLogger _logger = null!;
    private Harmony _harmony = null!;

    private static int _sink;
    private static Action<int>? _inside;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DummySpawn(int depth)
    {
        _sink++;
        _inside?.Invoke(depth);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DummyOnTick()
    {
        _sink++;
        Thread.Sleep(5);
    }

    private static void ThrowingFinalizer() => throw new InvalidOperationException("a finalizer after the probe's");

    private static void ThrowingPrefix() => throw new InvalidOperationException("a prefix before the probe's");

    private static bool SkipOriginalPrefix() => false;

    [TestInitialize]
    public void Setup()
    {
        HitchProbeHooks.ResetForTests();
        MissionTickProfilerHooks.ResetForTests();
        _logger = Substitute.For<IModLogger>();
        MissionTickProfilerHooks.Profiler = new MissionTickProfiler(Stopwatch.Frequency);
        MissionTickProfilerHooks.Logger = _logger;
        Profiler.BeginMission(Stopwatch.GetTimestamp(), Environment.CurrentManagedThreadId,
            measuring: true, hitchThresholdMs: 1000, behaviorTiming: false);
        _harmony = new Harmony(HarmonyId);
        _inside = null;
    }

    [TestCleanup]
    public void Cleanup()
    {
        _harmony.UnpatchAll(HarmonyId);
        _inside = null;
        HitchProbeHooks.ResetForTests();
        MissionTickProfilerHooks.ResetForTests();
    }

    private static MissionTickProfiler Profiler => MissionTickProfilerHooks.Profiler!;

    private static void Boundary() => Profiler.CloseFrame(Stopwatch.GetTimestamp(), 0, 0, 0, 0);

    private string[] Warnings() => _logger.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogWarning))
        .Select(c => (string)c.GetArguments()[0]!)
        .ToArray();

    private static MethodInfo Own(string name) =>
        typeof(HitchProbePrefixGuardTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    private static HarmonyMethod Fix(Type patchClass, string name) => new HarmonyMethod(patchClass.GetMethod(name));

    /// <summary>Attaches the real probe prefix and finalizer of <paramref name="patchClass"/> to the dummy.</summary>
    private MethodInfo AttachProbe(string dummy, Type patchClass)
    {
        var target = Own(dummy);
        _harmony.Patch(target, prefix: Fix(patchClass, "Prefix"), finalizer: Fix(patchClass, "Finalizer"));
        return target;
    }

    [TestMethod]
    public void PrefixStrippedInsideAnOuterSpawn_TheLaterNestedCallsLoneFinalizer_LeavesTheOuterCallItsOwnTiming()
    {
        var target = AttachProbe(nameof(DummySpawn), typeof(Mission_SpawnAgent_HitchProbe_Patch));
        var outer = new Stopwatch();
        var warningsAfterNested = -1;
        var nestedDoneMs = 0d;
        _inside = depth =>
        {
            if (depth != 0)
                return;
            // PatchShield strips from inside the call that threw, so the outer call keeps running its old
            // replacement while every later call runs a replacement with no probe prefix.
            _harmony.Unpatch(target, HarmonyPatchType.Prefix, HarmonyId);
            Thread.Sleep(10);
            DummySpawn(1);
            nestedDoneMs = outer.Elapsed.TotalMilliseconds;
            warningsAfterNested = Warnings().Length;
            Thread.Sleep(25);
        };

        Boundary();
        outer.Start();
        DummySpawn(0);
        outer.Stop();
        Boundary();

        Assert.AreEqual(1, warningsAfterNested, "The nested call has no prefix: its finalizer is the one that warns, at once.");
        Assert.AreEqual(HitchProbeLines.SpawnPrefixMissingLine, Warnings().Single());
        var w = Profiler.TakeExtrasWindow(8);
        Assert.AreEqual(1, w.Spawns, "Only the outer call's prefix ran.");
        Assert.IsTrue(w.SpawnMs >= nestedDoneMs + 20d && w.SpawnMs <= outer.Elapsed.TotalMilliseconds + 0.5d,
            "The outer call is timed whole, by its own finalizer, past the nested call that ended at " + nestedDoneMs
            + " ms: " + w.SpawnMs + " ms of " + outer.Elapsed.TotalMilliseconds);
    }

    [TestMethod]
    public void LaterFinalizerThatThrows_HarmonyRerunsEveryFinalizer_ButTheProbeClosesTheCallOnce()
    {
        var patch = typeof(Mission_OnTick_HitchProbe_Patch);
        var target = AttachProbe(nameof(DummyOnTick), patch);
        _harmony.Patch(target, finalizer: new HarmonyMethod(Own(nameof(ThrowingFinalizer))) { priority = Priority.Last });
        var finalizers = Harmony.GetPatchInfo(target).Finalizers;
        Assert.AreEqual(2, finalizers.Count);
        Assert.AreEqual(patch.GetMethod("Finalizer"), finalizers[0].PatchMethod, "The probe's finalizer sorts first, the thrower after it.");

        Boundary();
        Assert.ThrowsException<InvalidOperationException>(() => DummyOnTick());
        Boundary();

        Assert.IsTrue(Profiler.SummarizeExtras(8).OnTickMs >= 4d, "The first run of the probe's finalizer recorded the call.");
        Assert.AreEqual(0, _logger.ReceivedCalls().Count(),
            "Harmony reran the probe's finalizer for the same call: that is not a missing prefix.");
    }

    [TestMethod]
    public void FinalizersRerunForANestedSpawn_DoNotCloseTheOuterSpawnEarly()
    {
        // The nested call's finalizers run twice (a later one throws): the second run must not take the outer
        // call's depth. The outer call's own finalizer then closes it, after both sleeps.
        var target = AttachProbe(nameof(DummySpawn), typeof(Mission_SpawnAgent_HitchProbe_Patch));
        _harmony.Patch(target, finalizer: new HarmonyMethod(Own(nameof(ThrowingFinalizer))) { priority = Priority.Last });
        _inside = depth =>
        {
            if (depth != 0)
                return;
            Thread.Sleep(10);
            Assert.ThrowsException<InvalidOperationException>(() => DummySpawn(1));
            Thread.Sleep(10);
        };

        Boundary();
        var outer = Stopwatch.StartNew();
        Assert.ThrowsException<InvalidOperationException>(() => DummySpawn(0));
        outer.Stop();
        Boundary();

        var w = Profiler.TakeExtrasWindow(8);
        Assert.AreEqual(2, w.Spawns);
        Assert.IsTrue(w.SpawnMs >= 18d && w.SpawnMs <= outer.Elapsed.TotalMilliseconds + 0.5d,
            "The outer call is timed whole: " + w.SpawnMs + " ms of " + outer.Elapsed.TotalMilliseconds);
        Assert.AreEqual(0, _logger.ReceivedCalls().Count(), "Neither run of either call's finalizer is a missing prefix.");
    }

    [TestMethod]
    public void PrefixBeforeOursThatReturnsFalse_DoesNotSkipTheProbePrefix()
    {
        var target = AttachProbe(nameof(DummyOnTick), typeof(Mission_OnTick_HitchProbe_Patch));
        _harmony.Patch(target, prefix: new HarmonyMethod(Own(nameof(SkipOriginalPrefix))) { priority = Priority.First + 1 });
        var before = _sink;

        DummyOnTick();

        Assert.AreEqual(before, _sink, "The earlier prefix skipped the original.");
        Assert.AreEqual(0, _logger.ReceivedCalls().Count(), "Our void prefix still ran, so its finalizer found its pair.");
    }

    [TestMethod]
    public void PrefixBeforeOursThatThrows_LeavesTheFinalizerAlone_AndWarnsOnce()
    {
        var target = AttachProbe(nameof(DummyOnTick), typeof(Mission_OnTick_HitchProbe_Patch));
        _harmony.Patch(target, prefix: new HarmonyMethod(Own(nameof(ThrowingPrefix))) { priority = Priority.First + 1 });
        Assert.AreEqual(2, Harmony.GetPatchInfo(target).Prefixes.Count);

        Assert.ThrowsException<InvalidOperationException>(() => DummyOnTick());
        Assert.ThrowsException<InvalidOperationException>(() => DummyOnTick());

        Assert.AreEqual(HitchProbeLines.OnTickPrefixMissingLine, Warnings().Single());
    }
}
