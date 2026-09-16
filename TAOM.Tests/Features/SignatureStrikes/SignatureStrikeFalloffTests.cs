using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SignatureStrikes;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// The ring falloff is the engine's own boulder curve (Mission.MissileAreaDamageCallback, v1.5.3
/// Mission.cs:5739-5744): full damage inside the inner radius, then 1 / lerp(1, 3, t)^2 out to the
/// outer radius, so the edge takes one ninth. Every input is an engine float or a config float,
/// so every non-finite input must yield zero, never NaN (csharp-architecture.md "Engine-Float
/// Decision Gates").
/// </summary>
[TestClass]
public class SignatureStrikeFalloffTests
{
    [TestMethod]
    public void Compute_InsideInnerRadius_ReturnsOne()
    {
        Assert.AreEqual(1f, SignatureStrikeFalloff.Compute(0.5f, 1.5f, 4f), 0.0001f);
    }

    [TestMethod]
    public void Compute_ExactlyAtInnerRadius_ReturnsOne()
    {
        Assert.AreEqual(1f, SignatureStrikeFalloff.Compute(1.5f, 1.5f, 4f), 0.0001f);
    }

    [TestMethod]
    public void Compute_AtOuterRadius_ReturnsOneNinth()
    {
        Assert.AreEqual(1f / 9f, SignatureStrikeFalloff.Compute(4f, 1.5f, 4f), 0.0001f);
    }

    [TestMethod]
    public void Compute_MidwayThroughTheBand_MatchesTheEngineCurve()
    {
        // t = 0.5 -> lerp(1, 3, 0.5) = 2 -> 1 / 4.
        Assert.AreEqual(0.25f, SignatureStrikeFalloff.Compute(2.75f, 1.5f, 4f), 0.0001f);
    }

    [TestMethod]
    public void Compute_BeyondOuterRadius_ReturnsZero()
    {
        Assert.AreEqual(0f, SignatureStrikeFalloff.Compute(4.01f, 1.5f, 4f), 0.0001f);
    }

    [TestMethod]
    public void Compute_InnerEqualsOuter_IsFlatInsideOuter()
    {
        Assert.AreEqual(1f, SignatureStrikeFalloff.Compute(2f, 3f, 3f), 0.0001f);
        Assert.AreEqual(0f, SignatureStrikeFalloff.Compute(3.5f, 3f, 3f), 0.0001f);
    }

    [TestMethod]
    public void Compute_InnerAboveOuter_IsFlatInsideOuter()
    {
        // The config provider rejects this ordering; the function still degrades to a flat band
        // rather than a negative t if it ever sees it at runtime.
        Assert.AreEqual(1f, SignatureStrikeFalloff.Compute(2f, 5f, 3f), 0.0001f);
    }

    [TestMethod]
    public void Compute_NegativeDistance_ReturnsZero()
    {
        Assert.AreEqual(0f, SignatureStrikeFalloff.Compute(-1f, 1.5f, 4f), 0.0001f);
    }

    [TestMethod]
    public void Compute_ZeroOuterRadius_ReturnsZero()
    {
        Assert.AreEqual(0f, SignatureStrikeFalloff.Compute(0f, 0f, 0f), 0.0001f);
    }

    [TestMethod]
    public void Compute_NaNDistance_ReturnsZero()
    {
        Assert.AreEqual(0f, SignatureStrikeFalloff.Compute(float.NaN, 1.5f, 4f), 0.0001f);
    }

    [TestMethod]
    public void Compute_NaNInnerRadius_ReturnsZero()
    {
        Assert.AreEqual(0f, SignatureStrikeFalloff.Compute(1f, float.NaN, 4f), 0.0001f);
    }

    [TestMethod]
    public void Compute_NaNOuterRadius_ReturnsZero()
    {
        Assert.AreEqual(0f, SignatureStrikeFalloff.Compute(1f, 1.5f, float.NaN), 0.0001f);
    }

    [TestMethod]
    public void Compute_InfiniteDistance_ReturnsZero()
    {
        Assert.AreEqual(0f, SignatureStrikeFalloff.Compute(float.PositiveInfinity, 1.5f, 4f), 0.0001f);
    }

    [TestMethod]
    public void Compute_InfiniteOuterRadius_ReturnsZero()
    {
        Assert.AreEqual(0f, SignatureStrikeFalloff.Compute(1f, 1.5f, float.PositiveInfinity), 0.0001f);
    }
}
