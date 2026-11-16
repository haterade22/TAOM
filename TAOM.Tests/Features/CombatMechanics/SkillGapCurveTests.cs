using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CombatMechanics;

namespace TAOM.Tests.Features.CombatMechanics;

[TestClass]
public class SkillGapCurveTests
{
    private const int DeadZone = 20;
    private const int TargetDelta = 135;
    private const float Exponent = 0.01197f;

    private static double Denominator() => SkillGapCurve.Denominator(DeadZone, TargetDelta, Exponent);

    private static float Normalized(float delta) => SkillGapCurve.Normalized(delta, DeadZone, Exponent, Denominator());

    [TestMethod]
    public void Normalized_AtTheDeadZone_IsZero() => Assert.AreEqual(0f, Normalized(20f));

    [TestMethod]
    public void Normalized_JustAboveTheDeadZone_IsPositive() => Assert.IsTrue(Normalized(21f) > 0f);

    [TestMethod]
    public void Normalized_AtTheTargetDelta_IsExactlyOne() => Assert.AreEqual(1f, Normalized(135f));

    [TestMethod]
    public void Normalized_FarAboveTheTargetDelta_IsOne() => Assert.AreEqual(1f, Normalized(300f));

    [TestMethod]
    public void Normalized_NaN_IsZero() => Assert.AreEqual(0f, Normalized(float.NaN));

    [TestMethod]
    public void Normalized_NonPositiveDenominator_IsZero()
    {
        Assert.AreEqual(0f, SkillGapCurve.Normalized(100f, DeadZone, Exponent, 0.0));
        Assert.AreEqual(0f, SkillGapCurve.Normalized(100f, DeadZone, Exponent, -1.0));
    }

    [TestMethod]
    public void Normalized_MatchesTheOldInlineFormula_ForEveryDeltaFrom21To200()
    {
        float curveSpan = TargetDelta - DeadZone;
        float denominatorArg = curveSpan * Exponent;
        double oldDenominator = 1.0 - Math.Exp(-denominatorArg);
        Assert.AreEqual(oldDenominator, Denominator());

        for (int delta = 21; delta <= 200; delta++)
        {
            float numeratorArg = (delta - DeadZone) * Exponent;
            double normalized = (1.0 - Math.Exp(-numeratorArg)) / oldDenominator;
            float expected = normalized < 0.0 ? 0f : normalized > 1.0 ? 1f : (float)normalized;
            Assert.AreEqual(expected, Normalized(delta), $"delta {delta}");
        }
    }
}
