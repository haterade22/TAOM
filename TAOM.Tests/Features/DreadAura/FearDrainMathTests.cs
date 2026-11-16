using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.DreadAura.Domain;

namespace TAOM.Tests.Features.DreadAura;

[TestClass]
public class FearDrainMathTests
{
    [TestMethod]
    public void Compute_FullFalloffNoResist_IsTheScaledFear() => Assert.AreEqual(10f, FearDrainMath.Compute(10f, 1f, 1f, 50f), 0.001f);

    [TestMethod]
    public void Compute_ZeroMorale_Zero() => Assert.AreEqual(0f, FearDrainMath.Compute(10f, 1f, 1f, 0f), 0.001f);

    [TestMethod]
    public void Compute_AppliesFalloffAndResist() => Assert.AreEqual(2f, FearDrainMath.Compute(10f, 0.5f, 0.4f, 50f), 0.001f);

    [TestMethod]
    public void Compute_ClampsToTheMoraleLeft() => Assert.AreEqual(4f, FearDrainMath.Compute(10f, 1f, 1f, 4f), 0.001f);

    [TestMethod]
    public void Compute_NoMoraleSentinel_Zero() => Assert.AreEqual(0f, FearDrainMath.Compute(10f, 1f, 1f, -1f), 0.001f);

    [DataTestMethod]
    [DataRow(float.NaN, 1f, 1f, 50f)]
    [DataRow(10f, float.NaN, 1f, 50f)]
    [DataRow(10f, 1f, float.NaN, 50f)]
    [DataRow(10f, 1f, 1f, float.NaN)]
    [DataRow(float.PositiveInfinity, 1f, 1f, 50f)]
    [DataRow(-5f, 1f, 1f, 50f)]
    public void Compute_BadInput_Zero(float fear, float falloff, float resist, float morale)
        => Assert.AreEqual(0f, FearDrainMath.Compute(fear, falloff, resist, morale), 0.001f);
}
