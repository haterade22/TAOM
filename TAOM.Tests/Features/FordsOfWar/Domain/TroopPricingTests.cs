using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>The default unit and pack price (build-spec 3.2).</summary>
[TestClass]
public class TroopPricingTests
{
    [DataTestMethod]
    [DataRow(1, 4f)]
    [DataRow(6, 6f)]
    [DataRow(11, 9f)]
    [DataRow(16, 13f)]
    [DataRow(21, 18f)]
    [DataRow(26, 25f)]
    [DataRow(31, 34f)]
    [DataRow(36, 45f)]
    [DataRow(41, 60f)]
    [DataRow(46, 80f)]
    [DataRow(51, 105f)]
    public void UnitPrice_OnGridPoint_ReturnsBasePrice(int level, float expected)
    {
        Assert.AreEqual(expected, TroopPricing.UnitPrice(level, false, 1f), 1e-4f);
    }

    [DataTestMethod]
    [DataRow(5, 4f)]
    [DataRow(10, 6f)]
    [DataRow(25, 18f)]
    [DataRow(50, 80f)]
    public void UnitPrice_BetweenGridPoints_UsesTheLowerOne(int level, float expected)
    {
        Assert.AreEqual(expected, TroopPricing.UnitPrice(level, false, 1f), 1e-4f);
    }

    [TestMethod]
    public void UnitPrice_BelowFirstGridPoint_UsesFirst()
    {
        Assert.AreEqual(4f, TroopPricing.UnitPrice(0, false, 1f), 1e-4f);
        Assert.AreEqual(4f, TroopPricing.UnitPrice(-3, false, 1f), 1e-4f);
    }

    [TestMethod]
    public void UnitPrice_AboveLastGridPoint_UsesLast()
    {
        Assert.AreEqual(105f, TroopPricing.UnitPrice(80, false, 1f), 1e-4f);
    }

    [TestMethod]
    public void UnitPrice_Mounted_CostsOneAndAHalfTimes()
    {
        Assert.AreEqual(13.5f, TroopPricing.UnitPrice(11, true, 1f), 1e-4f);
    }

    [TestMethod]
    public void UnitPrice_EliteWeight_Multiplies()
    {
        Assert.AreEqual(4f * 20f, TroopPricing.UnitPrice(1, false, 20f), 1e-4f);
    }

    [TestMethod]
    public void UnitPrice_NegativeEliteWeight_TreatedAsZero()
    {
        Assert.AreEqual(0f, TroopPricing.UnitPrice(11, false, -2f), 1e-4f);
    }

    [TestMethod]
    public void PackPrice_RoundsToNearestFive()
    {
        Assert.AreEqual(45, TroopPricing.PackPrice(9f, 5));
        Assert.AreEqual(70, TroopPricing.PackPrice(13.5f, 5));
        Assert.AreEqual(35, TroopPricing.PackPrice(6f, 6));
        Assert.AreEqual(25, TroopPricing.PackPrice(4f, 6));
    }

    [TestMethod]
    public void PackPrice_TinyPack_NeverBelowFive()
    {
        Assert.AreEqual(5, TroopPricing.PackPrice(0.4f, 1));
        Assert.AreEqual(5, TroopPricing.PackPrice(0f, 3));
    }

    [TestMethod]
    public void PackPrice_NonPositiveSize_ReturnsMinimum()
    {
        Assert.AreEqual(5, TroopPricing.PackPrice(20f, 0));
    }

    [TestMethod]
    public void PackPrice_FromLevel_MatchesUnitPriceTimesSize()
    {
        var expected = TroopPricing.PackPrice(TroopPricing.UnitPrice(21, true, 1f), 8);

        Assert.AreEqual(expected, TroopPricing.PackPrice(21, true, 1f, 8));
        Assert.AreEqual(215, expected);
    }
}
