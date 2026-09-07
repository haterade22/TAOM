using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TroopWeight;

namespace TAOM.Tests.Features.TroopWeight;

// The 2026-09-06 usage-frame reframe: the elite tax is still ENFORCED by deflating the party-size limit,
// but it is DISPLAYED as capacity used (weighted / true-base) rather than as a shrinking limit, because a
// shrinking denominator reads as "adding troops made my party smaller".
[TestClass]
public class TroopWeightDisplayTests
{
    [TestMethod]
    public void DisplayUsed_HeavyParty_ShowsWeightedCost()
    {
        // The reported case: Amras + 9 weight-2 Noldorin Lancers = 10 bodies, 19 weighted.
        Assert.AreEqual(19, TroopWeightDisplay.DisplayUsed(rawCount: 10, weightedCount: 19));
    }

    [TestMethod]
    public void DisplayUsed_LightParty_ShowsRawCount()
        => Assert.AreEqual(40, TroopWeightDisplay.DisplayUsed(rawCount: 40, weightedCount: 40));

    [TestMethod]
    public void DisplayUsed_WeightedCollapsedToZero_FallsBackToRaw()
    {
        // CalculateWeightedRosterCount returns 0f on a failed roster walk. Rendering "0 / 20" for a real
        // 40-body party would be a worse lie than rendering the raw count, so the fallback is one-way.
        Assert.AreEqual(40, TroopWeightDisplay.DisplayUsed(rawCount: 40, weightedCount: 0));
    }

    [TestMethod]
    public void DisplayLimit_PenaltyActive_ShowsTrueBase()
        => Assert.AreEqual(20, TroopWeightDisplay.DisplayLimit(deflatedLimit: 11, trueBaseLimit: 20));

    [TestMethod]
    public void DisplayLimit_NoPenalty_ShowsDeflatedLimit()
        => Assert.AreEqual(20, TroopWeightDisplay.DisplayLimit(deflatedLimit: 20, trueBaseLimit: 20));

    [TestMethod]
    public void DisplayLimit_NoCachedBase_NeverInventsALargerLimit()
    {
        // GetTrueBaseSizeLimit falls back to the deflated limit when it has no cached base for a party
        // (feature off, or the model has not run for it yet). Display must not amplify that.
        Assert.AreEqual(11, TroopWeightDisplay.DisplayLimit(deflatedLimit: 11, trueBaseLimit: 0));
    }

    [TestMethod]
    public void FormatWeightMultiplier_DefaultWeight_IsEmpty()
        => Assert.AreEqual(string.Empty, TroopWeightDisplay.FormatWeightMultiplier(1.0f));

    [TestMethod]
    public void FormatWeightMultiplier_IntegerWeight_HasNoDecimalTail()
    {
        Assert.AreEqual("2", TroopWeightDisplay.FormatWeightMultiplier(2.0f));
        Assert.AreEqual("4", TroopWeightDisplay.FormatWeightMultiplier(4.0f));
    }

    [TestMethod]
    public void FormatWeightMultiplier_FractionalWeight_KeepsTwoDecimals()
        => Assert.AreEqual("1.5", TroopWeightDisplay.FormatWeightMultiplier(1.5f));

    [TestMethod]
    public void FormatWeightMultiplier_BelowOne_IsEmpty()
        => Assert.AreEqual(string.Empty, TroopWeightDisplay.FormatWeightMultiplier(0.5f));

    [TestMethod]
    public void FormatWeightMultiplier_NonFinite_IsEmpty()
    {
        // Positive requirement, per the engine-float gate rule: NaN > 1.0f is false either way, but the
        // explicit finiteness gate keeps float.PositiveInfinity out of the (int)Math.Round cast below it.
        Assert.AreEqual(string.Empty, TroopWeightDisplay.FormatWeightMultiplier(float.NaN));
        Assert.AreEqual(string.Empty, TroopWeightDisplay.FormatWeightMultiplier(float.PositiveInfinity));
    }
}
