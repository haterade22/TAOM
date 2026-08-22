using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SupplyLines.Domain;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// The SupplyOrder POCO's own logic: enum round-trips through the int-backed save fields,
/// the lord-vs-settlement source discriminator, and the ElapsedFraction guard path.
///
/// The live ElapsedFraction path divides <c>DispatchTime.ElapsedHoursUntilNow</c> by
/// PlannedHours; ElapsedHoursUntilNow reads <c>Campaign.Current.MapTimeTracker</c> (verified
/// v1.4.8), which does not exist in a test host, so that arm is entry-point territory. The
/// division's consumers (SupplyOrderEngine) are fully covered against injected fractions.
/// </summary>
[TestClass]
public class SupplyOrderTests
{
    // ---- ElapsedFraction guard: no planned time means "already arrived" ----

    [TestMethod]
    public void ElapsedFraction_ZeroPlannedHours_ReturnsOne()
    {
        // The source computed 99.0 for a degenerate schedule, which force-delivered instantly
        // through the 2x timeout. 1.0 keeps "nominally arrived" without the magic number.
        var order = new SupplyOrder { PlannedHours = 0f };

        Assert.AreEqual(1f, order.ElapsedFraction());
    }

    [TestMethod]
    public void ElapsedFraction_NegativePlannedHours_ReturnsOne()
    {
        var order = new SupplyOrder { PlannedHours = -3f };

        Assert.AreEqual(1f, order.ElapsedFraction());
    }

    // ---- Enum round-trips through the int save fields ----

    [TestMethod]
    public void StatusEnum_SetEachValue_RoundTripsThroughBackingInt()
    {
        var order = new SupplyOrder();

        foreach (SupplyOrderStatus status in new[]
        {
            SupplyOrderStatus.Ordered,
            SupplyOrderStatus.InTransit,
            SupplyOrderStatus.Delivered,
            SupplyOrderStatus.Lost,
        })
        {
            order.StatusEnum = status;

            Assert.AreEqual((int)status, order.Status);
            Assert.AreEqual(status, order.StatusEnum);
        }
    }

    [TestMethod]
    public void StatusEnum_DefaultOrder_IsOrdered()
    {
        // A freshly constructed order (backing int 0) must read as Ordered, the enum's zero.
        Assert.AreEqual(SupplyOrderStatus.Ordered, new SupplyOrder().StatusEnum);
    }

    [TestMethod]
    public void EscortEnum_SetEachValue_RoundTripsThroughBackingInt()
    {
        var order = new SupplyOrder();

        foreach (SupplyEscortOption escort in new[]
        {
            SupplyEscortOption.None,
            SupplyEscortOption.Mercenaries,
            SupplyEscortOption.Companion,
        })
        {
            order.EscortEnum = escort;

            Assert.AreEqual((int)escort, order.Escort);
            Assert.AreEqual(escort, order.EscortEnum);
        }
    }

    [TestMethod]
    public void EscortEnum_DefaultOrder_IsNone()
    {
        Assert.AreEqual(SupplyEscortOption.None, new SupplyOrder().EscortEnum);
    }

    // ---- IsFromLord discriminator ----

    [TestMethod]
    public void IsFromLord_SourceHeroIdSet_ReturnsTrue()
    {
        var order = new SupplyOrder { SourceHeroId = "lord_1_1" };

        Assert.IsTrue(order.IsFromLord);
    }

    [TestMethod]
    public void IsFromLord_SourceHeroIdNull_ReturnsFalse()
    {
        var order = new SupplyOrder { SourceHeroId = null, SourceSettlementId = "town_A1" };

        Assert.IsFalse(order.IsFromLord);
    }

    [TestMethod]
    public void IsFromLord_SourceHeroIdEmpty_ReturnsFalse()
    {
        // Save round-trips can turn null strings into empty ones; both mean "settlement order".
        var order = new SupplyOrder { SourceHeroId = string.Empty };

        Assert.IsFalse(order.IsFromLord);
    }
}
