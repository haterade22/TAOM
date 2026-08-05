using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Equipment;

namespace TAOM.Tests.Features.Enlistment.Equipment;

/// <summary>
/// Pure payoff math: payoff = Σ max(25, itemValue); quartermaster price =
/// max(25, cost − 10×tier). No engine types, no state.
/// </summary>
[TestClass]
public class EquipmentPayoffCalculatorTests
{
    [TestMethod]
    public void CalculatePayoff_SumsItemValues()
    {
        var payoff = EquipmentPayoffCalculator.CalculatePayoff(new[] { 100, 200, 50 });

        Assert.AreEqual(350, payoff);
    }

    [TestMethod]
    public void CalculatePayoff_FloorsEachItemAtMinimum()
    {
        // 1 -> 25, 10 -> 25: the floor applies PER ITEM, not to the sum.
        var payoff = EquipmentPayoffCalculator.CalculatePayoff(new[] { 1, 10 });

        Assert.AreEqual(50, payoff);
    }

    [TestMethod]
    public void CalculatePayoff_MixedValues_FloorsOnlyLowOnes()
    {
        var payoff = EquipmentPayoffCalculator.CalculatePayoff(new[] { 30, 10 });

        Assert.AreEqual(55, payoff);
    }

    [TestMethod]
    public void CalculatePayoff_ZeroAndNegativeValues_FloorAt25()
    {
        // Unpriced items (ItemObject.Value 0) still pay the floor.
        var payoff = EquipmentPayoffCalculator.CalculatePayoff(new[] { 0, -5 });

        Assert.AreEqual(50, payoff);
    }

    [TestMethod]
    public void CalculatePayoff_EmptySequence_ReturnsZero()
    {
        Assert.AreEqual(0, EquipmentPayoffCalculator.CalculatePayoff(new int[0]));
    }

    [TestMethod]
    public void CalculatePayoff_NullSequence_ReturnsZero()
    {
        Assert.AreEqual(0, EquipmentPayoffCalculator.CalculatePayoff(null));
    }

    [TestMethod]
    public void ApplyQuartermasterDiscount_SubtractsTenPerTier()
    {
        Assert.AreEqual(70, EquipmentPayoffCalculator.ApplyQuartermasterDiscount(100, 3));
    }

    [TestMethod]
    public void ApplyQuartermasterDiscount_TierZero_LeavesCostUnchanged()
    {
        Assert.AreEqual(100, EquipmentPayoffCalculator.ApplyQuartermasterDiscount(100, 0));
    }

    [TestMethod]
    public void ApplyQuartermasterDiscount_FloorsAt25()
    {
        Assert.AreEqual(25, EquipmentPayoffCalculator.ApplyQuartermasterDiscount(40, 5));
    }

    [TestMethod]
    public void ApplyQuartermasterDiscount_NegativeTier_DoesNotInflateCost()
    {
        // A corrupt/negative tier must never make the quartermaster MORE expensive.
        Assert.AreEqual(100, EquipmentPayoffCalculator.ApplyQuartermasterDiscount(100, -2));
    }
}
