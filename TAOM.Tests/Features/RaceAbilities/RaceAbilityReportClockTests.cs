using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RaceAbilities;

// When the battle report goes to the log: every 30 s of mission time, and only after new activity.

namespace TAOM.Tests.Features.RaceAbilities;

[TestClass]
public class RaceAbilityReportClockTests
{
    private RaceAbilityReportClock _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new RaceAbilityReportClock();

    [TestMethod]
    public void Due_BeforeTheFirstInterval_IsFalse() =>
        Assert.IsFalse(_sut.Due(RaceAbilityReportClock.IntervalSeconds - 1f, activations: 5));

    [TestMethod]
    public void Due_AtTheIntervalWithNewActivity_IsTrue() =>
        Assert.IsTrue(_sut.Due(RaceAbilityReportClock.IntervalSeconds, activations: 5));

    [TestMethod]
    public void Due_NoActivitySinceTheLastReport_IsFalse()
    {
        _sut.Due(RaceAbilityReportClock.IntervalSeconds, activations: 5);

        Assert.IsFalse(_sut.Due(2f * RaceAbilityReportClock.IntervalSeconds, activations: 5));
    }

    [TestMethod]
    public void Due_NoActivityAtAll_IsFalse() =>
        Assert.IsFalse(_sut.Due(RaceAbilityReportClock.IntervalSeconds, activations: 0));

    [TestMethod]
    public void Due_InsideTheInterval_IsFalse()
    {
        _sut.Due(RaceAbilityReportClock.IntervalSeconds, activations: 5);

        Assert.IsFalse(_sut.Due(RaceAbilityReportClock.IntervalSeconds + 1f, activations: 9));
    }

    [TestMethod]
    public void Due_NaNTime_IsFalse() => Assert.IsFalse(_sut.Due(float.NaN, activations: 5));

    [TestMethod]
    public void Reset_StartsAgain()
    {
        _sut.Due(RaceAbilityReportClock.IntervalSeconds, activations: 5);

        _sut.Reset();

        Assert.IsTrue(_sut.Due(RaceAbilityReportClock.IntervalSeconds, activations: 5));
    }

    // IsDue lets the ticker skip counting the activations; it must agree with Due's own time gate.
    [TestMethod]
    public void IsDue_BeforeTheInterval_IsFalse() =>
        Assert.IsFalse(_sut.IsDue(RaceAbilityReportClock.IntervalSeconds - 1f));

    [TestMethod]
    public void IsDue_AtTheInterval_IsTrue() =>
        Assert.IsTrue(_sut.IsDue(RaceAbilityReportClock.IntervalSeconds));

    [TestMethod]
    public void IsDue_NaNTime_IsFalse() => Assert.IsFalse(_sut.IsDue(float.NaN));

    [TestMethod]
    public void IsDue_ChangesNothing()
    {
        _sut.IsDue(RaceAbilityReportClock.IntervalSeconds);

        Assert.IsTrue(_sut.Due(RaceAbilityReportClock.IntervalSeconds, activations: 5));
    }

    [TestMethod]
    public void IsDue_AfterAReport_WaitsAFullInterval()
    {
        _sut.Due(RaceAbilityReportClock.IntervalSeconds, activations: 5);

        Assert.IsFalse(_sut.IsDue(2f * RaceAbilityReportClock.IntervalSeconds - 1f));
        Assert.IsTrue(_sut.IsDue(2f * RaceAbilityReportClock.IntervalSeconds));
    }
}
