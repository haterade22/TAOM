using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroDefence.Domain;

namespace TAOM.Tests.Features.HeroDefence.Domain;

[TestClass]
public class PackStallClockTests
{
    private const int Pack = 20;

    private static readonly PackStallLimits Limits = new();

    private static PackStallClock Started(float progress = 0f) => new(Limits, 0f, progress, Pack);

    [TestMethod]
    public void Limits_Defaults_AreFortySecondsAndFiveMetres()
    {
        Assert.AreEqual(40f, Limits.WindowSeconds);
        Assert.AreEqual(5f, Limits.StepMetres);
    }

    [TestMethod]
    public void Observe_NoProgressAndNoBlow_StallsAtTheWindow()
    {
        var clock = Started();

        Assert.IsFalse(clock.Observe(39.9f, 0f, Pack, false));
        Assert.IsTrue(clock.Observe(40f, 0f, Pack, false));
    }

    [TestMethod]
    public void Observe_ProgressOfOneStep_RestartsTheWindow()
    {
        var clock = Started();

        clock.Observe(30f, 5f, Pack, false);

        Assert.IsFalse(clock.Observe(69.9f, 5f, Pack, false));
        Assert.IsTrue(clock.Observe(70f, 5f, Pack, false));
    }

    [TestMethod]
    public void Observe_ProgressBelowOneStep_DoesNotRestartTheWindow()
    {
        var clock = Started();

        clock.Observe(30f, 4.9f, Pack, false);

        Assert.IsTrue(clock.Observe(40f, 4.9f, Pack, false));
    }

    [TestMethod]
    public void Observe_SmallGainsThatAddUpToOneStep_RestartTheWindow()
    {
        var clock = Started();

        clock.Observe(20f, 3f, Pack, false);
        clock.Observe(35f, 5.5f, Pack, false);

        Assert.IsFalse(clock.Observe(74.9f, 5.5f, Pack, false));
        Assert.IsTrue(clock.Observe(75f, 5.5f, Pack, false));
    }

    [TestMethod]
    public void Observe_ARegainAfterBeingPushedBack_IsNotProgress()
    {
        var clock = Started();

        clock.Observe(10f, -10f, Pack, false);
        clock.Observe(20f, -4f, Pack, false);

        Assert.IsTrue(clock.Observe(40f, -4f, Pack, false), "a regain to below the mark is no forward progress");
    }

    [TestMethod]
    public void Observe_ABlowGivenOrTaken_RestartsTheWindow()
    {
        var clock = Started();

        clock.Observe(30f, 0f, Pack, true);

        Assert.IsFalse(clock.Observe(69.9f, 0f, Pack, false));
        Assert.IsTrue(clock.Observe(70f, 0f, Pack, false));
    }

    [TestMethod]
    public void Observe_PushedBackButTradingBlows_NeverStalls()
    {
        var clock = Started();

        for (var t = 10f; t <= 120f; t += 10f)
            Assert.IsFalse(clock.Observe(t, -t, Pack, true), $"t={t}");
    }

    [TestMethod]
    public void Observe_TheLeadCreepLeaves_TheMeanDropIsNotHeldAgainstThePack()
    {
        var clock = new PackStallClock(Limits, 0f, 100f, 3);

        clock.Observe(10f, 95f, 2, false);
        clock.Observe(20f, 101f, 2, false);

        Assert.IsFalse(clock.Observe(59.9f, 101f, 2, false), "the two left gained 6 m after the lead left, so the window restarted at 20");
        Assert.IsTrue(clock.Observe(60f, 101f, 2, false));
    }

    [TestMethod]
    public void Observe_ACreepLeavingOrFalling_IsNotActivityByItself()
    {
        var clock = new PackStallClock(Limits, 0f, 100f, 3);

        Assert.IsFalse(clock.Observe(30f, 120f, 2, false));

        Assert.IsTrue(clock.Observe(40f, 120f, 2, false), "the jump in the mean came from the count change, not from a march");
    }

    [TestMethod]
    public void Observe_CountChangesBeforeEachStep_TheGainSoFarStillCounts()
    {
        var clock = new PackStallClock(Limits, 0f, 100f, 30);

        for (var t = 1; t <= 50; t++)
            Assert.IsFalse(clock.Observe(t, 100f + 4f * t, 30 - t / 2, false), $"t={t}: the pack marches 4 m a second while a creep leaves every 2 s");
    }

    [TestMethod]
    public void Observe_NaNProgress_CountsAsNoProgress()
    {
        var clock = Started();

        clock.Observe(30f, float.NaN, Pack, false);

        Assert.IsTrue(clock.Observe(40f, float.NaN, Pack, false));
    }

    [TestMethod]
    public void Observe_NaNProgressForTheWholeLife_StallsAtTheWindow()
    {
        var clock = Started(float.NaN);

        Assert.IsFalse(clock.Observe(20f, float.NaN, Pack, false));
        Assert.IsTrue(clock.Observe(40f, float.NaN, Pack, false));
    }

    [TestMethod]
    public void Observe_NaNStartProgress_TakesTheFirstFiniteReadingAsTheMark()
    {
        var clock = Started(float.NaN);

        clock.Observe(10f, 100f, Pack, false);
        clock.Observe(30f, 105f, Pack, false);

        Assert.IsFalse(clock.Observe(69.9f, 105f, Pack, false));
        Assert.IsTrue(clock.Observe(70f, 105f, Pack, false));
    }

    [TestMethod]
    public void Observe_NaNStartTime_StartsTheWindowAtTheFirstFiniteTime()
    {
        var clock = new PackStallClock(Limits, float.NaN, 0f, Pack);

        Assert.IsFalse(clock.Observe(10f, 0f, Pack, false));
        Assert.IsFalse(clock.Observe(49.9f, 0f, Pack, false));
        Assert.IsTrue(clock.Observe(50f, 0f, Pack, false));
    }

    [TestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void Observe_NonFiniteTime_ChangesNothing(float now)
    {
        var clock = Started();

        Assert.IsFalse(clock.Observe(now, 50f, Pack, true));
        Assert.IsTrue(clock.Observe(40f, 0f, Pack, false), "the bad reading did not restart the window");

        clock.Observe(41f, 5f, Pack, false);
        Assert.IsFalse(clock.Observe(80.9f, 5f, Pack, false), "5 m over the old mark of 0 is a step, so the bad reading did not move the mark");
    }
}
