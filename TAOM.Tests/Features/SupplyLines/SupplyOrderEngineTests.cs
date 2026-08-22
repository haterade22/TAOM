using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SupplyLines;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// Every verdict branch of the hourly order decision, including the two source-defect fixes:
/// deliveries are gated on the player not being in an encounter (the source delivered recruits
/// into sieges via its 2x-timeout path), and non-finite elapsed/distance values take the safe
/// Continue branch instead of triggering a delivery (-Infinity distance and +Infinity elapsed
/// would both pass a naive comparison).
/// </summary>
[TestClass]
public class SupplyOrderEngineTests
{
    private const float FarAway = 100f;
    private const float OnTime = 0.5f;

    private SupplyOrderEngine _sut;

    [TestInitialize]
    public void Setup()
    {
        _sut = new SupplyOrderEngine();
    }

    private SupplyOrderVerdict Advance(
        float elapsedFraction = OnTime,
        bool caravanExists = true,
        bool caravanInRaidEvent = false,
        float distanceToPlayer = FarAway,
        bool playerInEncounter = false)
    {
        return _sut.Advance(elapsedFraction, caravanExists, caravanInRaidEvent, distanceToPlayer, playerInEncounter);
    }

    // ---- Loss branches ----

    [TestMethod]
    public void Advance_CaravanMissing_Loses()
    {
        Assert.AreEqual(SupplyOrderVerdict.Lose, Advance(caravanExists: false));
    }

    [TestMethod]
    public void Advance_CaravanRaided_Loses()
    {
        Assert.AreEqual(SupplyOrderVerdict.Lose, Advance(caravanInRaidEvent: true));
    }

    [TestMethod]
    public void Advance_CaravanMissingAndRaidFlagSet_Loses()
    {
        // Both loss inputs at once still resolve to Lose; there is no state where one masks
        // the other into a delivery.
        Assert.AreEqual(SupplyOrderVerdict.Lose, Advance(caravanExists: false, caravanInRaidEvent: true));
    }

    [TestMethod]
    public void Advance_RaidedWhileCaravanInDeliveryRange_LossOutranksDelivery()
    {
        // A raided caravan sitting next to the player is still gone; proximity must not save it.
        Assert.AreEqual(SupplyOrderVerdict.Lose, Advance(caravanInRaidEvent: true, distanceToPlayer: 0.5f));
    }

    [TestMethod]
    public void Advance_RaidedWhilePlayerInEncounter_StillLoses()
    {
        // The encounter gate only holds DELIVERIES back; a loss is a fact about the caravan.
        Assert.AreEqual(SupplyOrderVerdict.Lose, Advance(caravanInRaidEvent: true, playerInEncounter: true));
    }

    // ---- Delivery by proximity ----

    [TestMethod]
    public void Advance_CaravanWithinRange_Delivers()
    {
        Assert.AreEqual(SupplyOrderVerdict.Deliver, Advance(distanceToPlayer: 0.5f));
    }

    [TestMethod]
    public void Advance_CaravanExactlyAtRange_Delivers()
    {
        Assert.AreEqual(SupplyOrderVerdict.Deliver, Advance(distanceToPlayer: _sut.DeliveryRange));
    }

    [TestMethod]
    public void Advance_CaravanJustOutsideRange_Continues()
    {
        Assert.AreEqual(SupplyOrderVerdict.Continue, Advance(distanceToPlayer: _sut.DeliveryRange + 0.01f));
    }

    [TestMethod]
    public void Advance_WithinRangeButPlayerInEncounter_Continues()
    {
        // Defect fix: the source handed cargo over regardless of the player being mid-battle.
        Assert.AreEqual(
            SupplyOrderVerdict.Continue,
            Advance(distanceToPlayer: 0.5f, playerInEncounter: true));
    }

    // ---- Force delivery (stuck-caravan failsafe) ----

    [TestMethod]
    public void Advance_ElapsedAtForceFraction_Delivers()
    {
        Assert.AreEqual(SupplyOrderVerdict.Deliver, Advance(elapsedFraction: _sut.ForceDeliverFraction));
    }

    [TestMethod]
    public void Advance_ElapsedBeyondForceFraction_Delivers()
    {
        Assert.AreEqual(SupplyOrderVerdict.Deliver, Advance(elapsedFraction: 3f));
    }

    [TestMethod]
    public void Advance_ElapsedJustBelowForceFraction_Continues()
    {
        Assert.AreEqual(SupplyOrderVerdict.Continue, Advance(elapsedFraction: _sut.ForceDeliverFraction - 0.01f));
    }

    [TestMethod]
    public void Advance_ForceFractionReachedButPlayerInEncounter_Continues()
    {
        // Defect fix: this is the exact source path that delivered recruits into a siege.
        Assert.AreEqual(
            SupplyOrderVerdict.Continue,
            Advance(elapsedFraction: 3f, playerInEncounter: true));
    }

    // ---- Nominal continue ----

    [TestMethod]
    public void Advance_FarAwayAndOnTime_Continues()
    {
        Assert.AreEqual(SupplyOrderVerdict.Continue, Advance());
    }

    [TestMethod]
    public void Advance_UnknownDistanceSentinel_Continues()
    {
        // float.MaxValue is the documented "distance unknown" input; it is finite and far.
        Assert.AreEqual(SupplyOrderVerdict.Continue, Advance(distanceToPlayer: float.MaxValue));
    }

    // ---- NaN/Infinity polarity (safe branch is Continue) ----

    [TestMethod]
    public void Advance_NaNDistance_Continues()
    {
        Assert.AreEqual(SupplyOrderVerdict.Continue, Advance(distanceToPlayer: float.NaN));
    }

    [TestMethod]
    public void Advance_NegativeInfinityDistance_Continues()
    {
        // -Infinity passes a naive "<= range" comparison; the finiteness gate must reject it.
        Assert.AreEqual(SupplyOrderVerdict.Continue, Advance(distanceToPlayer: float.NegativeInfinity));
    }

    [TestMethod]
    public void Advance_PositiveInfinityDistance_Continues()
    {
        Assert.AreEqual(SupplyOrderVerdict.Continue, Advance(distanceToPlayer: float.PositiveInfinity));
    }

    [TestMethod]
    public void Advance_NaNElapsedFraction_Continues()
    {
        Assert.AreEqual(SupplyOrderVerdict.Continue, Advance(elapsedFraction: float.NaN));
    }

    [TestMethod]
    public void Advance_PositiveInfinityElapsedFraction_Continues()
    {
        // +Infinity passes a naive ">= fraction" comparison; the finiteness gate must reject it.
        Assert.AreEqual(SupplyOrderVerdict.Continue, Advance(elapsedFraction: float.PositiveInfinity));
    }

    [TestMethod]
    public void Advance_NaNInputsWithMissingCaravan_StillLoses()
    {
        // The loss branches read only bools; corrupt floats must not shadow a real loss.
        Assert.AreEqual(
            SupplyOrderVerdict.Lose,
            Advance(elapsedFraction: float.NaN, caravanExists: false, distanceToPlayer: float.NaN));
    }

    // ---- Constants ----

    [TestMethod]
    public void DeliveryRange_MatchesSourceMeetingDistance()
    {
        Assert.AreEqual(1.2f, _sut.DeliveryRange);
    }

    [TestMethod]
    public void ForceDeliverFraction_MatchesSourceHourlyTimeout()
    {
        Assert.AreEqual(2f, _sut.ForceDeliverFraction);
    }
}
