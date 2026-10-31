using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
public class FordsExitClockTests
{
    [TestMethod]
    public void Tick_WhileTheMatchRuns_NeverExits()
    {
        var clock = new FordsExitClock();

        Assert.AreEqual(FordsExit.None, clock.Tick(500f, failedStart: false, matchEnded: false));
        Assert.AreEqual(FordsExit.None, clock.Tick(500f, failedStart: false, matchEnded: false));
    }

    [TestMethod]
    public void Tick_AfterTheMatchEnded_ExitsOnceTwentyFiveSecondsOfMissionTimeHavePassed()
    {
        var clock = new FordsExitClock();

        Assert.AreEqual(FordsExit.None, clock.Tick(24.9f, failedStart: false, matchEnded: true));
        Assert.AreEqual(FordsExit.AfterResult, clock.Tick(0.2f, failedStart: false, matchEnded: true));
        Assert.AreEqual(FordsExit.None, clock.Tick(30f, failedStart: false, matchEnded: true), "the exit is asked for once");
    }

    [TestMethod]
    public void Tick_TheResultClock_StartsAtTheEndNotAtTheMissionStart()
    {
        var clock = new FordsExitClock();

        clock.Tick(600f, failedStart: false, matchEnded: false);

        Assert.AreEqual(FordsExit.None, clock.Tick(10f, failedStart: false, matchEnded: true));
    }

    [TestMethod]
    public void Tick_AFrameWithNoMissionTime_CountsForNothing()
    {
        var clock = new FordsExitClock();

        Assert.AreEqual(FordsExit.None, clock.Tick(0f, failedStart: false, matchEnded: true));
        Assert.AreEqual(FordsExit.None, clock.Tick(-5f, failedStart: false, matchEnded: true));
        Assert.AreEqual(FordsExit.None, clock.Tick(float.NaN, failedStart: false, matchEnded: true));
        Assert.AreEqual(FordsExit.None, clock.Tick(24f, failedStart: false, matchEnded: true), "a paused engine ticks dt 0 and must not run the clock");
    }

    [TestMethod]
    public void Tick_AFailedStart_ExitsAfterThreeSeconds()
    {
        var clock = new FordsExitClock();

        Assert.AreEqual(FordsExit.None, clock.Tick(2.9f, failedStart: true, matchEnded: false));
        Assert.AreEqual(FordsExit.AfterFailedStart, clock.Tick(0.2f, failedStart: true, matchEnded: false));
        Assert.AreEqual(FordsExit.None, clock.Tick(10f, failedStart: true, matchEnded: false));
    }

    [TestMethod]
    public void Tick_AFailedStartThatAlsoReportsAnEnd_UsesTheShortWait()
    {
        var clock = new FordsExitClock();

        Assert.AreEqual(FordsExit.AfterFailedStart, clock.Tick(3.5f, failedStart: true, matchEnded: true));
    }

    [TestMethod]
    public void Tick_AfterOneExit_NeverAsksAgain()
    {
        var clock = new FordsExitClock();
        clock.Tick(30f, failedStart: false, matchEnded: true);

        Assert.AreEqual(FordsExit.None, clock.Tick(30f, failedStart: true, matchEnded: true));
    }
}
