using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TAOM.Features.AiPartySize;
using TAOM.Features.FactionPower;

namespace TAOM.Tests.Features.FactionPower;

[TestClass]
[TestCategory("RequiresGame")]
public class FactionPowerMathTests
{
    private const float Tolerance = 0.001f;

    private static ExplainedNumber WithFactors(float baseValue, bool describe, params float[] factors)
    {
        var n = new ExplainedNumber(baseValue, describe);
        foreach (var f in factors) n.AddFactor(f);
        return n;
    }

    [TestMethod]
    public void MultiplyResult_At1_LeavesNumberBitIdentical()
    {
        var n = WithFactors(100f, true, 0.1f, 1f);
        var before = (n.BaseNumber, n.SumOfFactors, n.ResultNumber, n.GetLines().Count);

        FactionPowerMath.MultiplyResult(ref n, 1f, null);

        Assert.AreEqual(before, (n.BaseNumber, n.SumOfFactors, n.ResultNumber, n.GetLines().Count));
    }

    [TestMethod]
    public void MultiplyResult_150_ScalesFinalWithExistingFactors()
    {
        var n = WithFactors(100f, false, 0.1f, 1f);

        FactionPowerMath.MultiplyResult(ref n, 1.5f, null);

        Assert.AreEqual(315f, n.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void MultiplyResult_50_HalvesFinal()
    {
        var n = WithFactors(100f, false, 0.1f, 1f);

        FactionPowerMath.MultiplyResult(ref n, 0.5f, null);

        Assert.AreEqual(105f, n.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void MultiplyResult_WithNoFactors_StillScalesTheBase()
    {
        var n = new ExplainedNumber(80f);

        FactionPowerMath.MultiplyResult(ref n, 2f, null);

        Assert.AreEqual(160f, n.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void FlatBonusAfter_StillCostsExactSlots()
    {
        var n = WithFactors(100f, false, 0.1f, 1f);
        FactionPowerMath.MultiplyResult(ref n, 1.5f, null);

        AiPartySizeService.AddResultFrameBonus(ref n, 70f);

        Assert.AreEqual(385f, n.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void MultiplyResult_AddsOneDescribedLine_WhenItChangesTheNumber()
    {
        var n = new ExplainedNumber(100f, true);

        FactionPowerMath.MultiplyResult(ref n, 1.5f, new TaleWorlds.Localization.TextObject("Realm power"));

        Assert.AreEqual(1, n.GetLines().FindAll(l => l.Item1 == "Realm power").Count);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(0.49f)]
    [DataRow(2.01f)]
    [DataRow(0f)]
    [DataRow(-1f)]
    public void MultiplyResult_NaN_Infinite_OutOfRange_Unchanged(float m)
    {
        var n = WithFactors(100f, false, 0.1f);

        FactionPowerMath.MultiplyResult(ref n, m, null);

        Assert.AreEqual(110f, n.ResultNumber, Tolerance);
    }

    [DataTestMethod]
    [DataRow(-1f)]
    [DataRow(-1.5f)]
    [DataRow(-0.995f)]
    [DataRow(float.NaN)]
    public void MultiplyResult_FrameScaleNearZeroOrNaN_Unchanged(float factor)
    {
        var n = WithFactors(100f, false, factor);
        var before = n.ResultNumber;

        FactionPowerMath.MultiplyResult(ref n, 1.5f, null);

        Assert.AreEqual(before.ToString(), n.ResultNumber.ToString());
    }

    [DataTestMethod]
    [DataRow(49, false)]
    [DataRow(50, true)]
    [DataRow(100, true)]
    [DataRow(200, true)]
    [DataRow(201, false)]
    [DataRow(int.MinValue, false)]
    [DataRow(int.MaxValue, false)]
    public void IsValidPercent_AcceptsFiftyToTwoHundred(int percent, bool expected)
    {
        Assert.AreEqual(expected, FactionPowerMath.IsValidPercent(percent));
    }

    [TestMethod]
    public void ToMultiplier_DividesByAHundred_AndIsExactlyOneAtAHundred()
    {
        Assert.AreEqual(1f, FactionPowerMath.ToMultiplier(100));
        Assert.AreEqual(1.5f, FactionPowerMath.ToMultiplier(150));
        Assert.AreEqual(0.5f, FactionPowerMath.ToMultiplier(50));
    }
}
