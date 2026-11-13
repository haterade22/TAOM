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
    public void FirstDeserterSlot_NoDeserter_IsTheSlotCount()
    {
        Assert.AreEqual(6, DesertionRecruitCap.FirstDeserterSlot(new[] { "mordor", null, "mordor", null, null, null }, Deserts));
    }

    [TestMethod]
    public void FirstDeserterSlot_SkipsEmptySlots_AndFindsTheFirstDeserter()
    {
        Assert.AreEqual(2, DesertionRecruitCap.FirstDeserterSlot(new[] { "mordor", null, "gondor", "mordor", "gondor", null }, Deserts));
        Assert.AreEqual(0, DesertionRecruitCap.FirstDeserterSlot(new[] { "gondor", "mordor" }, Deserts));
    }

    [TestMethod]
    public void FirstDeserterSlot_NullSlots_IsZero()
    {
        Assert.AreEqual(0, DesertionRecruitCap.FirstDeserterSlot(null, Deserts));
    }

    [TestMethod]
    public void CapInclusiveIndex_StopsBeforeTheDeserter()
    {
        Assert.AreEqual(1, DesertionRecruitCap.CapInclusiveIndex(vanillaIndex: 3, firstDeserterSlot: 2));
        Assert.AreEqual(-1, DesertionRecruitCap.CapInclusiveIndex(vanillaIndex: 3, firstDeserterSlot: 0), "Deserter in slot 0: recruit nothing.");
        Assert.AreEqual(3, DesertionRecruitCap.CapInclusiveIndex(vanillaIndex: 3, firstDeserterSlot: 6), "No deserter: vanilla's index.");
        Assert.AreEqual(-1, DesertionRecruitCap.CapInclusiveIndex(vanillaIndex: -1, firstDeserterSlot: 6), "Never raises vanilla's block.");
    }

    [TestMethod]
    public void CapSlotCount_StopsBeforeTheDeserter()
    {
        Assert.AreEqual(2, DesertionRecruitCap.CapSlotCount(vanillaCount: 4, firstDeserterSlot: 2));
        Assert.AreEqual(0, DesertionRecruitCap.CapSlotCount(vanillaCount: 4, firstDeserterSlot: 0));
        Assert.AreEqual(4, DesertionRecruitCap.CapSlotCount(vanillaCount: 4, firstDeserterSlot: 6));
        Assert.AreEqual(1, DesertionRecruitCap.CapSlotCount(vanillaCount: 1, firstDeserterSlot: 3), "Never raises vanilla's count.");
    }

    [TestMethod]
    public void EstimateSlotEnd_IsTheSmallestOfWindowSlotsAndDeserter()
    {
        Assert.AreEqual(4, DesertionRecruitCap.EstimateSlotEnd(slotsConsidered: 4, slotCount: 6, firstDeserterSlot: 6));
        Assert.AreEqual(2, DesertionRecruitCap.EstimateSlotEnd(slotsConsidered: 2, slotCount: 6, firstDeserterSlot: 5));
        Assert.AreEqual(1, DesertionRecruitCap.EstimateSlotEnd(slotsConsidered: 4, slotCount: 6, firstDeserterSlot: 1));
        Assert.AreEqual(0, DesertionRecruitCap.EstimateSlotEnd(slotsConsidered: 4, slotCount: 6, firstDeserterSlot: 0));
        Assert.AreEqual(3, DesertionRecruitCap.EstimateSlotEnd(slotsConsidered: 4, slotCount: 3, firstDeserterSlot: int.MaxValue));
    }
}
