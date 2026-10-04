using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionPerf;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The frame, window and mission arithmetic of the Patch97 tick profiler. Constructed with
/// ticksPerSecond 1000, so one tick is one millisecond and every oracle reads in ms.
/// </summary>
[TestClass]
public class MissionTickProfilerTests
{
    private const double NoHitch = 100000d;

    private static MissionTickProfiler Started(out int slot, double hitchMs = NoHitch, long start = 0)
    {
        var p = new MissionTickProfiler(1000);
        p.BeginMission(start, Thread.CurrentThread.ManagedThreadId, measuring: true, hitchThresholdMs: hitchMs);
        slot = p.Behaviors.SlotFor(typeof(string));
        return p;
    }

    [TestMethod]
    public void CloseFrame_FirstBoundaryOfMission_ReturnsNullAndCountsNoFrame()
    {
        var p = Started(out var s, hitchMs: 1);
        p.Record(TickPhase.MissionTick, s, 40, 0);
        p.AddWait(7);
        p.AddAgentTick(9, onMainThread: true);

        Assert.IsNull(p.CloseFrame(500, 0, 0, 0, 0));
        Assert.IsNull(p.CloseFrame(500, 0, 0, 0, 0), "A zero-length second frame is below a 1 ms threshold.");

        var w = p.TakeWindow(8);
        Assert.AreEqual(1, w.Frames);
        Assert.AreEqual(0d, w.MissionTickMs);
        Assert.AreEqual(0d, w.WaitTickMs);
        Assert.AreEqual(0d, w.AgentTickMs);
        Assert.AreEqual(0, w.Top.Count);
    }

    [TestMethod]
    public void CloseFrame_SumsPhasesIntoTheWindow()
    {
        var p = Started(out var s);
        p.CloseFrame(100, 0, 0, 0, 0);
        p.Record(TickPhase.PreDisplay, s, 2, 0);
        p.Record(TickPhase.MissionTick, s, 10, 0);
        p.Record(TickPhase.PreTick, s, 3, 0);
        p.AddWait(5);
        p.AddAgentTick(30, onMainThread: false);
        p.CloseFrame(150, 0, 0, 0, 0);
        p.Record(TickPhase.MissionTick, s, 20, 0);
        p.AddAgentTick(15, onMainThread: true);
        p.CloseFrame(250, 0, 0, 0, 0);

        var w = p.TakeWindow(8);
        Assert.AreEqual(2, w.Frames);
        Assert.AreEqual(150d, w.WallMs);
        Assert.AreEqual(2d, w.PreDisplayMs);
        Assert.AreEqual(30d, w.MissionTickMs);
        Assert.AreEqual(3d, w.PreTickMs);
        Assert.AreEqual(5d, w.WaitTickMs);
        Assert.AreEqual(45d, w.AgentTickMs);
        Assert.AreEqual(95d, w.OtherMs);
        Assert.AreEqual(w.WallMs, w.PreDisplayMs + w.MissionTickMs + w.PreTickMs + w.WaitTickMs + w.OtherMs + 15d);
    }

    [TestMethod]
    public void CloseFrame_OtherMs_IsFrameMinusMainThreadPhases()
    {
        var p = Started(out var s);
        p.CloseFrame(0, 0, 0, 0, 0);
        p.Record(TickPhase.MissionTick, s, 10, 0);
        p.AddWait(4);
        p.AddAgentTick(30, onMainThread: false);
        p.CloseFrame(50, 0, 0, 0, 0);

        Assert.AreEqual(36d, p.TakeWindow(8).OtherMs);
    }

    [TestMethod]
    public void CloseFrame_AgentTickOnMainThread_IsSubtractedFromOther()
    {
        var p = Started(out _);
        p.CloseFrame(0, 0, 0, 0, 0);
        p.AddAgentTick(30, onMainThread: true);
        p.CloseFrame(50, 0, 0, 0, 0);

        var w = p.TakeWindow(8);
        Assert.AreEqual(20d, w.OtherMs);
        Assert.AreEqual(30d, w.AgentTickMs);
    }

    [TestMethod]
    public void CloseFrame_OtherMs_NeverNegative()
    {
        var p = Started(out var s);
        p.CloseFrame(0, 0, 0, 0, 0);
        p.Record(TickPhase.MissionTick, s, 80, 0);
        p.AddAgentTick(40, onMainThread: true);
        p.CloseFrame(50, 0, 0, 0, 0);

        Assert.AreEqual(0d, p.TakeWindow(8).OtherMs);
    }

    [TestMethod]
    public void CloseFrame_AtThreshold_ReturnsHitch()
    {
        var p = Started(out _, hitchMs: 50);
        p.CloseFrame(0, 0, 0, 0, 0);
        var hitch = p.CloseFrame(50, 0, 0, 0, 0);

        Assert.IsNotNull(hitch);
        Assert.AreEqual(50d, hitch!.FrameMs);
    }

    [TestMethod]
    public void CloseFrame_BelowThreshold_ReturnsNull()
    {
        var p = Started(out _, hitchMs: 50);
        p.CloseFrame(0, 0, 0, 0, 0);
        Assert.IsNull(p.CloseFrame(49, 0, 0, 0, 0));
    }

    [TestMethod]
    public void Hitch_TopThree_AreTheFramesSlowestBehaviours()
    {
        var p = Started(out _, hitchMs: 10);
        var a = p.Behaviors.SlotFor(typeof(int));
        var b = p.Behaviors.SlotFor(typeof(long));
        var c = p.Behaviors.SlotFor(typeof(byte));
        var d = p.Behaviors.SlotFor(typeof(short));
        p.CloseFrame(0, 0, 0, 0, 0);
        p.Record(TickPhase.MissionTick, d, 1, 0);
        p.Record(TickPhase.MissionTick, a, 4, 0);
        p.Record(TickPhase.PreTick, b, 9, 0);
        p.Record(TickPhase.PreDisplay, c, 2, 0);
        var hitch = p.CloseFrame(100, 0, 0, 0, 0);

        CollectionAssert.AreEqual(new[] { "Int64", "Int32", "Byte" }, hitch!.Top.Select(t => t.Name).ToArray());
    }

    [TestMethod]
    public void Hitch_GcAndAllocDeltas_AreThisFramesOnly()
    {
        var p = Started(out _, hitchMs: 60);
        p.CloseFrame(0, 1000, 5, 3, 1);
        Assert.IsNull(p.CloseFrame(10, 1500, 7, 3, 1));
        var hitch = p.CloseFrame(100, 4000, 8, 4, 1);

        Assert.AreEqual(1, hitch!.Gc0);
        Assert.AreEqual(1, hitch.Gc1);
        Assert.AreEqual(0, hitch.Gc2);
        Assert.AreEqual(2500L, hitch.AllocBytes);
    }

    [TestMethod]
    public void TakeWindow_ResetsForTheNextWindow()
    {
        var p = Started(out var s);
        p.CloseFrame(0, 0, 0, 0, 0);
        p.Record(TickPhase.MissionTick, s, 5, 0);
        p.CloseFrame(20, 100, 0, 0, 0);
        p.TakeWindow(8);

        var w = p.TakeWindow(8);
        Assert.AreEqual(0, w.Frames);
        Assert.AreEqual(0d, w.WallMs);
        Assert.AreEqual(0d, w.MissionTickMs);
        Assert.AreEqual(0L, w.AllocBytes);
        Assert.AreEqual(0, w.Top.Count);
    }

    [TestMethod]
    public void TakeWindow_TopN_ComesFromTheBehaviourTable()
    {
        var p = Started(out _);
        var a = p.Behaviors.SlotFor(typeof(int));
        var b = p.Behaviors.SlotFor(typeof(long));
        p.CloseFrame(0, 0, 0, 0, 0);
        p.Record(TickPhase.MissionTick, a, 3, 0);
        p.Record(TickPhase.MissionTick, b, 8, 0);
        p.CloseFrame(20, 0, 0, 0, 0);

        var top = p.TakeWindow(1).Top;
        Assert.AreEqual(1, top.Count);
        Assert.AreEqual("Int64", top[0].Name);
        Assert.AreEqual(8d, top[0].Ms);
    }

    [TestMethod]
    public void TakeWindow_OpenFrameRecords_LandInTheNextWindow()
    {
        var p = Started(out var s);
        p.CloseFrame(0, 0, 0, 0, 0);
        p.Record(TickPhase.MissionTick, s, 6, 0);

        Assert.AreEqual(0, p.TakeWindow(8).Top.Count);

        p.CloseFrame(20, 0, 0, 0, 0);
        var w = p.TakeWindow(8);
        Assert.AreEqual(1, w.Top.Count);
        Assert.AreEqual(6d, w.MissionTickMs);
    }

    [TestMethod]
    public void AddAgentTick_FromAnotherThread_LandsInTheNextClosedFrame()
    {
        var p = Started(out _);
        p.CloseFrame(0, 0, 0, 0, 0);
        var worker = new Thread(() => p.AddAgentTick(40, onMainThread: false));
        worker.Start();
        worker.Join();
        p.CloseFrame(100, 0, 0, 0, 0);

        var w = p.TakeWindow(8);
        Assert.AreEqual(40d, w.AgentTickMs);
        Assert.AreEqual(100d, w.OtherMs);
    }

    [TestMethod]
    public void BeginMission_ResetsFramesWindowAndBehaviourTotals()
    {
        var p = Started(out var s);
        p.CloseFrame(0, 0, 0, 0, 0);
        p.Record(TickPhase.MissionTick, s, 6, 0);
        p.CloseFrame(20, 0, 0, 0, 0);

        p.BeginMission(1000, Thread.CurrentThread.ManagedThreadId, measuring: true, hitchThresholdMs: NoHitch);

        var w = p.TakeWindow(8);
        Assert.AreEqual(0, w.Frames);
        Assert.AreEqual(0d, w.WallMs);
        Assert.AreEqual(0, w.Top.Count);
        Assert.AreEqual(0, p.Summarize(8).Frames);
        Assert.IsNull(p.CloseFrame(1500, 0, 0, 0, 0), "The first boundary of the new mission only stamps the clock.");
        Assert.AreEqual(1000L, p.MissionStartTicks);
    }

    [TestMethod]
    public void EndMission_CurrentGeneration_StopsMeasuring()
    {
        var p = new MissionTickProfiler(1000);
        var g = p.BeginMission(0, 1, measuring: true, hitchThresholdMs: NoHitch);
        Assert.IsTrue(p.Measuring);

        Assert.IsTrue(p.EndMission(g));
        Assert.IsFalse(p.Measuring);
    }

    [TestMethod]
    public void EndMission_StaleGeneration_KeepsMeasuring()
    {
        var p = new MissionTickProfiler(1000);
        var g1 = p.BeginMission(0, 1, measuring: true, hitchThresholdMs: NoHitch);
        var g2 = p.BeginMission(0, 1, measuring: true, hitchThresholdMs: NoHitch);

        Assert.IsFalse(p.EndMission(g1));
        Assert.IsTrue(p.Measuring);
        Assert.AreEqual(g2, p.Generation);
    }

    [TestMethod]
    public void Summarize_CoversEveryClosedFrameAcrossWindows_WithHitchesAndWorst()
    {
        var p = Started(out var s, hitchMs: 50, start: 0);
        p.CloseFrame(1000, 0, 0, 0, 0);
        p.Record(TickPhase.MissionTick, s, 10, 64);
        p.CloseFrame(1060, 100, 0, 0, 0);             // 60 ms hitch at t=1.06 s
        p.TakeWindow(8);
        p.Record(TickPhase.MissionTick, s, 30, 32);
        p.CloseFrame(1160, 300, 0, 0, 0);             // 100 ms hitch at t=1.16 s, the worst
        p.TakeWindow(8);
        p.CloseFrame(1170, 300, 0, 0, 0);             // 10 ms, not taken into a window yet

        var sum = p.Summarize(8);
        Assert.AreEqual(3, sum.Frames);
        Assert.AreEqual(170d, sum.WallMs);
        Assert.AreEqual(40d, sum.MissionTickMs);
        Assert.AreEqual(130d, sum.OtherMs);
        Assert.AreEqual(300L, sum.AllocBytes);
        Assert.AreEqual(2, sum.Hitches);
        Assert.AreEqual(100d, sum.WorstHitchMs);
        Assert.AreEqual(1.16d, sum.WorstHitchTSeconds, 1e-9);
        var top = sum.Top.Single();
        Assert.AreEqual(40d, top.Ms);
        Assert.AreEqual(2, top.Calls);
        Assert.AreEqual(30d, top.MaxMs);
        Assert.AreEqual(96L, top.AllocBytes);
    }

    [TestMethod]
    public void CloseFrame_PastTheHitchLineCap_CountsEveryHitchButReturnsOnlyTheFirstCap()
    {
        var cap = MissionTickProfiler.MaxHitchLinesPerMission;
        var p = Started(out _, hitchMs: 1);
        p.CloseFrame(0, 0, 0, 0, 0);
        long now = 0;
        var returned = 0;
        var capFrames = 0;
        for (var i = 0; i < cap + 4; i++)
        {
            now += 10;
            if (p.CloseFrame(now, 0, 0, 0, 0) != null)
                returned++;
            if (p.HitchCapReachedThisFrame)
                capFrames++;
        }
        now += 500;
        Assert.IsNull(p.CloseFrame(now, 0, 0, 0, 0), "Past the cap no frame comes back, however slow.");

        Assert.AreEqual(cap, returned);
        Assert.AreEqual(1, capFrames, "The cap is reported once, on the first frame past it.");
        var sum = p.Summarize(8);
        Assert.AreEqual(cap + 5, sum.Hitches, "Every hitch is still counted.");
        Assert.AreEqual(500d, sum.WorstHitchMs, "The worst hitch is still tracked past the cap.");
    }

    [TestMethod]
    public void BeginMission_ResetsTheHitchLineCap()
    {
        var p = Started(out _, hitchMs: 1);
        p.CloseFrame(0, 0, 0, 0, 0);
        for (var i = 1; i <= MissionTickProfiler.MaxHitchLinesPerMission + 1; i++)
            p.CloseFrame(i * 10, 0, 0, 0, 0);

        p.BeginMission(0, Thread.CurrentThread.ManagedThreadId, measuring: true, hitchThresholdMs: 1);
        p.CloseFrame(0, 0, 0, 0, 0);

        Assert.IsNotNull(p.CloseFrame(10, 0, 0, 0, 0));
        Assert.IsFalse(p.HitchCapReachedThisFrame);
    }
}
