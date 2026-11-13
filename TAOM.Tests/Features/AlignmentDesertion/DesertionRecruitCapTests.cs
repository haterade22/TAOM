using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AlignmentDesertion;

namespace TAOM.Tests.Features.AlignmentDesertion;

/// <summary>
/// The slot arithmetic that keeps a volunteer who would desert out of a buyer's reach (conflict audit 2026-11-13).
/// "gondor" stands for a culture that deserts the buyer, anything else stays.
/// </summary>
[TestClass]
public class DesertionRecruitCapTests
{
    private static bool Deserts(string? culture) => culture == "gondor";

    [TestMethod]
    public void FirstDeserterSlot_NoDeserter_IsNoDeserter()
    {
        Assert.AreEqual(DesertionRecruitCap.NoDeserter, DesertionRecruitCap.FirstDeserterSlot(new[] { "mordor", null, "mordor", null, null, null }, Deserts));
        Assert.AreEqual(DesertionRecruitCap.NoDeserter, DesertionRecruitCap.FirstDeserterSlot(new string?[0], Deserts));
    }

    [TestMethod]
    public void FirstDeserterSlot_SkipsEmptySlots_AndFindsTheFirstDeserter()
    {
        Assert.AreEqual(2, DesertionRecruitCap.FirstDeserterSlot(new[] { "mordor", null, "gondor", "mordor", "gondor", null }, Deserts));
        Assert.AreEqual(0, DesertionRecruitCap.FirstDeserterSlot(new[] { "gondor", "mordor" }, Deserts));
    }

    [TestMethod]
    public void FirstDeserterSlot_NullSlots_IsNoDeserter()
    {
        Assert.AreEqual(DesertionRecruitCap.NoDeserter, DesertionRecruitCap.FirstDeserterSlot(null, Deserts));
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CapVolunteerIndex_NoDeserter_KeepsVanillaForEveryBuyer(bool buyerIsPlayer)
    {
        Assert.AreEqual(6, DesertionRecruitCap.CapVolunteerIndex(6, DesertionRecruitCap.NoDeserter, buyerIsPlayer));
        Assert.AreEqual(3, DesertionRecruitCap.CapVolunteerIndex(3, DesertionRecruitCap.NoDeserter, buyerIsPlayer));
        Assert.AreEqual(-1, DesertionRecruitCap.CapVolunteerIndex(-1, DesertionRecruitCap.NoDeserter, buyerIsPlayer));
    }

    [DataTestMethod]
    [DataRow(0, -1)]
    [DataRow(1, 0)]
    [DataRow(3, 2)]
    public void CapVolunteerIndex_PlayerBuyer_IsInclusiveAndStopsBeforeTheDeserter(int firstDeserter, int expected)
    {
        Assert.AreEqual(expected, DesertionRecruitCap.CapVolunteerIndex(6, firstDeserter, buyerIsPlayer: true));
    }

    [DataTestMethod]
    [DataRow(0, 0)]
    [DataRow(1, 1)]
    [DataRow(3, 3)]
    public void CapVolunteerIndex_AiBuyer_IsExclusiveSoTheDeserterSlotIsTheBound(int firstDeserter, int expected)
    {
        Assert.AreEqual(expected, DesertionRecruitCap.CapVolunteerIndex(6, firstDeserter, buyerIsPlayer: false));
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CapVolunteerIndex_NeverRaisesVanilla(bool buyerIsPlayer)
    {
        Assert.AreEqual(-1, DesertionRecruitCap.CapVolunteerIndex(-1, 3, buyerIsPlayer));
        Assert.AreEqual(1, DesertionRecruitCap.CapVolunteerIndex(1, 3, buyerIsPlayer));
    }

    [TestMethod]
    public void CapSlotCount_StopsBeforeTheDeserter()
    {
        Assert.AreEqual(2, DesertionRecruitCap.CapSlotCount(vanillaCount: 4, firstDeserterSlot: 2));
        Assert.AreEqual(0, DesertionRecruitCap.CapSlotCount(vanillaCount: 4, firstDeserterSlot: 0));
        Assert.AreEqual(4, DesertionRecruitCap.CapSlotCount(vanillaCount: 4, firstDeserterSlot: DesertionRecruitCap.NoDeserter));
        Assert.AreEqual(6, DesertionRecruitCap.CapSlotCount(vanillaCount: 6, firstDeserterSlot: DesertionRecruitCap.NoDeserter), "No deserter: vanilla's count, even the full six.");
        Assert.AreEqual(1, DesertionRecruitCap.CapSlotCount(vanillaCount: 1, firstDeserterSlot: 3), "Never raises vanilla's count.");
    }

    [TestMethod]
    public void EstimateSlotEnd_IsTheSmallestOfWindowSlotsAndDeserter()
    {
        Assert.AreEqual(4, DesertionRecruitCap.EstimateSlotEnd(slotsConsidered: 4, slotCount: 6, firstDeserterSlot: DesertionRecruitCap.NoDeserter));
        Assert.AreEqual(2, DesertionRecruitCap.EstimateSlotEnd(slotsConsidered: 2, slotCount: 6, firstDeserterSlot: 5));
        Assert.AreEqual(1, DesertionRecruitCap.EstimateSlotEnd(slotsConsidered: 4, slotCount: 6, firstDeserterSlot: 1));
        Assert.AreEqual(0, DesertionRecruitCap.EstimateSlotEnd(slotsConsidered: 4, slotCount: 6, firstDeserterSlot: 0));
        Assert.AreEqual(3, DesertionRecruitCap.EstimateSlotEnd(slotsConsidered: 4, slotCount: 3, firstDeserterSlot: int.MaxValue));
    }
}
