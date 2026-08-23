using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Refuge;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.Refuge;

/// <summary>
/// The ONE composition contract for the refuge defender damage reduction, shared by the
/// real-time path (float) and the auto-resolve path (ExplainedNumber): a reduction r scales the
/// FINAL damage by (1 - r). Exists because the two consult sites shipped with different
/// arithmetic (Codex round 2 #11): ExplainedNumber composes factors against the BASE
/// (result = base + base * sum), so a naive AddFactor(-r) subtracted r of the base while the
/// real-time path multiplied the final number, and the paths drifted whenever vanilla factors
/// were non-zero.
/// </summary>
[TestClass]
public class RefugeDamageReductionTests
{
    [TestMethod]
    public void FloatAndExplainedNumber_AgreeWithVanillaFactorsPresent()
    {
        // The drift scenario from the finding: +50% vanilla factor, 20% refuge reduction.
        // Old auto-resolve arithmetic gave 1.30x base; the contract is 1.20x base.
        var explained = new ExplainedNumber(100f);
        explained.AddFactor(0.5f);
        Assert.AreEqual(150f, explained.ResultNumber, 0.001f, "vanilla factor sanity");

        RefugeDamageReduction.Apply(ref explained, 0.2f);
        float viaFloat = RefugeDamageReduction.Apply(150f, 0.2f);

        Assert.AreEqual(120f, viaFloat, 0.001f);
        Assert.AreEqual(viaFloat, explained.ResultNumber, 0.001f,
            "both consult sites must produce the identical final number");
    }

    [TestMethod]
    public void FloatAndExplainedNumber_AgreeWithNoVanillaFactors()
    {
        var explained = new ExplainedNumber(80f);

        RefugeDamageReduction.Apply(ref explained, 0.35f);

        Assert.AreEqual(RefugeDamageReduction.Apply(80f, 0.35f), explained.ResultNumber, 0.001f);
        Assert.AreEqual(52f, explained.ResultNumber, 0.001f);
    }

    [TestMethod]
    public void FloatAndExplainedNumber_AgreeWithNegativeVanillaFactors()
    {
        var explained = new ExplainedNumber(100f);
        explained.AddFactor(-0.4f);

        RefugeDamageReduction.Apply(ref explained, 0.2f);

        Assert.AreEqual(48f, explained.ResultNumber, 0.001f, "60 * 0.8");
    }

    [TestMethod]
    public void OutOfRangeOrNaNReduction_AppliesNothing()
    {
        foreach (var reduction in new[] { 0f, -0.2f, 1f, 1.5f, float.NaN })
        {
            var explained = new ExplainedNumber(100f);
            RefugeDamageReduction.Apply(ref explained, reduction);

            Assert.AreEqual(100f, explained.ResultNumber, 0.001f, $"reduction {reduction}");
            Assert.AreEqual(90f, RefugeDamageReduction.Apply(90f, reduction), 0.001f, $"reduction {reduction}");
        }
    }

    [TestMethod]
    public void PreClampedNumber_PinsTheDocumentedDivergence()
    {
        // PRECONDITION pin (round-C finding): the (1 - r)-on-final contract is exact only for an
        // UNCLAMPED number, because the scale derives from the clamped ResultNumber. Both consult
        // sites apply the reduction before any clamp; this test pins what a pre-clamped input
        // does TODAY so a change in that behaviour is loud, not silent. With base 100, +100%
        // factor, LimitMax 150: ResultNumber reads 150, scale 1.5, factor -0.30, unclamped
        // composition 170, still above the limit, so the clamped result stays 150.
        var explained = new ExplainedNumber(100f);
        explained.AddFactor(1.0f);
        explained.LimitMax(150f);

        RefugeDamageReduction.Apply(ref explained, 0.2f);

        Assert.AreEqual(150f, explained.ResultNumber, 0.001f,
            "pre-clamped input: the limit still binds; (1-r)-on-final is NOT delivered here, "
            + "which is exactly why the docstring precondition exists");
    }

    [TestMethod]
    public void ZeroBase_NoThrowNoChange()
    {
        // base 0 means the result is 0 whatever the factors; the ratio guard must not divide.
        var explained = new ExplainedNumber(0f);
        explained.AddFactor(0.5f);

        RefugeDamageReduction.Apply(ref explained, 0.2f);

        Assert.AreEqual(0f, explained.ResultNumber, 0.001f);
    }
}
