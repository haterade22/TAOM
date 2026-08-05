using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.Abilities;

namespace TAOM.Tests.Features.CareerSystem.Abilities;

// Issue #383 — the "+N from ability" share of a hit. DamageMultiplierBonus is a driven
// property (the engine bakes it into the final number), so the share is recovered as
// damagedHp * f / (1 + f) — exact while the ability buff is the sole multiplicative
// term on the path (CareerAgentStatService applies the passive Damage effect through a
// different mechanism, deliberately out of scope here).
[TestClass]
public class AbilityDamageAttributionTests
{
    [TestMethod]
    public void ComputeBonusDamage_TypicalBonus_ExactShare()
    {
        // 43 final damage at +15%: baseline 43/1.15 = 37.39, share 5.61.
        var share = AbilityDamageAttribution.ComputeBonusDamage(43f, 0.15f);
        Assert.AreEqual(5.6087f, share, 0.001f);
    }

    [TestMethod]
    public void ComputeBonusDamage_ZeroBonus_Zero()
    {
        Assert.AreEqual(0f, AbilityDamageAttribution.ComputeBonusDamage(43f, 0f), 0.001f);
    }

    [TestMethod]
    public void ComputeBonusDamage_ZeroOrNegativeDamage_Zero()
    {
        Assert.AreEqual(0f, AbilityDamageAttribution.ComputeBonusDamage(0f, 0.15f), 0.001f);
        Assert.AreEqual(0f, AbilityDamageAttribution.ComputeBonusDamage(-5f, 0.15f), 0.001f);
    }

    [TestMethod]
    public void ComputeBonusDamage_NonFiniteInputs_Zero()
    {
        // Engine floats arrive per-hit and can be garbage — NaN must fail the gate.
        Assert.AreEqual(0f, AbilityDamageAttribution.ComputeBonusDamage(float.NaN, 0.15f), 0.001f);
        Assert.AreEqual(0f, AbilityDamageAttribution.ComputeBonusDamage(43f, float.NaN), 0.001f);
        Assert.AreEqual(0f, AbilityDamageAttribution.ComputeBonusDamage(float.PositiveInfinity, 0.15f), 0.001f);
        Assert.AreEqual(0f, AbilityDamageAttribution.ComputeBonusDamage(43f, float.NegativeInfinity), 0.001f);
    }

    [TestMethod]
    public void ComputeBonusDamage_NegativeBonus_Zero()
    {
        // A net-negative buff composition must not print a negative attribution.
        Assert.AreEqual(0f, AbilityDamageAttribution.ComputeBonusDamage(43f, -0.2f), 0.001f);
    }

    [TestMethod]
    public void ShouldReport_AboveThreshold_True()
    {
        Assert.IsTrue(AbilityDamageAttribution.ShouldReport(0.6f, 0.5f));
        Assert.IsTrue(AbilityDamageAttribution.ShouldReport(0.5f, 0.5f));
    }

    [TestMethod]
    public void ShouldReport_BelowThreshold_False()
    {
        Assert.IsFalse(AbilityDamageAttribution.ShouldReport(0.4f, 0.5f));
        Assert.IsFalse(AbilityDamageAttribution.ShouldReport(0f, 0.5f));
    }

    [TestMethod]
    public void ShouldReport_NaN_False()
    {
        Assert.IsFalse(AbilityDamageAttribution.ShouldReport(float.NaN, 0.5f));
    }
}
