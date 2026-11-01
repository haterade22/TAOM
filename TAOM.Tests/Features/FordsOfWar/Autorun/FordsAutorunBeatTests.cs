using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Autorun;

namespace TAOM.Tests.Features.FordsOfWar.Autorun;

[TestClass]
public class FordsAutorunBeatTests
{
    [TestMethod]
    public void ClockLine_BeforeThirtyWallSeconds_IsNull()
    {
        var beat = new FordsAutorunBeat();
        beat.StartClock(10.0, 100.0);

        Assert.IsNull(beat.ClockLine(39.9, 129.9, 20.0));
    }

    [TestMethod]
    public void ClockLine_AfterThirtyWallSeconds_NamesTheMissionSecondsPerWallSecond()
    {
        var beat = new FordsAutorunBeat();
        beat.StartClock(10.0, 100.0);

        var line = beat.ClockLine(40.0, 130.0, 20.0);

        Assert.AreEqual("clock wall=40.0 mt=130.0 live=20.0 ratio=1.00", line);
    }

    [TestMethod]
    public void ClockLine_AMissionThePauseHeld_ReportsARatioNearZero()
    {
        var beat = new FordsAutorunBeat();
        beat.StartClock(0.0, 50.0);

        var line = beat.ClockLine(30.0, 50.3, 0.0);

        StringAssert.EndsWith(line, "ratio=0.01");
    }

    [TestMethod]
    public void ClockLine_AfterALine_RestartsTheThirtySecondWindow()
    {
        var beat = new FordsAutorunBeat();
        beat.StartClock(0.0, 0.0);
        Assert.IsNotNull(beat.ClockLine(30.0, 30.0, 30.0));

        Assert.IsNull(beat.ClockLine(59.9, 59.9, 59.9));
        StringAssert.EndsWith(beat.ClockLine(60.0, 90.0, 60.0), "ratio=2.00");
    }

    [TestMethod]
    public void StatusDue_FromAFreshBeat_IsTrueEveryFiveWallSeconds()
    {
        var beat = new FordsAutorunBeat();

        Assert.IsFalse(beat.StatusDue(4.9));
        Assert.IsTrue(beat.StatusDue(5.0));
        Assert.IsFalse(beat.StatusDue(9.9));
        Assert.IsTrue(beat.StatusDue(10.0));
    }
}
