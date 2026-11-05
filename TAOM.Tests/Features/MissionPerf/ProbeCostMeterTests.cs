using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionPerf;
using TAOM.Features.MissionPerf.Hooks;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The start-up measurement behind the probe install line's <c>bookkeeping</c> figure: the profiler work the
/// Patch98 hooks do per frame, without Harmony, on a scratch profiler that never touches the live one.
/// </summary>
[TestClass]
public class ProbeCostMeterTests
{
    [TestCleanup]
    public void Cleanup() => MissionTickProfilerHooks.Profiler = null;

    [TestMethod]
    public void MeasureBookkeeping_ReturnsAFinitePositiveCost()
    {
        var us = ProbeCostMeter.MeasureBookkeepingMicroseconds(2000);

        Assert.IsFalse(double.IsNaN(us) || double.IsInfinity(us), us.ToString());
        Assert.IsTrue(us > 0d, us.ToString());
    }

    [TestMethod]
    public void MeasureBookkeeping_LeavesTheStaticProfilerUntouched()
    {
        var live = new MissionTickProfiler(Stopwatch.Frequency);
        live.BeginMission(Stopwatch.GetTimestamp(), 1, measuring: true, hitchThresholdMs: 1000, behaviorTiming: false);
        MissionTickProfilerHooks.Profiler = live;
        Assert.AreEqual(0, live.TakeWindow(8).Frames);

        ProbeCostMeter.MeasureBookkeepingMicroseconds(2000);

        Assert.AreSame(live, MissionTickProfilerHooks.Profiler);
        Assert.AreEqual(0, live.TakeWindow(8).Frames);
        Assert.AreEqual(0, live.TakeExtrasWindow(8).Frames);
        Assert.AreEqual(0, live.SummarizeExtras(8).Spawns);
    }
}
