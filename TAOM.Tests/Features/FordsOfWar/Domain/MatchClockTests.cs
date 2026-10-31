using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Countdown, live time, and the minute the economy and respawn rules key on.</summary>
[TestClass]
public class MatchClockTests
{
    [TestMethod]
    public void New_WithCountdown_StartsInCountdown()
    {
        var c = new MatchClock(10f);

        Assert.AreEqual(MatchPhase.Countdown, c.Phase);
        Assert.AreEqual(0f, c.Elapsed);
        Assert.AreEqual(10f, c.CountdownRemaining, 1e-5f);
    }

    [TestMethod]
    public void New_ZeroCountdown_StartsLive()
    {
        Assert.AreEqual(MatchPhase.Live, new MatchClock(0f).Phase);
    }

    [TestMethod]
    public void Advance_DuringCountdown_DoesNotAccumulateElapsed()
    {
        var c = new MatchClock(10f);

        c.Advance(4f);

        Assert.AreEqual(MatchPhase.Countdown, c.Phase);
        Assert.AreEqual(0f, c.Elapsed);
        Assert.AreEqual(6f, c.CountdownRemaining, 1e-5f);
    }

    [TestMethod]
    public void Advance_PastCountdown_GoesLiveAndCarriesTheRemainder()
    {
        var c = new MatchClock(10f);

        c.Advance(12.5f);

        Assert.AreEqual(MatchPhase.Live, c.Phase);
        Assert.AreEqual(2.5f, c.Elapsed, 1e-5f);
        Assert.AreEqual(0f, c.CountdownRemaining);
    }

    [TestMethod]
    public void Minute_AfterNinetySecondsLive_IsOnePointFive()
    {
        var c = new MatchClock(0f);

        c.Advance(90f);

        Assert.AreEqual(1.5f, c.Minute, 1e-5f);
    }

    [TestMethod]
    public void Advance_NonPositiveOrNaN_ChangesNothing()
    {
        var c = new MatchClock(0f);
        c.Advance(5f);

        c.Advance(-3f);
        c.Advance(0f);
        c.Advance(float.NaN);

        Assert.AreEqual(5f, c.Elapsed, 1e-5f);
    }

    [TestMethod]
    public void End_FromLive_StopsTheClock()
    {
        var c = new MatchClock(0f);
        c.Advance(30f);

        c.End();
        c.Advance(30f);

        Assert.AreEqual(MatchPhase.Ended, c.Phase);
        Assert.AreEqual(30f, c.Elapsed, 1e-5f);
    }

    [TestMethod]
    public void End_DuringCountdown_EndsTheMatch()
    {
        var c = new MatchClock(10f);

        c.End();

        Assert.AreEqual(MatchPhase.Ended, c.Phase);
    }
}
