using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>One self-firing timer per commander ability (phase2-plan.md section 2): a first delay, at most one fire per tick, a cooldown from the fire, a terminal stop.</summary>
[TestClass]
public class CommanderClockTests
{
    [TestMethod]
    public void NothingFiresBeforeTheFirstDelay_ThenItFiresOnce()
    {
        var clock = new CommanderClock(now: 100f, firstSeconds: 30f, cooldownSeconds: 120f);

        Assert.IsFalse(clock.Tick(110f));
        Assert.IsFalse(clock.Tick(129.9f));
        Assert.IsTrue(clock.Tick(130f));
        Assert.IsFalse(clock.Tick(130.5f), "the cooldown runs from the fire");
    }

    [TestMethod]
    public void ALongStep_FiresOnlyOnce_AndTheNextReadyTimeCountsFromThatTick()
    {
        var clock = new CommanderClock(0f, 10f, 20f);

        Assert.IsTrue(clock.Tick(500f), "a step of 500 s is one use, not twenty");
        Assert.IsFalse(clock.Tick(500f));
        Assert.IsFalse(clock.Tick(519.9f));
        Assert.IsTrue(clock.Tick(520f));
    }

    [TestMethod]
    public void AfterStop_NeverFiresAgain_AndNoTimeIsLeft()
    {
        var clock = new CommanderClock(0f, 10f, 20f);
        Assert.IsTrue(clock.Tick(10f));

        clock.Stop();

        Assert.IsTrue(clock.Stopped);
        Assert.IsFalse(clock.Tick(10000f));
        Assert.AreEqual(0f, clock.SecondsLeft(10000f));
    }

    [TestMethod]
    public void StoppedBeforeTheFirstFire_NeverFires()
    {
        var clock = new CommanderClock(0f, 10f, 20f);

        clock.Stop();

        Assert.IsFalse(clock.Tick(50f));
    }

    [TestMethod]
    public void SecondsLeft_CountsDownAndStopsAtZero()
    {
        var clock = new CommanderClock(5f, 30f, 60f);

        Assert.AreEqual(30f, clock.SecondsLeft(5f), 1e-4f);
        Assert.AreEqual(12f, clock.SecondsLeft(23f), 1e-4f);
        Assert.AreEqual(0f, clock.SecondsLeft(99f));
    }

    [TestMethod]
    public void ANaNClock_NeverFires()
    {
        var clock = new CommanderClock(0f, 1f, 1f);

        Assert.IsFalse(clock.Tick(float.NaN));
        Assert.IsFalse(clock.Tick(float.NegativeInfinity));
    }
}
