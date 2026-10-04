using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionPerf;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The Patch98 hitch probe's accumulators on <see cref="MissionTickProfiler"/>: the probe-mode phase
/// columns derived from the whole-method brackets, the per-frame <see cref="HitchDetailFrame"/>, and the
/// spawn, script and clip-loading totals per window and per mission. Constructed with ticksPerSecond 1000,
/// so one tick is one millisecond and every oracle reads in ms.
/// </summary>
[TestClass]
public class MissionTickProfilerProbeTests
{
    private const double NoHitch = 100000d;

    private static MissionTickProfiler Probe(double hitchMs = NoHitch, bool behaviorTiming = false)
    {
        var p = new MissionTickProfiler(1000);
        p.BeginMission(0, Environment.CurrentManagedThreadId, measuring: true, hitchThresholdMs: hitchMs, behaviorTiming: behaviorTiming);
        return p;
    }

    private static void ProbeBrackets(MissionTickProfiler p)
    {
        p.AddPreTickAll(40);
        p.AddWait(30);
        p.AddOnTick(60);
        p.AddAgentTick(10, onMainThread: true);
        p.AddAgentTick(25, onMainThread: false);
    }

    [TestMethod]
    public void CloseFrame_ProbeMode_DerivesPhasesFromTheWholeMethodBrackets()
    {
        var p = Probe(hitchMs: 1000);
        p.CloseFrame(100, 0, 0, 0, 0);
        ProbeBrackets(p);
        p.CloseFrame(300, 0, 0, 0, 0);

        var w = p.TakeWindow(8);
        Assert.AreEqual(1, w.Frames);
        Assert.AreEqual(200d, w.WallMs);
        Assert.AreEqual(0d, w.PreDisplayMs);
        Assert.AreEqual(50d, w.MissionTickMs);
        Assert.AreEqual(10d, w.PreTickMs);
        Assert.AreEqual(30d, w.WaitTickMs);
        Assert.AreEqual(35d, w.AgentTickMs);
        Assert.AreEqual(100d, w.OtherMs);
        Assert.AreEqual(0, w.Top.Count);
    }

    [TestMethod]
    public void CloseFrame_FullMode_KeepsBehaviourSums_AndReportsBracketsInTheDetail()
    {
        var p = Probe(hitchMs: 150, behaviorTiming: true);
        var slot = p.Behaviors.SlotFor(typeof(string));
        p.CloseFrame(100, 0, 0, 0, 0);
        ProbeBrackets(p);
        p.Record(TickPhase.MissionTick, slot, 20, 0);

        var hitch = p.CloseFrame(300, 0, 0, 0, 0);

        Assert.IsNotNull(hitch);
        Assert.IsNotNull(hitch.Detail);
        Assert.AreEqual(60d, hitch.Detail.OnTickMs);
        Assert.AreEqual(40d, hitch.Detail.PreTickAllMs);
        Assert.AreEqual("full", hitch.Detail.Mode);
        var w = p.TakeWindow(8);
        Assert.AreEqual(20d, w.MissionTickMs);
        Assert.AreEqual(0d, w.PreTickMs);
        Assert.AreEqual(140d, w.OtherMs);
    }

    [TestMethod]
    public void CloseFrame_ProbeMode_NeverRecordsBehaviourTop()
    {
        var p = Probe(hitchMs: 1);
        p.CloseFrame(100, 0, 0, 0, 0);
        ProbeBrackets(p);

        var hitch = p.CloseFrame(300, 0, 0, 0, 0);

        Assert.IsNotNull(hitch);
        Assert.AreEqual(0, hitch.Top.Count);
        Assert.AreEqual("probe", hitch.Detail!.Mode);
        Assert.AreEqual(0, p.TakeWindow(8).Top.Count);
        Assert.AreEqual(0, p.Summarize(8).Top.Count);
    }

    [TestMethod]
    public void CloseFrame_Hitch_CarriesItsDetail()
    {
        var p = Probe(hitchMs: 1);
        p.ScriptBlockTiming = true;
        p.AnimSampling = true;
        p.CloseFrame(100, 0, 0, 0, 0);
        p.MarkAnimLoading(true);
        for (var i = 0; i < 3; i++)
            p.CountSpawn();
        p.AddSpawnTime(12);
        p.AddScriptTick(4);
        p.AddScriptParallel(1);
        p.AddOccasional(2);
        p.AddOnTick(20);
        p.AddPreTickAll(5);

        var d = p.CloseFrame(300, 0, 0, 0, 0)!.Detail!;

        Assert.AreEqual(12d, d.SpawnMs);
        Assert.AreEqual(3, d.Spawns);
        Assert.AreEqual(4d, d.ScriptTickMs);
        Assert.AreEqual(1d, d.ScriptParallelMs);
        Assert.AreEqual(2d, d.OccasionalMs);
        Assert.AreEqual(1, d.AnimLoading);
        Assert.AreEqual(20d, d.OnTickMs);
        Assert.AreEqual(5d, d.PreTickAllMs);
        Assert.AreEqual("probe", d.Mode);
    }

    [TestMethod]
    public void CloseFrame_BelowThreshold_ReturnsNullWithoutDetail()
    {
        var p = Probe(hitchMs: 1000);
        p.CloseFrame(100, 0, 0, 0, 0);
        p.CountSpawn();
        p.AddSpawnTime(3);

        Assert.IsNull(p.CloseFrame(150, 0, 0, 0, 0));
        Assert.AreEqual(1, p.TakeExtrasWindow(8).Spawns, "A frame below the threshold still reaches the window.");
    }

    [TestMethod]
    public void MarkAnimLoading_AppliesToTheFrameItOpens()
    {
        var p = Probe(hitchMs: 1);
        p.AnimSampling = true;
        p.CloseFrame(100, 0, 0, 0, 0);
        p.MarkAnimLoading(true);
        var first = p.CloseFrame(200, 0, 0, 0, 0)!;
        p.MarkAnimLoading(false);
        var second = p.CloseFrame(300, 0, 0, 0, 0)!;

        Assert.AreEqual(1, first.Detail!.AnimLoading);
        Assert.AreEqual(0, second.Detail!.AnimLoading);
        var w = p.TakeExtrasWindow(8);
        Assert.AreEqual(1, w.LoadingFrames);
        Assert.AreEqual(2, w.Frames);
    }

    [TestMethod]
    public void TakeExtrasWindow_CountsLoadingFramesAndFrames()
    {
        var p = Probe();
        p.AnimSampling = true;
        p.CloseFrame(0, 0, 0, 0, 0);
        for (var i = 1; i <= 5; i++)
        {
            p.MarkAnimLoading(i % 2 == 1);
            p.CloseFrame(i * 10, 0, 0, 0, 0);
        }

        var w = p.TakeExtrasWindow(8);
        Assert.AreEqual(5, w.Frames);
        Assert.AreEqual(3, w.LoadingFrames);
        Assert.IsTrue(w.AnimSampling);
        var next = p.TakeExtrasWindow(8);
        Assert.AreEqual(0, next.Frames, "Taking the window resets it.");
        Assert.AreEqual(0, next.LoadingFrames);
    }

    [TestMethod]
    public void TakeExtrasWindow_OpenFrameValues_LandInTheNextWindow()
    {
        var p = Probe();
        p.CloseFrame(0, 0, 0, 0, 0);
        p.CloseFrame(10, 0, 0, 0, 0);
        p.CountSpawn();
        p.AddSpawnTime(4);

        var now = p.TakeExtrasWindow(8);
        p.CloseFrame(20, 0, 0, 0, 0);
        var next = p.TakeExtrasWindow(8);

        Assert.AreEqual(0, now.Spawns);
        Assert.AreEqual(0d, now.SpawnMs);
        Assert.AreEqual(1, next.Spawns);
        Assert.AreEqual(4d, next.SpawnMs);
    }

    [TestMethod]
    public void AddScriptTick_FromAnotherThread_LandsInTheNextClosedFrame()
    {
        var p = Probe();
        p.ScriptBlockTiming = true;
        p.CloseFrame(0, 0, 0, 0, 0);
        var worker = new Thread(() =>
        {
            p.AddScriptTick(7);
            p.AddScriptParallel(3);
            p.AddOccasional(1);
        });
        worker.Start();
        worker.Join();
        p.CloseFrame(50, 0, 0, 0, 0);

        var w = p.TakeExtrasWindow(8);
        Assert.AreEqual(7d, w.ScriptTickMs);
        Assert.AreEqual(3d, w.ScriptParallelMs);
        Assert.AreEqual(1d, w.OccasionalMs);
        Assert.AreEqual(1, w.ScriptCalls);
    }

    [TestMethod]
    public void SpawnsBeforeTheFirstBoundary_LandInPreFrameTotals_NotInAWindow()
    {
        var p = Probe();
        p.CountSpawn();
        p.AddSpawnTime(5);
        p.CloseFrame(0, 0, 0, 0, 0);
        p.CloseFrame(10, 0, 0, 0, 0);

        Assert.AreEqual(0, p.TakeExtrasWindow(8).Spawns);
        var m = p.SummarizeExtras(8);
        Assert.AreEqual(1, m.PreFrameSpawns);
        Assert.AreEqual(5d, m.PreFrameSpawnMs);
        Assert.AreEqual(0, m.Spawns);
    }

    [TestMethod]
    public void SummarizeExtras_SumsEveryClosedFrame_AndCountsHitchesWithAnimLoading()
    {
        var p = Probe(hitchMs: 50);
        p.AnimSampling = true;
        p.CloseFrame(0, 0, 0, 0, 0);
        p.CountSpawn();
        p.AddSpawnTime(2);
        p.AddScriptTick(3);
        p.AddOnTick(6);
        p.AddPreTickAll(8);
        p.MarkAnimLoading(true);
        p.CloseFrame(100, 0, 0, 0, 0);
        p.TakeExtrasWindow(8);
        p.CountSpawn();
        p.CountOffMainSpawn();
        p.AddSpawnTime(4);
        p.AddScriptTick(5);
        p.AddOnTick(7);
        p.AddPreTickAll(9);
        p.MarkAnimLoading(true);
        p.CloseFrame(120, 0, 0, 0, 0);

        var m = p.SummarizeExtras(8);
        Assert.AreEqual(2, m.Frames);
        Assert.AreEqual(2, m.Spawns);
        Assert.AreEqual(6d, m.SpawnMs);
        Assert.AreEqual(8d, m.ScriptTickMs);
        Assert.AreEqual(13d, m.OnTickMs);
        Assert.AreEqual(17d, m.PreTickAllMs);
        Assert.AreEqual(2, m.AnimLoadingFrames);
        Assert.AreEqual(1, m.HitchesWithAnimLoading, "Only the 100 ms frame is a hitch.");
        Assert.AreEqual(1, m.OffMainSpawns);
        Assert.AreEqual("probe", m.Mode);
        Assert.IsTrue(double.IsNaN(m.ScriptParallelMs), "Parallel blocks are not timed in probe mode.");
        Assert.IsTrue(double.IsNaN(m.OccasionalMs));
    }

    [TestMethod]
    public void SummarizeExtras_TopsComeFromTheSpawnAndScriptTables()
    {
        var p = Probe(behaviorTiming: true);
        p.SpawnAttribution = true;
        p.ScriptAttribution = true;
        p.CloseFrame(0, 0, 0, 0, 0);
        p.SpawnBuilds.Record(p.SpawnBuilds.SlotFor(typeof(string)), 12, 0);
        p.ScriptComponents.Record(p.ScriptComponents.SlotFor(typeof(int)), 9, 0);
        p.CloseFrame(10, 0, 0, 0, 0);
        p.TakeExtrasWindow(8);

        var m = p.SummarizeExtras(8);

        Assert.AreEqual("String", m.SpawnTop.Single().Name);
        Assert.AreEqual(12d, m.SpawnTop.Single().Ms);
        Assert.AreEqual("Int32", m.ScriptTop.Single().Name);
        Assert.AreEqual(9d, m.ScriptTop.Single().Ms);
        Assert.AreEqual("full", m.Mode);
    }

    [TestMethod]
    public void BeginMission_ResetsEveryExtra()
    {
        var p = Probe(behaviorTiming: true);
        p.SpawnAttribution = true;
        p.ScriptAttribution = true;
        p.ScriptBlockTiming = true;
        p.AnimSampling = true;
        p.CountSpawn();
        p.AddSpawnTime(1);
        p.CloseFrame(0, 0, 0, 0, 0);
        p.SpawnBuilds.Record(p.SpawnBuilds.SlotFor(typeof(string)), 12, 0);
        p.CountSpawn();
        p.CountOffMainSpawn();
        p.AddScriptTick(3);
        p.MarkAnimLoading(true);
        p.CloseFrame(10, 0, 0, 0, 0);
        p.AddScriptTick(4);

        p.BeginMission(100, Environment.CurrentManagedThreadId, measuring: true, hitchThresholdMs: NoHitch, behaviorTiming: false);

        Assert.IsFalse(p.BehaviorTiming);
        Assert.IsFalse(p.SpawnAttribution);
        Assert.IsFalse(p.ScriptAttribution);
        Assert.IsFalse(p.ScriptBlockTiming);
        Assert.IsFalse(p.AnimSampling);
        p.CloseFrame(100, 0, 0, 0, 0);
        p.CloseFrame(110, 0, 0, 0, 0);
        var w = p.TakeExtrasWindow(8);
        Assert.AreEqual(1, w.Frames);
        Assert.AreEqual(0, w.Spawns);
        Assert.AreEqual(0d, w.ScriptTickMs);
        Assert.AreEqual(0, w.ScriptCalls);
        Assert.AreEqual(0, w.LoadingFrames);
        var m = p.SummarizeExtras(8);
        Assert.AreEqual(1, m.Frames);
        Assert.AreEqual(0, m.Spawns);
        Assert.AreEqual(0, m.PreFrameSpawns);
        Assert.AreEqual(0d, m.PreFrameSpawnMs);
        Assert.AreEqual(0, m.OffMainSpawns);
        Assert.AreEqual(0d, m.ScriptTickMs);
        Assert.AreEqual(-1, m.AnimLoadingFrames, "The sampler was never on in the new mission.");
        Assert.AreEqual(-1, m.HitchesWithAnimLoading);
        Assert.AreEqual(0, m.SpawnTop.Count);
    }

    [TestMethod]
    public void BeginMission_WithoutBehaviorTiming_DefaultsToFullMode()
    {
        var p = new MissionTickProfiler(1000);
        p.BeginMission(0, Environment.CurrentManagedThreadId, measuring: true, hitchThresholdMs: NoHitch);

        Assert.IsTrue(p.BehaviorTiming);
        p.CloseFrame(0, 0, 0, 0, 0);
        p.CloseFrame(10, 0, 0, 0, 0);
        Assert.AreEqual("full", p.SummarizeExtras(8).Mode);
    }
}
