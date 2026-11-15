using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleBalance;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.BattleBalance;

/// <summary>
/// #1283: <c>AutoResolveQualityMath.Apply</c> on a real ExplainedNumber. The final strike damage is multiplied by
/// (striker power / struck power)^(exponent - 0.7), whatever factors the base already added.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class AutoResolveQualityApplyTests
{
    [TestMethod]
    public void Apply_WithBaseFactorsPresent_ScalesTheFinalNumber()
    {
        var explained = new ExplainedNumber(40f);
        explained.AddFactor(0.5f);

        AutoResolveQualityMath.Apply(ref explained, 4f, 1f, 0.5f);

        Assert.AreEqual(60f * (float)Math.Pow(4.0, -0.2), explained.ResultNumber, 1e-3f);
    }

    [TestMethod]
    public void Apply_StrikerAndStruck_AreNotInterchangeable()
    {
        var strongStrikes = new ExplainedNumber(40f);
        var weakStrikes = new ExplainedNumber(40f);

        AutoResolveQualityMath.Apply(ref strongStrikes, 4f, 1f, 0.5f);
        AutoResolveQualityMath.Apply(ref weakStrikes, 1f, 4f, 0.5f);

        Assert.IsTrue(strongStrikes.ResultNumber < 40f, "the stronger striker deals less than vanilla");
        Assert.IsTrue(weakStrikes.ResultNumber > 40f, "the weaker striker deals more than vanilla");
    }

    [DataTestMethod]
    [DataRow(0.7f)]
    [DataRow(float.NaN)]
    public void Apply_VanillaOrInvalidExponent_ChangesNothing(float exponent)
    {
        var explained = new ExplainedNumber(40f);
        explained.AddFactor(0.5f);

        AutoResolveQualityMath.Apply(ref explained, 4f, 1f, exponent);

        Assert.AreEqual(60f, explained.ResultNumber, 1e-3f);
    }

    [TestMethod]
    public void Apply_ZeroBase_NoThrowNoChange()
    {
        var explained = new ExplainedNumber(0f);

        AutoResolveQualityMath.Apply(ref explained, 4f, 1f, 0.5f);

        Assert.AreEqual(0f, explained.ResultNumber, 1e-5f);
    }
}
