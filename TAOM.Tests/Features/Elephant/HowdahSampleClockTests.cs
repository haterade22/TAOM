using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// The howdah status line fires once per period of accumulated mission time (#627). dt comes from the engine, and a
/// NaN folded into the accumulator would never compare >= period again, silencing the log for the rest of the battle.
/// </summary>
[TestClass]
public class HowdahSampleClockTests
{
    [TestMethod]
    public void Tick_BeforeThePeriod_DoesNotFire()
    {
        var clock = new HowdahSampleClock(5f);
        Assert.IsFalse(clock.Tick(4.9f));
    }

    [TestMethod]
    public void Tick_ReachingThePeriod_FiresOnceThenRestarts()
    {
        var clock = new HowdahSampleClock(5f);
        Assert.IsFalse(clock.Tick(3f));
        Assert.IsTrue(clock.Tick(2f));
        Assert.IsFalse(clock.Tick(4.9f));
        Assert.IsTrue(clock.Tick(0.1f));
    }

    [TestMethod]
    public void Tick_OneLongHitch_FiresOnceNotInABurst()
    {
        var clock = new HowdahSampleClock(5f);
        Assert.IsTrue(clock.Tick(30f));
        Assert.IsFalse(clock.Tick(0.016f));
    }

    [TestMethod]
    public void Tick_NaNOrNegativeDt_IsIgnoredAndTheClockKeepsWorking()
    {
        var clock = new HowdahSampleClock(5f);
        Assert.IsFalse(clock.Tick(float.NaN));
        Assert.IsFalse(clock.Tick(-1f));
        Assert.IsFalse(clock.Tick(float.PositiveInfinity));
        Assert.IsTrue(clock.Tick(5f), "a bad dt must not poison the accumulator");
    }

    [TestMethod]
    public void Tick_FirstCall_CanFireImmediately_WhenAskedTo()
    {
        var clock = new HowdahSampleClock(5f, fireOnFirstTick: true);
        Assert.IsTrue(clock.Tick(0.016f), "the first status line lands on the first tick, not 5 s in");
        Assert.IsFalse(clock.Tick(0.016f));
    }
}
