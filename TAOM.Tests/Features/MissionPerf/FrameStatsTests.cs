using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionPerf;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The pure half of the <c>[MissionPerf]</c> heartbeat: a window of frame times that emits every
/// five seconds of wall clock. Wall clock, not mission time: mission time slows under time scaling
/// and stops in the order menu, and the question the line answers is "how long do frames take".
/// </summary>
[TestClass]
public class FrameStatsTests
{
    [TestMethod]
    public void ShouldEmit_BeforeIntervalElapsed_ReturnsFalse()
    {
        var sut = new FrameStats(intervalSeconds: 5.0);

        Assert.IsFalse(sut.ShouldEmit(0.0));
        Assert.IsFalse(sut.ShouldEmit(4.9));
    }

    [TestMethod]
    public void ShouldEmit_AfterIntervalElapsed_ReturnsTrue()
    {
        var sut = new FrameStats(intervalSeconds: 5.0);
        sut.ShouldEmit(0.0);

        Assert.IsTrue(sut.ShouldEmit(5.0));
    }

    [TestMethod]
    public void Emit_WithRecordedFrames_ReportsCountAverageAndMax()
    {
        var sut = new FrameStats(intervalSeconds: 5.0);
        sut.ShouldEmit(0.0);
        sut.Record(10.0);
        sut.Record(20.0);
        sut.Record(30.0);

        var window = sut.Emit(5.0);

        Assert.AreEqual(3, window.Frames);
        Assert.AreEqual(20.0, window.AverageMs, 1e-9);
        Assert.AreEqual(30.0, window.MaxMs, 1e-9);
        Assert.AreEqual(5.0, window.Seconds, 1e-9);
        Assert.AreEqual(0.6, window.Fps, 1e-9);
    }

    [TestMethod]
    public void Emit_P95_IsNearestRankOfSortedSamples()
    {
        var sut = new FrameStats(intervalSeconds: 5.0);
        sut.ShouldEmit(0.0);
        // 1..20 ms in shuffled order: nearest-rank p95 of 20 samples is the 19th value.
        foreach (var ms in new[] { 7, 3, 20, 1, 15, 9, 12, 4, 18, 6, 11, 2, 17, 8, 14, 5, 19, 10, 16, 13 })
            sut.Record(ms);

        var window = sut.Emit(5.0);

        Assert.AreEqual(19.0, window.P95Ms, 1e-9);
    }

    [TestMethod]
    public void Emit_SingleSample_P95IsThatSample()
    {
        var sut = new FrameStats(intervalSeconds: 5.0);
        sut.ShouldEmit(0.0);
        sut.Record(16.7);

        Assert.AreEqual(16.7, sut.Emit(5.0).P95Ms, 1e-9);
    }

    [TestMethod]
    public void Emit_NoFrames_ReportsZerosNotNaN()
    {
        var sut = new FrameStats(intervalSeconds: 5.0);
        sut.ShouldEmit(0.0);

        var window = sut.Emit(5.0);

        Assert.AreEqual(0, window.Frames);
        Assert.AreEqual(0.0, window.AverageMs);
        Assert.AreEqual(0.0, window.P95Ms);
        Assert.AreEqual(0.0, window.Fps);
    }

    [TestMethod]
    public void Emit_ResetsTheWindow_SoTheNextWindowStartsEmpty()
    {
        var sut = new FrameStats(intervalSeconds: 5.0);
        sut.ShouldEmit(0.0);
        sut.Record(50.0);
        sut.Emit(5.0);
        sut.Record(10.0);

        var second = sut.Emit(10.0);

        Assert.AreEqual(1, second.Frames);
        Assert.AreEqual(10.0, second.AverageMs, 1e-9);
        Assert.AreEqual(5.0, second.Seconds, 1e-9);
    }

    [TestMethod]
    public void Record_BeyondCapacity_KeepsTheNewestSamples()
    {
        var sut = new FrameStats(intervalSeconds: 5.0, maxSamples: 4);
        sut.ShouldEmit(0.0);
        foreach (var ms in new[] { 100.0, 1.0, 2.0, 3.0, 4.0 })
            sut.Record(ms);

        var window = sut.Emit(5.0);

        // Count, average and max cover every frame seen; only the percentile sample set is bounded,
        // and it keeps the newest samples so a long stall early in the window cannot pin it.
        Assert.AreEqual(5, window.Frames);
        Assert.AreEqual(22.0, window.AverageMs, 1e-9);
        Assert.AreEqual(100.0, window.MaxMs, 1e-9);
        Assert.AreEqual(4.0, window.P95Ms, 1e-9);
    }

    [TestMethod]
    public void Reset_ForgetsTheClock_SoTheFirstEmitWaitsAFullInterval()
    {
        var sut = new FrameStats(intervalSeconds: 5.0);
        sut.ShouldEmit(100.0);
        sut.Reset();

        Assert.IsFalse(sut.ShouldEmit(102.0));
        Assert.IsTrue(sut.ShouldEmit(107.0));
    }

    [TestMethod]
    public void BuildLine_FormatsEveryFieldInvariantly()
    {
        var window = new FrameWindow(frames: 300, seconds: 5.0, averageMs: 16.6667, p95Ms: 25.5, maxMs: 40.25);

        var line = MissionPerfLine.Build(tSeconds: 65.0, window, agents: 812, activeAgents: 640, formations: 9, gc0: 12, gc1: 3, gc2: 1);

        Assert.AreEqual(
            "[MissionPerf] t=+65s frames=300 fps=60.0 avgMs=16.67 p95Ms=25.50 maxMs=40.3 agents=812 active=640 formations=9 gc0=12 gc1=3 gc2=1",
            line);
    }
}
