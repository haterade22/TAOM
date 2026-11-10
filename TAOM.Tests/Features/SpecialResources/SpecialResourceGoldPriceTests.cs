using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SpecialResources;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>#1109: the one conversion from resource points to gold that every gold-mode price uses.</summary>
[TestClass]
public class SpecialResourceGoldPriceTests
{
    [TestMethod]
    public void ToGold_MultipliesPointsByRate() =>
        Assert.AreEqual(300, SpecialResourceGoldPrice.ToGold(3, 100));

    [TestMethod]
    public void ToGold_RoundsHalfAwayFromZero() =>
        Assert.AreEqual(2, SpecialResourceGoldPrice.ToGold(0.5, 3));

    [TestMethod]
    public void ToGold_NaN_ReturnsZero() =>
        Assert.AreEqual(0, SpecialResourceGoldPrice.ToGold(double.NaN, 100));

    [TestMethod]
    public void ToGold_NonPositivePointsOrRate_ReturnsZero()
    {
        Assert.AreEqual(0, SpecialResourceGoldPrice.ToGold(0, 100));
        Assert.AreEqual(0, SpecialResourceGoldPrice.ToGold(-2, 100));
        Assert.AreEqual(0, SpecialResourceGoldPrice.ToGold(2, 0));
        Assert.AreEqual(0, SpecialResourceGoldPrice.ToGold(2, -5));
    }

    [TestMethod]
    public void ToGold_Huge_SaturatesAtMaxGold()
    {
        Assert.AreEqual(SpecialResourceGoldPrice.MaxGold, SpecialResourceGoldPrice.ToGold(int.MaxValue, 1000));
        Assert.AreEqual(SpecialResourceGoldPrice.MaxGold, SpecialResourceGoldPrice.ToGold(double.PositiveInfinity, 100));
    }
}
