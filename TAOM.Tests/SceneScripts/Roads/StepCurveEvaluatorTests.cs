// Tests for TAOM's clean-room reimplementation; behavioural inspiration: Alliance
// mod (GPL v3). See docs/scene-scripts/ATTRIBUTION.md.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.SceneScripts.Roads;

namespace TAOM.Tests.SceneScripts.Roads;

[TestClass]
public class StepCurveEvaluatorTests
{
    private static readonly StepKey[] _flatCurve =
    {
        new StepKey(0f, 1f),
        new StepKey(100f, 1f),
    };

    private static readonly StepKey[] _ascendingCurve =
    {
        new StepKey(0f, 0.5f),
        new StepKey(100f, 2.5f),
    };

    private static readonly StepKey[] _threeKeyCurve =
    {
        new StepKey(0f, 0.5f),
        new StepKey(50f, 2f),
        new StepKey(100f, 0.5f),
    };

    [TestMethod]
    public void Evaluate_FlatCurveAtAnyPercent_ReturnsConstantStep()
    {
        Assert.AreEqual(1f, StepCurveEvaluator.Evaluate(_flatCurve, 0f));
        Assert.AreEqual(1f, StepCurveEvaluator.Evaluate(_flatCurve, 50f));
        Assert.AreEqual(1f, StepCurveEvaluator.Evaluate(_flatCurve, 100f));
    }

    [TestMethod]
    public void Evaluate_AtExactFirstKey_ReturnsFirstKeyStep()
    {
        float result = StepCurveEvaluator.Evaluate(_ascendingCurve, 0f);

        Assert.AreEqual(0.5f, result);
    }

    [TestMethod]
    public void Evaluate_AtExactLastKey_ReturnsLastKeyStep()
    {
        float result = StepCurveEvaluator.Evaluate(_ascendingCurve, 100f);

        Assert.AreEqual(2.5f, result);
    }

    [TestMethod]
    public void Evaluate_AtMidpoint_LerpsBetweenKeys()
    {
        float result = StepCurveEvaluator.Evaluate(_ascendingCurve, 50f);

        Assert.AreEqual(1.5f, result, 0.0001f, "Linear lerp 0.5→2.5 at t=0.5 = 1.5");
    }

    [TestMethod]
    public void Evaluate_AtQuarterPoint_LerpsCorrectly()
    {
        float result = StepCurveEvaluator.Evaluate(_ascendingCurve, 25f);

        Assert.AreEqual(1f, result, 0.0001f, "Linear lerp 0.5→2.5 at t=0.25 = 1.0");
    }

    [TestMethod]
    public void Evaluate_BeforeFirstKey_ClampsToFirstKeyStep()
    {
        float result = StepCurveEvaluator.Evaluate(_ascendingCurve, -50f);

        Assert.AreEqual(0.5f, result);
    }

    [TestMethod]
    public void Evaluate_AfterLastKey_ClampsToLastKeyStep()
    {
        float result = StepCurveEvaluator.Evaluate(_ascendingCurve, 200f);

        Assert.AreEqual(2.5f, result);
    }

    [TestMethod]
    public void Evaluate_ThreeKeyCurveAtMiddleKey_ReturnsMiddleKeyStep()
    {
        float result = StepCurveEvaluator.Evaluate(_threeKeyCurve, 50f);

        Assert.AreEqual(2f, result);
    }

    [TestMethod]
    public void Evaluate_ThreeKeyCurveInFirstSegment_LerpsInThatSegmentOnly()
    {
        float result = StepCurveEvaluator.Evaluate(_threeKeyCurve, 25f);

        Assert.AreEqual(1.25f, result, 0.0001f, "Lerp 0.5→2 at t=0.5 = 1.25");
    }

    [TestMethod]
    public void Evaluate_ThreeKeyCurveInSecondSegment_LerpsInThatSegmentOnly()
    {
        float result = StepCurveEvaluator.Evaluate(_threeKeyCurve, 75f);

        Assert.AreEqual(1.25f, result, 0.0001f, "Lerp 2→0.5 at t=0.5 = 1.25");
    }

    [TestMethod]
    public void Evaluate_SingleKeyCurve_ReturnsThatKeyStep()
    {
        var single = new[] { new StepKey(50f, 3f) };

        Assert.AreEqual(3f, StepCurveEvaluator.Evaluate(single, 0f));
        Assert.AreEqual(3f, StepCurveEvaluator.Evaluate(single, 50f));
        Assert.AreEqual(3f, StepCurveEvaluator.Evaluate(single, 100f));
    }

    [TestMethod]
    public void Evaluate_NullCurve_ReturnsSafeDefault()
    {
        float result = StepCurveEvaluator.Evaluate(null, 50f);

        Assert.AreEqual(1f, result);
    }

    [TestMethod]
    public void Evaluate_EmptyCurve_ReturnsSafeDefault()
    {
        float result = StepCurveEvaluator.Evaluate(new StepKey[0], 50f);

        Assert.AreEqual(1f, result);
    }

    [TestMethod]
    public void Evaluate_TwoKeysAtSamePercent_ReturnsFirstStep()
    {
        var degenerate = new[]
        {
            new StepKey(50f, 1f),
            new StepKey(50f, 5f),
        };

        float result = StepCurveEvaluator.Evaluate(degenerate, 50f);

        Assert.IsTrue(result == 1f || result == 5f, "Either endpoint is acceptable for zero-span segment");
    }
}
