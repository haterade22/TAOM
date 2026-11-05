using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

[TestClass]
public class MainThreadHeartbeatTests
{
    [TestMethod]
    public void TryRead_BeforeAnyBeat_ReturnsFalse()
    {
        Assert.IsFalse(new MainThreadHeartbeat().TryRead(out long ticks, out _));
        Assert.AreEqual(0L, ticks);
    }

    [TestMethod]
    public void BeatAt_RecordsTheTicks()
    {
        var sut = new MainThreadHeartbeat();
        sut.BeatAt(12345L);
        Assert.IsTrue(sut.TryRead(out long ticks, out _));
        Assert.AreEqual(12345L, ticks);
    }

    [TestMethod]
    public void BindCurrentThread_RecordsThisThread()
    {
        var sut = new MainThreadHeartbeat();
        sut.BindCurrentThread();
        sut.TryRead(out _, out var thread);
        Assert.AreSame(System.Threading.Thread.CurrentThread, thread);
    }
}

[TestClass]
public class FreezeSamplePolicyTests
{
    [TestMethod]
    public void CrossesThreshold_FiresOncePerThresholdInOrder()
    {
        Assert.IsFalse(FreezeSamplePolicy.CrossesThreshold(9.9, 0));
        Assert.IsTrue(FreezeSamplePolicy.CrossesThreshold(10, 0));
        Assert.IsFalse(FreezeSamplePolicy.CrossesThreshold(10, 1));
        Assert.IsTrue(FreezeSamplePolicy.CrossesThreshold(20, 1));
        Assert.IsTrue(FreezeSamplePolicy.CrossesThreshold(40, 2));
        Assert.IsTrue(FreezeSamplePolicy.CrossesThreshold(80, 3));
        Assert.IsFalse(FreezeSamplePolicy.CrossesThreshold(1000, 4));
    }

    [TestMethod]
    public void Decide_WalkOrExitSampler_IsNone()
    {
        Assert.AreEqual(FreezeSampleAction.None, FreezeSamplePolicy.Decide(0, FreezeStandDown.PrecompileWalk, false, false));
        Assert.AreEqual(FreezeSampleAction.None, FreezeSamplePolicy.Decide(3, FreezeStandDown.ExitSampler, false, false));
    }

    [TestMethod]
    public void Decide_MissionFrame_LogsWithoutStack()
    {
        Assert.AreEqual(FreezeSampleAction.LogWithoutStack, FreezeSamplePolicy.Decide(0, FreezeStandDown.MissionFrame, false, false));
    }

    [TestMethod]
    public void Decide_HeapMovingBeforeForty_IsNone()
    {
        Assert.AreEqual(FreezeSampleAction.None, FreezeSamplePolicy.Decide(0, FreezeStandDown.None, true, false));
        Assert.AreEqual(FreezeSampleAction.None, FreezeSamplePolicy.Decide(1, FreezeStandDown.None, true, false));
    }

    [TestMethod]
    public void Decide_HeapMovingAtFortyOnce_LogsWithoutStackThenNone()
    {
        Assert.AreEqual(FreezeSampleAction.LogWithoutStack, FreezeSamplePolicy.Decide(2, FreezeStandDown.None, true, false));
        Assert.AreEqual(FreezeSampleAction.None, FreezeSamplePolicy.Decide(3, FreezeStandDown.None, true, true));
    }

    [DataTestMethod]
    [DataRow(2, 69d, false)]   // E4: the blocked 69 s wait
    [DataRow(3, 10d, true)]
    [DataRow(8, 40d, false)]
    [DataRow(12, 40d, true)]
    [DataRow(44, 10d, true)]   // E4: normal play, 4.4/s
    [DataRow(5, 0d, false)]
    public void HeapMoving_CalibrationCells(int gen0Delta, double window, bool expected)
    {
        Assert.AreEqual(expected, FreezeSamplePolicy.HeapMoving(gen0Delta, window));
    }
}

/// <summary>Pinned literals: tools/tests/test_triage_battle_load.py parses these same strings.</summary>
[TestClass]
public class FreezeLogTests
{
    [TestMethod]
    public void FormatSample_AllTokens_MatchesPinnedLiteral()
    {
        Assert.AreEqual(
            "[Freeze] main thread silent 20s sample#2 context=battle-load shaders=2697 rglCompiles=31 lastCompileAgoS=2 gc=3167/785/81 heapMB=466 last phase=WaitingForRender seq=40 waitedMs=2259 shaders=2697",
            FreezeLog.FormatSample(20, 2, "battle-load", 2697, true, 31, 2, "gc=3167/785/81 heapMB=466", "phase=WaitingForRender seq=40 waitedMs=2259 shaders=2697"));
    }

    [TestMethod]
    public void FormatSample_Unmeasured_OmitsTokens()
    {
        string noShaders = FreezeLog.FormatSample(10, 1, "other", -1, true, 0, null, "gc=1/1/1 heapMB=1", "phase=X");
        StringAssert.DoesNotMatch(noShaders, new System.Text.RegularExpressions.Regex("shaders="));
        StringAssert.Contains(noShaders, "rglCompiles=0");
        StringAssert.DoesNotMatch(noShaders, new System.Text.RegularExpressions.Regex("lastCompileAgoS"));

        string noRgl = FreezeLog.FormatSample(10, 1, "other", -1, false, 7, 3, "gc=1/1/1 heapMB=1", "phase=X");
        StringAssert.Contains(noRgl, "rgl=unavailable");
        StringAssert.DoesNotMatch(noRgl, new System.Text.RegularExpressions.Regex("rglCompiles|lastCompileAgoS"));
    }

    [TestMethod]
    public void FormatRecovered_MatchesPinnedLiterals()
    {
        Assert.AreEqual("[Freeze] main thread recovered after about 68s rglCompiles=1569", FreezeLog.FormatRecovered(68, true, 1569));
        Assert.AreEqual("[Freeze] main thread recovered after about 45s rgl=unavailable", FreezeLog.FormatRecovered(45, false, 0));
    }

    [TestMethod]
    public void FormatStarted_NamesTheLogFileOrItsAbsence()
    {
        Assert.AreEqual(
            "[Freeze] main-thread freeze sampler started; engine log rgl_log_1744.txt",
            FreezeLog.FormatStarted(@"C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_1744.txt"));
        Assert.AreEqual(
            "[Freeze] main-thread freeze sampler started; engine log not found, compile counts unavailable",
            FreezeLog.FormatStarted(null));
    }
}

[TestClass]
public class RglLogLocatorTests
{
    private static readonly DateTime Start = new DateTime(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);

    private static RglCandidate C(string name, int minutesAfterStart, int root = 0)
        => new RglCandidate(@"C:\logs" + root + @"\" + name, Start.AddMinutes(minutesAfterStart), root);

    [TestMethod]
    public void PidExactName_IsChosen()
    {
        var chosen = RglLogLocator.Choose(1744, Start, new List<RglCandidate>
        {
            C("rgl_log_9999.txt", 5),
            C("rgl_log_1744.txt", 1, root: 1),
            C("rgl_log_1744.txt", -600, root: 0),
        });
        Assert.AreEqual(@"C:\logs0\rgl_log_1744.txt", chosen);
    }

    [TestMethod]
    public void ErrorsLog_IsNeverChosen()
    {
        var chosen = RglLogLocator.Choose(1744, Start, new List<RglCandidate>
        {
            C("rgl_log_errors_1744.txt", 9),
            C("rgl_log_errors_5.txt", 9),
        });
        Assert.IsNull(chosen);
    }

    [TestMethod]
    public void NoPidMatch_FallsBackToNewestWrittenSinceProcessStart()
    {
        var chosen = RglLogLocator.Choose(1744, Start, new List<RglCandidate>
        {
            C("rgl_log_1.txt", 1),
            C("rgl_log_2.txt", 3),
            C("rgl_log_errors_2.txt", 9),
            C("rgl_log_3.txt", -5),
        });
        Assert.AreEqual(@"C:\logs0\rgl_log_2.txt", chosen);
    }

    [TestMethod]
    public void NothingSuitable_ReturnsNull()
    {
        Assert.IsNull(RglLogLocator.Choose(1744, Start, new List<RglCandidate> { C("rgl_log_3.txt", -5) }));
        Assert.IsNull(RglLogLocator.Choose(1744, Start, new List<RglCandidate>()));
    }
}
