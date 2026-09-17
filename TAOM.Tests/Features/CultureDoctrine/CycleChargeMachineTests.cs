using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Doctrines;

namespace TAOM.Tests.Features.CultureDoctrine;

[TestClass]
public class CycleChargeMachineTests
{
    private static readonly CycleTunables T = CycleTunables.Default;

    private static ChargeReading Reading(bool hasTarget = true, float distance = 100f, float stop = 40f, bool passed = false, bool gathered = false, float seconds = 0f)
        => new ChargeReading(hasTarget, distance, stop, passed, gathered, seconds);

    [TestMethod]
    public void StartsCharging_AndStaysWhileClosing()
    {
        var m = new CycleChargeMachine();
        Assert.AreEqual(ChargeStage.Charging, m.Stage);
        Assert.IsFalse(m.Step(Reading(seconds: 3f), T));
        Assert.AreEqual(ChargeStage.Charging, m.Stage);
    }

    [TestMethod]
    public void Charging_PassesTheTarget_RidesThrough()
    {
        var m = new CycleChargeMachine();
        Assert.IsTrue(m.Step(Reading(distance: 5f, passed: true), T));
        Assert.AreEqual(ChargeStage.RidingThrough, m.Stage);
    }

    [TestMethod]
    public void Charging_BoggedDownPastTheCap_RidesThroughAnyway()
    {
        var m = new CycleChargeMachine();
        Assert.IsFalse(m.Step(Reading(distance: 8f, seconds: 10f), T), "at the cap, not past it");
        Assert.IsTrue(m.Step(Reading(distance: 8f, seconds: 10.5f), T));
        Assert.AreEqual(ChargeStage.RidingThrough, m.Stage);
    }

    [TestMethod]
    public void RidingThrough_ClearOfTheTarget_Reforms()
    {
        var m = new CycleChargeMachine();
        m.Step(Reading(passed: true), T);
        Assert.IsFalse(m.Step(Reading(distance: 30f, stop: 40f, seconds: 1f), T));
        Assert.IsTrue(m.Step(Reading(distance: 40f, stop: 40f, seconds: 2f), T));
        Assert.AreEqual(ChargeStage.Reforming, m.Stage);
    }

    [TestMethod]
    public void RidingThrough_TimesOut_Reforms()
    {
        var m = new CycleChargeMachine();
        m.Step(Reading(passed: true), T);
        Assert.IsTrue(m.Step(Reading(distance: 10f, stop: 50f, seconds: 5.5f), T));
        Assert.AreEqual(ChargeStage.Reforming, m.Stage);
    }

    [TestMethod]
    public void Reforming_GatheredAfterTheMinimum_ChargesAgain()
    {
        var m = ReformingMachine();
        Assert.IsFalse(m.Step(Reading(distance: 60f, gathered: true, seconds: 1f), T), "not before the minimum");
        Assert.IsTrue(m.Step(Reading(distance: 60f, gathered: true, seconds: 2f), T));
        Assert.AreEqual(ChargeStage.Charging, m.Stage);
    }

    [TestMethod]
    public void Reforming_NeverGathers_ChargesAtTheMaximum()
    {
        var m = ReformingMachine();
        Assert.IsFalse(m.Step(Reading(distance: 60f, seconds: 7.9f), T));
        Assert.IsTrue(m.Step(Reading(distance: 60f, seconds: 8f), T));
        Assert.AreEqual(ChargeStage.Charging, m.Stage);
    }

    [TestMethod]
    public void Reforming_EnemyComesToUs_ChargesAtOnce()
    {
        var m = ReformingMachine();
        Assert.IsTrue(m.Step(Reading(distance: 19f, stop: 40f, seconds: 0.5f), T), "inside half the stop distance");
        Assert.AreEqual(ChargeStage.Charging, m.Stage);
    }

    [TestMethod]
    public void Reforming_IsNotSkipped_WhenTheReformPointSitsInsideTheContactDistance()
    {
        // Found in review 2026-09-17: with vanilla's 20 m floor and a 30 m contact distance a
        // charge that began inside 30 m reformed for one tick. The contact threshold is now
        // never more than half the stop distance, and the floor is 35 m for horse.
        Assert.IsTrue(T.MinStopDistance > T.ContactDistance);
        Assert.AreEqual(17.5f, T.ReformContactDistance(35f), 1e-4f);
        Assert.AreEqual(30f, T.ReformContactDistance(80f), 1e-4f, "capped at the contact distance");
        Assert.AreEqual(30f, T.ReformContactDistance(float.NaN), 1e-4f);
        var m = ReformingMachine();
        Assert.IsFalse(m.Step(Reading(distance: 35f, stop: 35f, seconds: 0.1f), T), "at the reform point, not charging");
        Assert.AreEqual(ChargeStage.Reforming, m.Stage);
    }

    [TestMethod]
    public void NoTarget_FallsBackToCharging_FromAnyStage()
    {
        var m = ReformingMachine();
        Assert.IsTrue(m.Step(Reading(hasTarget: false), T));
        Assert.AreEqual(ChargeStage.Charging, m.Stage);
    }

    [TestMethod]
    public void NaNInputs_NeverAdvanceAStage()
    {
        var m = new CycleChargeMachine();
        Assert.IsFalse(m.Step(Reading(distance: float.NaN, seconds: float.NaN), T));
        m.Step(Reading(passed: true), T);
        Assert.IsFalse(m.Step(Reading(distance: float.NaN, stop: float.NaN, seconds: float.NaN), T));
        Assert.AreEqual(ChargeStage.RidingThrough, m.Stage);
    }

    [TestMethod]
    public void StopDistance_IsTheChargeDistanceClamped()
    {
        Assert.AreEqual(35f, T.StopDistanceFor(5f));
        Assert.AreEqual(45f, T.StopDistanceFor(45f));
        Assert.AreEqual(60f, T.StopDistanceFor(400f));
        Assert.AreEqual(35f, T.StopDistanceFor(float.NaN));
    }

    private static CycleChargeMachine ReformingMachine()
    {
        var m = new CycleChargeMachine();
        m.Step(Reading(passed: true), T);
        m.Step(Reading(distance: 45f, stop: 40f, seconds: 1f), T);
        Assert.AreEqual(ChargeStage.Reforming, m.Stage);
        return m;
    }
}
