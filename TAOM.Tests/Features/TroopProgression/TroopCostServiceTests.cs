using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TroopProgression;

namespace TAOM.Tests.Features.TroopProgression;

[TestClass]
public class TroopCostServiceTests
{
    private TroopCostService _sut;

    [TestInitialize]
    public void Setup()
    {
        _sut = new TroopCostService();
    }

    // --- GetCharacterWage: tier-based wage table ---

    [TestMethod]
    [DataRow(0, 1)]
    [DataRow(1, 2)]
    [DataRow(2, 3)]
    [DataRow(3, 5)]
    [DataRow(4, 8)]
    [DataRow(5, 12)]
    [DataRow(6, 15)]
    [DataRow(7, 18)]
    [DataRow(8, 20)]
    [DataRow(9, 25)]
    [DataRow(10, 30)]
    public void GetCharacterWage_ByTier_ReturnsExpectedWage(int tier, int expectedWage)
    {
        var result = _sut.GetCharacterWage(tier, isMounted: false, isMercenary: false);

        Assert.AreEqual(expectedWage, result);
    }

    [TestMethod]
    public void GetCharacterWage_UnknownTier_ReturnsFallback()
    {
        var result = _sut.GetCharacterWage(11, isMounted: false, isMercenary: false);

        Assert.AreEqual(57, result);
    }

    [TestMethod]
    public void GetCharacterWage_Mounted_Adds30Percent()
    {
        var baseWage = _sut.GetCharacterWage(4, isMounted: false, isMercenary: false);
        var mountedWage = _sut.GetCharacterWage(4, isMounted: true, isMercenary: false);

        Assert.AreEqual((int)(baseWage * 1.3f), mountedWage);
    }

    [TestMethod]
    public void GetCharacterWage_Mercenary_Applies150Percent()
    {
        var baseWage = _sut.GetCharacterWage(3, isMounted: false, isMercenary: false);
        var mercWage = _sut.GetCharacterWage(3, isMounted: false, isMercenary: true);

        Assert.AreEqual((int)(baseWage * 1.5f), mercWage);
    }

    [TestMethod]
    public void GetCharacterWage_MountedMercenary_AppliesMercenaryThenMounted()
    {
        var baseWage = _sut.GetCharacterWage(5, isMounted: false, isMercenary: false);
        var bothWage = _sut.GetCharacterWage(5, isMounted: true, isMercenary: true);

        // LOTRAOM order: mercenary first, then mounted
        Assert.AreEqual((int)((int)(baseWage * 1.5f) * 1.3f), bothWage);
    }

    // --- GetTroopRecruitmentCost: level-bracket based ---

    [TestMethod]
    [DataRow(1, 10)]
    [DataRow(3, 20)]
    [DataRow(6, 20)]
    [DataRow(7, 50)]
    [DataRow(11, 50)]
    [DataRow(12, 200)]
    [DataRow(16, 200)]
    [DataRow(17, 400)]
    [DataRow(21, 400)]
    [DataRow(22, 600)]
    [DataRow(26, 600)]
    [DataRow(27, 1000)]
    [DataRow(31, 1000)]
    [DataRow(32, 1500)]
    [DataRow(36, 1500)]
    [DataRow(37, 2100)]
    [DataRow(41, 2100)]
    [DataRow(42, 2800)]
    [DataRow(46, 2800)]
    [DataRow(47, 3600)]
    [DataRow(51, 3600)]
    [DataRow(52, 4000)]
    [DataRow(55, 4000)]
    public void GetTroopRecruitmentCost_ByLevel_ReturnsExpectedCost(int level, int expectedCost)
    {
        var result = _sut.GetTroopRecruitmentCost(level, isMercenary: false);

        Assert.AreEqual(expectedCost, result);
    }

    [TestMethod]
    public void GetTroopRecruitmentCost_Mercenary_Applies2xMultiplier()
    {
        var baseCost = _sut.GetTroopRecruitmentCost(21, isMercenary: false);
        var mercCost = _sut.GetTroopRecruitmentCost(21, isMercenary: true);

        Assert.AreEqual(baseCost * 2, mercCost);
    }
}
