using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Validation;

namespace TAOM.Tests.Core.Validation;

[TestClass]
public class FiniteFloatValidatorTests
{
    [TestMethod]
    public void IsFinite_RegularFloat_True()
    {
        Assert.IsTrue(FiniteFloatValidator.IsFinite(0f));
        Assert.IsTrue(FiniteFloatValidator.IsFinite(1f));
        Assert.IsTrue(FiniteFloatValidator.IsFinite(-1f));
        Assert.IsTrue(FiniteFloatValidator.IsFinite(float.MaxValue));
        Assert.IsTrue(FiniteFloatValidator.IsFinite(float.MinValue));
        Assert.IsTrue(FiniteFloatValidator.IsFinite(float.Epsilon));
    }

    [TestMethod]
    public void IsFinite_NaN_False()
    {
        Assert.IsFalse(FiniteFloatValidator.IsFinite(float.NaN));
    }

    [TestMethod]
    public void IsFinite_PositiveInfinity_False()
    {
        Assert.IsFalse(FiniteFloatValidator.IsFinite(float.PositiveInfinity));
    }

    [TestMethod]
    public void IsFinite_NegativeInfinity_False()
    {
        Assert.IsFalse(FiniteFloatValidator.IsFinite(float.NegativeInfinity));
    }

    [TestMethod]
    public void IsFiniteInRange_ValueInRange_True()
    {
        Assert.IsTrue(FiniteFloatValidator.IsFiniteInRange(5f, 0f, 10f));
        Assert.IsTrue(FiniteFloatValidator.IsFiniteInRange(0f, 0f, 10f));
        Assert.IsTrue(FiniteFloatValidator.IsFiniteInRange(10f, 0f, 10f));
    }

    [TestMethod]
    public void IsFiniteInRange_ValueOutOfRange_False()
    {
        Assert.IsFalse(FiniteFloatValidator.IsFiniteInRange(-0.001f, 0f, 10f));
        Assert.IsFalse(FiniteFloatValidator.IsFiniteInRange(10.001f, 0f, 10f));
    }

    [TestMethod]
    public void IsFiniteInRange_NaN_False()
    {
        Assert.IsFalse(FiniteFloatValidator.IsFiniteInRange(float.NaN, 0f, 10f));
    }

    [TestMethod]
    public void IsFiniteInRange_Infinity_False()
    {
        Assert.IsFalse(FiniteFloatValidator.IsFiniteInRange(float.PositiveInfinity, 0f, 10f));
        Assert.IsFalse(FiniteFloatValidator.IsFiniteInRange(float.NegativeInfinity, 0f, 10f));
    }

    [TestMethod]
    public void IsFiniteAtMost_ValueAtOrBelowMax_True()
    {
        Assert.IsTrue(FiniteFloatValidator.IsFiniteAtMost(-1f, 0f));
        Assert.IsTrue(FiniteFloatValidator.IsFiniteAtMost(0f, 0f));
        Assert.IsTrue(FiniteFloatValidator.IsFiniteAtMost(-100f, 0f));
    }

    [TestMethod]
    public void IsFiniteAtMost_ValueAboveMax_False()
    {
        Assert.IsFalse(FiniteFloatValidator.IsFiniteAtMost(0.001f, 0f));
        Assert.IsFalse(FiniteFloatValidator.IsFiniteAtMost(1f, 0f));
    }

    [TestMethod]
    public void IsFiniteAtMost_NaN_False()
    {
        // CRITICAL: this is the regression case. NaN passes `> 0` checks via the standard pattern,
        // but IsFiniteAtMost must reject it.
        Assert.IsFalse(FiniteFloatValidator.IsFiniteAtMost(float.NaN, 0f));
    }

    [TestMethod]
    public void IsFiniteAtMost_Infinity_False()
    {
        Assert.IsFalse(FiniteFloatValidator.IsFiniteAtMost(float.PositiveInfinity, 0f));
        // NegativeInfinity is "at most" any finite value, but should still be rejected as non-finite.
        Assert.IsFalse(FiniteFloatValidator.IsFiniteAtMost(float.NegativeInfinity, 0f));
    }

    [TestMethod]
    public void IsFiniteAtLeast_ValueAtOrAboveMin_True()
    {
        Assert.IsTrue(FiniteFloatValidator.IsFiniteAtLeast(1f, 0f));
        Assert.IsTrue(FiniteFloatValidator.IsFiniteAtLeast(0f, 0f));
        Assert.IsTrue(FiniteFloatValidator.IsFiniteAtLeast(100f, 0f));
    }

    [TestMethod]
    public void IsFiniteAtLeast_ValueBelowMin_False()
    {
        Assert.IsFalse(FiniteFloatValidator.IsFiniteAtLeast(-0.001f, 0f));
    }

    [TestMethod]
    public void IsFiniteAtLeast_NaN_False()
    {
        Assert.IsFalse(FiniteFloatValidator.IsFiniteAtLeast(float.NaN, 0f));
    }

    [TestMethod]
    public void IsFinite_Double_RegressionCases()
    {
        // Confirm double overload exists and behaves correctly.
        Assert.IsTrue(FiniteFloatValidator.IsFinite(0.0));
        Assert.IsFalse(FiniteFloatValidator.IsFinite(double.NaN));
        Assert.IsFalse(FiniteFloatValidator.IsFinite(double.PositiveInfinity));
    }
}
