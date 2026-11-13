using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms.Domain.Services;

namespace TAOM.Tests.Features.Realms;

/// <summary>
/// The liege army discount and the King's Muster do not stack: the larger discount wins (conflict audit, 13 Nov 2026).
/// </summary>
[TestClass]
public class LiegeArmyDiscountRuleTests
{
    [TestMethod]
    public void NoEarlierDiscount_LiegeDiscountAppliesInFull()
    {
        Assert.AreEqual(0.5f, LiegeArmyDiscountRule.RemainingFactor(1f, 0.5f), 1e-6f);
    }

    [TestMethod]
    public void MusterHalf_LiegeHalf_NoExtraDiscount()
    {
        // The audit case: an AI ruler calls a vassal of their own title. Half, not a quarter.
        Assert.AreEqual(1f, LiegeArmyDiscountRule.RemainingFactor(0.5f, 0.5f), 1e-6f);
    }

    [TestMethod]
    public void LargerLiegeDiscount_AddsOnlyTheDifference()
    {
        // Muster 0.5, liege 75% off: the total is 0.25, the larger of the two.
        var remaining = LiegeArmyDiscountRule.RemainingFactor(0.5f, 0.75f);
        Assert.AreEqual(0.25f, 0.5f * remaining, 1e-6f);
    }

    [TestMethod]
    public void LargerMusterDiscount_LiegeAddsNothing()
    {
        Assert.AreEqual(1f, LiegeArmyDiscountRule.RemainingFactor(0.25f, 0.5f), 1e-6f);
    }

    [TestMethod]
    public void NoLiegeDiscount_OrBadNumbers_NeverRaiseTheCost()
    {
        Assert.AreEqual(1f, LiegeArmyDiscountRule.RemainingFactor(0.5f, 0f), 1e-6f);
        Assert.AreEqual(1f, LiegeArmyDiscountRule.RemainingFactor(0.5f, float.NaN), 1e-6f);
        Assert.AreEqual(0.5f, LiegeArmyDiscountRule.RemainingFactor(float.NaN, 0.5f), 1e-6f);
        Assert.AreEqual(0.5f, LiegeArmyDiscountRule.RemainingFactor(0f, 0.5f), 1e-6f);
        Assert.AreEqual(0f, LiegeArmyDiscountRule.RemainingFactor(1f, 1f), 1e-6f);
    }
}
