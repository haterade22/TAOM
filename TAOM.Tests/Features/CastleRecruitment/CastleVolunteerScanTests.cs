using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CastleRecruitment;

namespace TAOM.Tests.Features.CastleRecruitment;

[TestClass]
public class CastleVolunteerScanTests
{
    private static string?[] Slots(int length, params int[] filled)
    {
        var slots = new string?[length];
        foreach (int i in filled) slots[i] = "troop";
        return slots;
    }

    [TestMethod]
    public void HasVolunteerInScoredSlots_NullArray_ReturnsFalse() =>
        Assert.IsFalse(CastleVolunteerScan.HasVolunteerInScoredSlots<string>(null));

    [TestMethod]
    public void HasVolunteerInScoredSlots_EmptyArray_ReturnsFalse() =>
        Assert.IsFalse(CastleVolunteerScan.HasVolunteerInScoredSlots(new string?[0]));

    [TestMethod]
    public void HasVolunteerInScoredSlots_AllSlotsNull_ReturnsFalse() =>
        Assert.IsFalse(CastleVolunteerScan.HasVolunteerInScoredSlots(Slots(6)));

    [TestMethod]
    public void HasVolunteerInScoredSlots_OnlySlot4Filled_ReturnsFalse() =>
        Assert.IsFalse(CastleVolunteerScan.HasVolunteerInScoredSlots(Slots(6, 4)));

    [TestMethod]
    public void HasVolunteerInScoredSlots_OnlySlot5Filled_ReturnsFalse() =>
        Assert.IsFalse(CastleVolunteerScan.HasVolunteerInScoredSlots(Slots(6, 5)));

    [TestMethod]
    public void HasVolunteerInScoredSlots_Slot0Filled_ReturnsTrue() =>
        Assert.IsTrue(CastleVolunteerScan.HasVolunteerInScoredSlots(Slots(6, 0)));

    [TestMethod]
    public void HasVolunteerInScoredSlots_Slot3Filled_ReturnsTrue() =>
        Assert.IsTrue(CastleVolunteerScan.HasVolunteerInScoredSlots(Slots(6, 3)));

    [TestMethod]
    public void HasVolunteerInScoredSlots_ShortArraySlot1Filled_ReturnsTrue() =>
        Assert.IsTrue(CastleVolunteerScan.HasVolunteerInScoredSlots(Slots(2, 1)));
}
