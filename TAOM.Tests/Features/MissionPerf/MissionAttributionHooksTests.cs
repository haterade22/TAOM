using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.MissionPerf;
using TAOM.Features.MissionPerf.Hooks;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The full-mode attribution helpers the Patch97 transpilers swap into <c>Mission.SpawnAgent</c> and
/// <c>ManagedScriptHolder.TickComponents</c>: each calls the engine method exactly once, records by type (or
/// into the block totals) only while the mission attributes, and lets the engine call's exception through
/// unchanged. Drives a real <see cref="MissionLogic"/> subclass and <see cref="TWParallel"/> (a range below
/// its grain runs inline, but its type initialiser is engine code), so it needs the game assemblies.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class MissionAttributionHooksTests
{
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        MissionAttributionInstaller.ResetForTests();
        HitchProbeHooks.ResetForTests();
        MissionTickProfilerHooks.ResetForTests();
        _logger = Substitute.For<IModLogger>();
        MissionTickProfilerHooks.Logger = _logger;
        MissionTickProfilerHooks.Profiler = new MissionTickProfiler(Stopwatch.Frequency);
        Profiler.BeginMission(Stopwatch.GetTimestamp(), Environment.CurrentManagedThreadId, measuring: true,
            hitchThresholdMs: 100000, behaviorTiming: true);
        Profiler.SpawnAttribution = true;
        Profiler.ScriptAttribution = true;
        Profiler.ScriptBlockTiming = true;
        Boundary();
    }

    [TestCleanup]
    public void Cleanup()
    {
        MissionAttributionInstaller.ResetForTests();
        HitchProbeHooks.ResetForTests();
        MissionTickProfilerHooks.ResetForTests();
    }

    private static MissionTickProfiler Profiler => MissionTickProfilerHooks.Profiler!;

    private static void Boundary() => Profiler.CloseFrame(Stopwatch.GetTimestamp(), 0, 0, 0, 0);

    [TestMethod]
    public void TimedAgentBuild_Attributing_CallsOnAgentBuildOnce_AndRecordsBySpawnType()
    {
        var probe = new ProbeBehavior();

        MissionAttributionHooks.TimedAgentBuild(probe, null!, null!);
        Boundary();

        Assert.AreEqual(1, probe.Builds);
        var top = Profiler.TakeExtrasWindow(8).SpawnTop.Single();
        Assert.AreEqual(nameof(ProbeBehavior), top.Name);
        Assert.AreEqual(1, top.Calls);
    }

    [TestMethod]
    public void TimedAgentBuild_NotAttributing_CallsThroughAndRecordsNothing()
    {
        Profiler.SpawnAttribution = false;
        var probe = new ProbeBehavior();

        MissionAttributionHooks.TimedAgentBuild(probe, null!, null!);
        Boundary();
        Profiler.SpawnAttribution = true;

        Assert.AreEqual(1, probe.Builds);
        Assert.AreEqual(0, Profiler.TakeExtrasWindow(8).SpawnTop.Count);
    }

    [TestMethod]
    public void TimedAgentBuild_BehaviourThrows_PropagatesTheSameException_AndRecordsTheCall()
    {
        var boom = new InvalidOperationException("build broke");
        var probe = new ProbeBehavior { Throw = boom };

        var ex = Assert.ThrowsException<InvalidOperationException>(() => MissionAttributionHooks.TimedAgentBuild(probe, null!, null!));
        Boundary();

        Assert.AreSame(boom, ex);
        Assert.AreEqual(1, Profiler.TakeExtrasWindow(8).SpawnTop.Single().Calls);
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void TimedParallelBlock_RangeBelowGrain_RunsTheBodyOnce_AndAddsParallelTime()
    {
        var runs = 0;

        MissionAttributionHooks.TimedParallelBlock(0, 1, 0.016f, (from, to, dt) => { runs++; Thread.Sleep(10); }, 8);
        Boundary();

        Assert.AreEqual(1, runs);
        var w = Profiler.TakeExtrasWindow(8);
        Assert.IsTrue(w.ScriptParallelMs >= 8d, w.ScriptParallelMs.ToString());
        Assert.AreEqual(0d, w.OccasionalMs);
    }

    [TestMethod]
    public void TimedOccasionalBlock_RangeBelowGrain_AddsOccasionalTime()
    {
        var runs = 0;

        MissionAttributionHooks.TimedOccasionalBlock(0, 1, 0.016f, (from, to, dt) => { runs++; Thread.Sleep(10); }, 8);
        Boundary();

        Assert.AreEqual(1, runs);
        var w = Profiler.TakeExtrasWindow(8);
        Assert.IsTrue(w.OccasionalMs >= 8d, w.OccasionalMs.ToString());
        Assert.AreEqual(0d, w.ScriptParallelMs);
    }

    private sealed class ProbeBehavior : MissionLogic
    {
        internal Exception? Throw;
        internal int Builds;

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            Builds++;
            if (Throw != null)
                throw Throw;
        }
    }
}
