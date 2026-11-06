using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The timer of one volley source (phase3-plan.md D30): the domain arms a shot, and the cooldown starts only on a shot that actually fired, so a withheld shot keeps the volley ready and asks again
/// after a second. Unlike <see cref="CommanderClock"/>, asking never spends the use.
/// </summary>
[TestClass]
public class VolleyClockTests
{
    [TestMethod]
    public void NothingIsReadyBeforeTheFirstDelay_ThenItIsReady_AndStaysReadyUntilItFires()
    {
        var clock = new VolleyClock(now: 100f, firstSeconds: 20f, cooldownSeconds: 30f);

        Assert.IsFalse(clock.Ready(100f));
        Assert.IsFalse(clock.Ready(119.9f));
        Assert.IsTrue(clock.Ready(120f));
        Assert.IsTrue(clock.Ready(120f), "asking is not firing: the same answer twice");
        Assert.IsTrue(clock.Ready(400f));
    }

    [TestMethod]
    public void AFirstDelayOfZero_IsReadyAtOnce()
    {
        Assert.IsTrue(new VolleyClock(5f, 0f, 45f).Ready(5f));
    }

    [TestMethod]
    public void AfterItFires_NoSecondFireInTheSameTick_AndTheCooldownRunsFromThatTick()
    {
        var clock = new VolleyClock(0f, 20f, 30f);
        Assert.IsTrue(clock.Ready(25f));

        clock.Fired(25f);

        Assert.IsFalse(clock.Ready(25f), "one fire per tick");
        Assert.IsFalse(clock.Ready(54.9f));
        Assert.IsTrue(clock.Ready(55f));
    }

    [TestMethod]
    public void ALongStepAfterTheReadyTime_IsStillOneUse_AndTheCooldownCountsFromTheFire()
    {
        var clock = new VolleyClock(0f, 10f, 20f);

        Assert.IsTrue(clock.Ready(500f));
        clock.Fired(500f);

        Assert.IsFalse(clock.Ready(519.9f));
        Assert.IsTrue(clock.Ready(520f));
    }

    [TestMethod]
    public void AWithheldShot_KeepsTheVolleyReady_AndRetriesAfterOneSecond_WithoutSpendingTheCooldown()
    {
        var clock = new VolleyClock(0f, 20f, 30f);
        Assert.IsTrue(clock.Ready(20f));

        clock.Withheld(20f);

        Assert.IsFalse(clock.Ready(20f));
        Assert.IsFalse(clock.Ready(20.9f), "not asked again for a second, so a wave with no targets costs one read a second");
        Assert.IsTrue(clock.Ready(21f), "the cooldown was not spent: ready again after the second, not after 30 s");
        Assert.AreEqual(0f, clock.SecondsLeft(21f), "a retry hold is not a cooldown");
    }

    [TestMethod]
    public void WithheldTwice_HoldsTwiceFromEachCall()
    {
        var clock = new VolleyClock(0f, 0f, 30f);
        clock.Withheld(10f);
        Assert.IsTrue(clock.Ready(11f));

        clock.Withheld(11f);

        Assert.IsFalse(clock.Ready(11.5f));
        Assert.IsTrue(clock.Ready(12f));
    }

    [TestMethod]
    public void AFireAfterAWithhold_StartsTheFullCooldown_AndClearsTheHold()
    {
        var clock = new VolleyClock(0f, 0f, 30f);
        clock.Withheld(10f);

        clock.Fired(12f);

        Assert.IsFalse(clock.Ready(41.9f));
        Assert.IsTrue(clock.Ready(42f));
    }

    [TestMethod]
    public void SecondsLeft_IsTheTimeToTheNextUse_AtLeastZero()
    {
        var clock = new VolleyClock(100f, 20f, 30f);

        Assert.AreEqual(20f, clock.SecondsLeft(100f), 1e-4f);
        Assert.AreEqual(0f, clock.SecondsLeft(130f));
        clock.Fired(130f);
        Assert.AreEqual(30f, clock.SecondsLeft(130f), 1e-4f);
        Assert.AreEqual(10f, clock.SecondsLeft(150f), 1e-4f);
    }

    [TestMethod]
    public void AfterStop_NeverReadyAgain_AndNoTimeIsLeft()
    {
        var clock = new VolleyClock(0f, 0f, 30f);
        Assert.IsTrue(clock.Ready(1f));

        clock.Stop();

        Assert.IsTrue(clock.Stopped);
        Assert.IsFalse(clock.Ready(1f));
        Assert.IsFalse(clock.Ready(9999f));
        Assert.AreEqual(0f, clock.SecondsLeft(1f));
    }

    [TestMethod]
    public void ATimeThatIsNotANumber_IsNeverReady_AndNeverSpendsTheClock()
    {
        var clock = new VolleyClock(0f, 0f, 30f);

        Assert.IsFalse(clock.Ready(float.NaN));
        clock.Fired(float.NaN);
        clock.Withheld(float.PositiveInfinity);

        Assert.IsTrue(clock.Ready(1f), "bad times changed nothing");
    }
}
