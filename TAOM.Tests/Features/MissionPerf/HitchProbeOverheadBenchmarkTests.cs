using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.MissionPerf;
using TAOM.Features.MissionPerf.Hooks;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The always-on cost of the Patch98 hitch probe, measured: five dummy methods stand in for the patched engine
/// methods, a simulated frame calls them the way a battle frame does (pre-tick with the wait inside it, on-tick,
/// the script tick, the agent-tick pair, two spawns), and the same frames run first unpatched and then with the
/// REAL Patch98 prefixes and finalizers attached by Harmony, on a probe-mode profiler with clip sampling on
/// (the unpatched arm has no profiler, as a default player had before the probe). The native clip-loading
/// call is a stub here; its own median is the <c>anim-loading sample</c> line at the first measured mission.
/// The difference per frame must stay under 0.5% of a 10 ms frame (50 us). Opt-in
/// (<c>TAOM_RUN_BENCHMARKS=1</c>), so the default run and hosted CI never time a shared runner.
/// </summary>
[TestClass]
public class HitchProbeOverheadBenchmarkTests
{
    private const int Frames = 20_000;
    private const int WarmUp = 2_000;
    private const double TargetUs = 50.0;

    private static int _sink;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DummyWait() => _sink++;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DummyPreTick()
    {
        DummyWait();
        _sink++;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DummyOnTick() => _sink++;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DummyTickComponents() => _sink++;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DummySpawn() => _sink++;

    private sealed class StubAdapter : IAnimationLoadingAdapter
    {
        public bool IsAnyAnimationLoadingFromDisk() => false;
    }

    private static void Frame()
    {
        DummyPreTick();
        DummyOnTick();
        DummyTickComponents();
        MissionTickProfilerHooks.OnAgentTickEnter();
        MissionTickProfilerHooks.OnAgentTickExit();
        DummySpawn();
        DummySpawn();
    }

    private static double BestOfThreeTicks()
    {
        var best = double.MaxValue;
        for (var run = 0; run < 3; run++)
        {
            for (var i = 0; i < WarmUp; i++)
                Frame();
            var t0 = Stopwatch.GetTimestamp();
            for (var i = 0; i < Frames; i++)
                Frame();
            best = Math.Min(best, Stopwatch.GetTimestamp() - t0);
        }
        return best;
    }

    private static MethodInfo Dummy(string name) =>
        typeof(HitchProbeOverheadBenchmarkTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void Attach(Harmony harmony, string dummy, Type patch) => harmony.Patch(Dummy(dummy),
        prefix: new HarmonyMethod(patch.GetMethod("Prefix")), finalizer: new HarmonyMethod(patch.GetMethod("Finalizer")));

    [TestMethod]
    [TestCategory("Benchmark")]
    public void HitchProbe_SimulatedFrame_CostsUnderHalfAPercentOfTenMilliseconds()
    {
        if (Environment.GetEnvironmentVariable("TAOM_RUN_BENCHMARKS") != "1") Assert.Inconclusive("Benchmark: set TAOM_RUN_BENCHMARKS=1");

        var harmony = new Harmony("taom.tests.hitchprobe.bench");
        try
        {
            HitchProbeHooks.ResetForTests();
            MissionTickProfilerHooks.ResetForTests();
            var profiler = new MissionTickProfiler(Stopwatch.Frequency);
            MissionTickProfilerHooks.Profiler = profiler;
            profiler.BeginMission(Stopwatch.GetTimestamp(), Environment.CurrentManagedThreadId, measuring: true,
                hitchThresholdMs: double.MaxValue, behaviorTiming: false);
            var sampler = new AnimLoadingSampler(new StubAdapter(), Stopwatch.GetTimestamp, Stopwatch.Frequency);
            sampler.MeasureCost();
            Assert.IsTrue(sampler.Enabled, "The stub adapter is within the sampling budget.");

            // The baseline is a default player before plan 041: no profiler, so Patch91's agent-tick pair returns at
            // once. With the probe, that pair measures too, so its cost belongs to the patched arm (review 041).
            MissionTickProfilerHooks.Profiler = null;
            var unpatched = BestOfThreeTicks();

            MissionTickProfilerHooks.Profiler = profiler;
            HitchProbeHooks.Sampler = sampler;
            profiler.AnimSampling = true;
            Attach(harmony, nameof(DummyWait), typeof(Mission_WaitTickCompletion_HitchProbe_Patch));
            Attach(harmony, nameof(DummyPreTick), typeof(Mission_OnPreTick_HitchProbe_Patch));
            Attach(harmony, nameof(DummyOnTick), typeof(Mission_OnTick_HitchProbe_Patch));
            Attach(harmony, nameof(DummyTickComponents), typeof(ManagedScriptHolder_TickComponents_HitchProbe_Patch));
            Attach(harmony, nameof(DummySpawn), typeof(Mission_SpawnAgent_HitchProbe_Patch));
            var patched = BestOfThreeTicks();

            var window = profiler.TakeExtrasWindow(8);
            Assert.IsTrue(window.Frames > 0 && window.Spawns > 0 && window.ScriptCalls > 0, "The brackets ran.");
            var usPerFrame = (patched - unpatched) * 1_000_000d / Stopwatch.Frequency / Frames;
            Console.WriteLine("HitchProbe overhead: {0:0.000} us per simulated frame (unpatched {1:0.000} us, patched {2:0.000} us, target < {3} us)",
                usPerFrame, unpatched * 1_000_000d / Stopwatch.Frequency / Frames, patched * 1_000_000d / Stopwatch.Frequency / Frames, TargetUs);
            Assert.IsTrue(usPerFrame < TargetUs, $"{usPerFrame:0.000} us per frame");
        }
        finally
        {
            harmony.UnpatchAll("taom.tests.hitchprobe.bench");
            HitchProbeHooks.ResetForTests();
            MissionTickProfilerHooks.ResetForTests();
        }
    }
}
