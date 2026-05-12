// Tests for TAOM's clean-room reimplementation; behavioural inspiration: Alliance
// mod (GPL v3). See docs/scene-scripts/ATTRIBUTION.md.
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.SceneScripts.Roads;

namespace TAOM.Tests.SceneScripts.Roads;

[TestClass]
public class StepCurveParserTests
{
    [TestMethod]
    public void TryParse_BraceWrappedTwoKeys_ReturnsBothInOrder()
    {
        bool ok = StepCurveParser.TryParse("{0:1},{100:1}", out var curve);

        Assert.IsTrue(ok);
        Assert.AreEqual(2, curve.Count);
        Assert.AreEqual(0f, curve[0].Percent);
        Assert.AreEqual(1f, curve[0].Step);
        Assert.AreEqual(100f, curve[1].Percent);
        Assert.AreEqual(1f, curve[1].Step);
    }

    [TestMethod]
    public void TryParse_NoBraces_ParsesIdenticallyToBraced()
    {
        StepCurveParser.TryParse("0:1,100:1", out var withoutBraces);
        StepCurveParser.TryParse("{0:1},{100:1}", out var withBraces);

        CollectionAssert.AreEqual(
            withoutBraces.Select(k => (k.Percent, k.Step)).ToList(),
            withBraces.Select(k => (k.Percent, k.Step)).ToList());
    }

    [TestMethod]
    public void TryParse_UnsortedInput_ReturnsKeysSortedByPercentAscending()
    {
        StepCurveParser.TryParse("100:2,0:0.5,50:1", out var curve);

        Assert.AreEqual(3, curve.Count);
        Assert.AreEqual(0f, curve[0].Percent);
        Assert.AreEqual(50f, curve[1].Percent);
        Assert.AreEqual(100f, curve[2].Percent);
    }

    [TestMethod]
    public void TryParse_WhitespaceInValues_IgnoresWhitespace()
    {
        bool ok = StepCurveParser.TryParse("  { 0 : 1.5 } , { 100 : 2.0 }  ", out var curve);

        Assert.IsTrue(ok);
        Assert.AreEqual(2, curve.Count);
        Assert.AreEqual(0f, curve[0].Percent);
        Assert.AreEqual(1.5f, curve[0].Step);
        Assert.AreEqual(100f, curve[1].Percent);
        Assert.AreEqual(2f, curve[1].Step);
    }

    [TestMethod]
    public void TryParse_DecimalSteps_ParsedWithInvariantCulture()
    {
        bool ok = StepCurveParser.TryParse("0:0.25,100:1.75", out var curve);

        Assert.IsTrue(ok);
        Assert.AreEqual(0.25f, curve[0].Step);
        Assert.AreEqual(1.75f, curve[1].Step);
    }

    [TestMethod]
    public void TryParse_StepBelowMinimum_ClampedToMinStep()
    {
        StepCurveParser.TryParse("0:-5,100:0", out var curve);

        Assert.AreEqual(StepCurveParser.MinStep, curve[0].Step);
        Assert.AreEqual(StepCurveParser.MinStep, curve[1].Step);
    }

    [TestMethod]
    public void TryParse_PercentOutOfRange_ClampedToValidRange()
    {
        StepCurveParser.TryParse("-50:1,200:2", out var curve);

        Assert.AreEqual(StepCurveParser.MinPercent, curve[0].Percent);
        Assert.AreEqual(StepCurveParser.MaxPercent, curve[1].Percent);
    }

    [TestMethod]
    public void TryParse_NaNOrInfinity_DiscardsPair()
    {
        StepCurveParser.TryParse("0:NaN,50:Infinity,100:1", out var curve);

        Assert.AreEqual(1, curve.Count);
        Assert.AreEqual(100f, curve[0].Percent);
        Assert.AreEqual(1f, curve[0].Step);
    }

    [TestMethod]
    public void TryParse_EmptyString_Fails()
    {
        bool ok = StepCurveParser.TryParse("", out var curve);

        Assert.IsFalse(ok);
        Assert.AreEqual(StepCurveParser.DefaultCurve, curve);
    }

    [TestMethod]
    public void TryParse_NullInput_Fails()
    {
        bool ok = StepCurveParser.TryParse(null, out var curve);

        Assert.IsFalse(ok);
        Assert.AreEqual(StepCurveParser.DefaultCurve, curve);
    }

    [TestMethod]
    public void TryParse_AllPairsMalformed_Fails()
    {
        Assert.IsFalse(StepCurveParser.TryParse("garbage", out _));
        Assert.IsFalse(StepCurveParser.TryParse("no:colons:here", out _));
        Assert.IsFalse(StepCurveParser.TryParse(":1,2:", out _));
        Assert.IsFalse(StepCurveParser.TryParse("abc:def", out _));
    }

    [TestMethod]
    public void TryParse_MixedValidAndInvalidPairs_KeepsValidOnly()
    {
        bool ok = StepCurveParser.TryParse("0:1,garbage,50:2,100:3", out var curve);

        Assert.IsTrue(ok);
        Assert.AreEqual(3, curve.Count);
        Assert.AreEqual(0f, curve[0].Percent);
        Assert.AreEqual(50f, curve[1].Percent);
        Assert.AreEqual(100f, curve[2].Percent);
    }

    [TestMethod]
    public void Parse_InvalidInput_ReturnsDefaultCurve()
    {
        var curve = StepCurveParser.Parse("not a curve");

        Assert.AreEqual(StepCurveParser.DefaultCurve, curve);
    }

    [TestMethod]
    public void Parse_ValidInput_ReturnsParsedCurve()
    {
        var curve = StepCurveParser.Parse("0:0.5,100:2");

        Assert.AreEqual(2, curve.Count);
        Assert.AreEqual(0.5f, curve[0].Step);
        Assert.AreEqual(2f, curve[1].Step);
    }

    [TestMethod]
    public void DefaultCurve_HasConstantStepOfOne_Across0To100()
    {
        var defaultCurve = StepCurveParser.DefaultCurve;

        Assert.AreEqual(2, defaultCurve.Count);
        Assert.AreEqual(0f, defaultCurve[0].Percent);
        Assert.AreEqual(1f, defaultCurve[0].Step);
        Assert.AreEqual(100f, defaultCurve[1].Percent);
        Assert.AreEqual(1f, defaultCurve[1].Step);
    }

    [TestMethod]
    public void TryParse_SingleKey_AcceptedReturnsOneEntry()
    {
        bool ok = StepCurveParser.TryParse("50:1.5", out var curve);

        Assert.IsTrue(ok);
        Assert.AreEqual(1, curve.Count);
        Assert.AreEqual(50f, curve[0].Percent);
        Assert.AreEqual(1.5f, curve[0].Step);
    }
}
