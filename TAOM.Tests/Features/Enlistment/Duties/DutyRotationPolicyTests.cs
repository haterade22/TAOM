using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Duties;
using TAOM.Features.TroopProgression;

namespace TAOM.Tests.Features.Enlistment.Duties;

[TestClass]
public class DutyRotationPolicyTests
{
    private IRandomProvider _random = null!;
    private DutyRotationPolicy _policy = null!;

    [TestInitialize]
    public void Setup()
    {
        _random = Substitute.For<IRandomProvider>();
        _policy = new DutyRotationPolicy(_random);
    }

    private static SchedulerConfig Scheduler() => new SchedulerConfig
    {
        MinDaysBeforeFirstOffer = 3,
        OfferCooldownDaysQuiet = 4,
        OfferCooldownDaysPressure = 2,
        GuaranteedOfferDaysQuiet = 7,
        GuaranteedOfferDaysPressure = 4,
        BaseOfferChance = 0.06f,
        MaxOfferChance = 0.45f,
        IncidentMinDaysServed = 7,
        IncidentCooldownDays = 3,
    };

    [TestMethod]
    public void ShouldOfferDuty_BeforeMinDaysServed_ReturnsFalse()
    {
        var result = _policy.ShouldOfferDuty(Scheduler(), daysServed: 2, trust: 0, pressure: false, nowDays: 2, lastOfferDay: null);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ShouldOfferDuty_NeverOfferedAndMinDaysServedMet_ReturnsTrue()
    {
        var result = _policy.ShouldOfferDuty(Scheduler(), daysServed: 3, trust: 0, pressure: false, nowDays: 3, lastOfferDay: null);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ShouldOfferDuty_WithinQuietCooldown_ReturnsFalse()
    {
        var result = _policy.ShouldOfferDuty(Scheduler(), daysServed: 10, trust: 0, pressure: false, nowDays: 10.0, lastOfferDay: 8.0);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ShouldOfferDuty_WithinPressureCooldown_ReturnsFalse()
    {
        // Pressure cooldown is 2 days — 1 day since last offer is still inside it.
        var result = _policy.ShouldOfferDuty(Scheduler(), daysServed: 10, trust: 0, pressure: true, nowDays: 10.0, lastOfferDay: 9.0);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ShouldOfferDuty_PastQuietGuaranteedWindow_ReturnsTrueRegardlessOfRoll()
    {
        _random.Next(Arg.Any<int>()).Returns(999); // would fail any chance roll
        var result = _policy.ShouldOfferDuty(Scheduler(), daysServed: 20, trust: 0, pressure: false, nowDays: 20.0, lastOfferDay: 10.0);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ShouldOfferDuty_PastPressureGuaranteedWindow_ReturnsTrueRegardlessOfRoll()
    {
        _random.Next(Arg.Any<int>()).Returns(999);
        var result = _policy.ShouldOfferDuty(Scheduler(), daysServed: 20, trust: 0, pressure: true, nowDays: 14.0, lastOfferDay: 10.0);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ShouldOfferDuty_ChanceRollBelowThreshold_ReturnsTrue()
    {
        // Past cooldown (4 days quiet), before guaranteed window (7 days quiet): 5 days since last offer.
        _random.Next(1000).Returns(10); // base chance 0.06 -> threshold 60
        var result = _policy.ShouldOfferDuty(Scheduler(), daysServed: 20, trust: 0, pressure: false, nowDays: 15.0, lastOfferDay: 10.0);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ShouldOfferDuty_ChanceRollAboveThreshold_ReturnsFalse()
    {
        _random.Next(1000).Returns(999);
        var result = _policy.ShouldOfferDuty(Scheduler(), daysServed: 20, trust: 0, pressure: false, nowDays: 15.0, lastOfferDay: 10.0);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ShouldOfferDuty_TrustBonusRaisesChance_RollThatWouldFailAtZeroTrustNowPasses()
    {
        // base=0.06 -> threshold 60. Roll of 70 fails at trust 0, but trust 15 adds 0.15 -> chance 0.21 -> threshold 210.
        _random.Next(1000).Returns(70);
        var result = _policy.ShouldOfferDuty(Scheduler(), daysServed: 20, trust: 15, pressure: false, nowDays: 15.0, lastOfferDay: 10.0);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ShouldOfferDuty_ChanceClampedToMax_NeverExceedsMaxOfferChance()
    {
        // Huge trust would blow past 1.0 without the clamp; max is 0.45 -> threshold 450.
        _random.Next(1000).Returns(460);
        var result = _policy.ShouldOfferDuty(Scheduler(), daysServed: 20, trust: 1000, pressure: true, nowDays: 12.0, lastOfferDay: 10.0);
        Assert.IsFalse(result, "roll of 460 must fail once chance is clamped to 0.45 (threshold 450)");
    }

    [TestMethod]
    public void ShouldRollIncident_BeforeMinDaysServed_ReturnsFalse()
    {
        Assert.IsFalse(_policy.ShouldRollIncident(Scheduler(), daysServed: 6, nowDays: 6, lastIncidentDay: null));
    }

    [TestMethod]
    public void ShouldRollIncident_NeverRolledAndMinDaysServedMet_ReturnsTrue()
    {
        Assert.IsTrue(_policy.ShouldRollIncident(Scheduler(), daysServed: 7, nowDays: 7, lastIncidentDay: null));
    }

    [TestMethod]
    public void ShouldRollIncident_WithinCooldown_ReturnsFalse()
    {
        Assert.IsFalse(_policy.ShouldRollIncident(Scheduler(), daysServed: 20, nowDays: 11.0, lastIncidentDay: 10.0));
    }

    [TestMethod]
    public void ShouldRollIncident_PastCooldown_ReturnsTrue()
    {
        Assert.IsTrue(_policy.ShouldRollIncident(Scheduler(), daysServed: 20, nowDays: 14.0, lastIncidentDay: 10.0));
    }
}
