using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleBalance;

namespace TAOM.Tests.Features.BattleBalance;

/// <summary>
/// #1283: the auto-resolve power exponent. Vanilla's strike damage is 40 x (striker power / struck power)^0.7
/// (<c>DefaultCombatSimulationModel.SimulateHit</c>); TAOM rescales it to the configured exponent, so a side's strength goes
/// as N^1.6 x P^(2e) in a field battle and troop numbers outweigh troop quality.
/// </summary>
[TestClass]
public class AutoResolveQualityMathTests
{
    [TestMethod]
    public void DamageFactor_VanillaExponent_ChangesNothing()
    {
        Assert.AreEqual(1f, AutoResolveQualityMath.DamageFactor(2.91f, 0.66f, 0.7f), 1e-5f);
    }

    [TestMethod]
    public void DamageFactor_EqualPowers_ChangesNothing()
    {
        Assert.AreEqual(1f, AutoResolveQualityMath.DamageFactor(1.3f, 1.3f, 0.5f), 1e-5f);
    }

    [TestMethod]
    public void DamageFactor_LowerExponent_CutsTheStrongerStrikerAndLiftsTheWeaker()
    {
        Assert.AreEqual((float)Math.Pow(4.0, -0.2), AutoResolveQualityMath.DamageFactor(4f, 1f, 0.5f), 1e-5f);
        Assert.AreEqual((float)Math.Pow(0.25, -0.2), AutoResolveQualityMath.DamageFactor(1f, 4f, 0.5f), 1e-5f);
    }

    // The engine's own term times the factor must be the ratio at the configured exponent.
    [DataTestMethod]
    [DataRow(2.91f, 0.66f, 0.5f)]
    [DataRow(0.66f, 2.91f, 0.5f)]
    [DataRow(1.97f, 1.23f, 0f)]
    [DataRow(1.97f, 1.23f, 1f)]
    public void DamageFactor_TimesTheVanillaTerm_IsTheRatioAtTheConfiguredExponent(float striker, float struck, float exponent)
    {
        double ratio = striker / (double)struck;
        double vanilla = Math.Pow(ratio, AutoResolveQualityMath.VanillaPowerRatioExponent);

        double scaled = vanilla * AutoResolveQualityMath.DamageFactor(striker, struck, exponent);

        Assert.AreEqual(Math.Pow(ratio, exponent), scaled, 1e-4);
    }

    [DataTestMethod]
    [DataRow(0f, 1f)]
    [DataRow(-1f, 1f)]
    [DataRow(1f, 0f)]
    [DataRow(float.NaN, 1f)]
    [DataRow(1f, float.NaN)]
    [DataRow(float.PositiveInfinity, 1f)]
    public void DamageFactor_PowerNotPositiveAndFinite_ChangesNothing(float striker, float struck)
    {
        Assert.AreEqual(1f, AutoResolveQualityMath.DamageFactor(striker, struck, 0.5f));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(-0.1f)]
    [DataRow(1.5f)]
    public void DamageFactor_ExponentOutsideZeroToOne_ChangesNothing(float exponent)
    {
        Assert.AreEqual(1f, AutoResolveQualityMath.DamageFactor(4f, 1f, exponent));
    }

    // ExplainedNumber adds factors against the base: result = base x (1 + sum). A multiplier m on the final number is
    // therefore the addend (m - 1) x result / base (RefugeDamageReduction uses the same translation).
    [TestMethod]
    public void BaseRelativeAddend_ScalesTheFinalNumber()
    {
        float baseNumber = 40f, result = 60f, multiplier = 0.8f;

        float addend = AutoResolveQualityMath.BaseRelativeAddend(baseNumber, result, multiplier);

        Assert.AreEqual(result * multiplier, baseNumber + baseNumber * (result / baseNumber - 1f + addend), 1e-4f);
    }

    [DataTestMethod]
    [DataRow(0f, 0f, 0.8f)]
    [DataRow(float.NaN, 10f, 0.8f)]
    [DataRow(10f, float.NaN, 0.8f)]
    [DataRow(10f, 10f, float.NaN)]
    [DataRow(10f, 10f, 1f)]
    public void BaseRelativeAddend_NothingToScale_IsZero(float baseNumber, float result, float multiplier)
    {
        Assert.AreEqual(0f, AutoResolveQualityMath.BaseRelativeAddend(baseNumber, result, multiplier));
    }
}
