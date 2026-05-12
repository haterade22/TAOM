// Tests for TAOM's clean-room reimplementation; behavioural inspiration: Alliance
// mod (GPL v3). See docs/scene-scripts/ATTRIBUTION.md.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.SceneScripts.Roads;

namespace TAOM.Tests.SceneScripts.Roads;

[TestClass]
public class RoadPathSamplerTests
{
    private static readonly StepKey[] _flatCurve =
    {
        new StepKey(0f, 1f),
        new StepKey(100f, 1f),
    };

    [TestMethod]
    public void SampleDistances_FlatCurveTotal10Step1_ProducesElevenDistancesIncludingEndpoint()
    {
        var distances = RoadPathSampler.SampleDistances(_flatCurve, totalDistance: 10f, minStep: 0.1f);

        Assert.AreEqual(11, distances.Count, "0..9 stepping by 1 (10 entries) plus endpoint 10 = 11");
        Assert.AreEqual(0f, distances[0]);
        Assert.AreEqual(10f, distances[distances.Count - 1]);
    }

    [TestMethod]
    public void SampleDistances_ZeroTotalDistance_ReturnsEmpty()
    {
        var distances = RoadPathSampler.SampleDistances(_flatCurve, 0f, 0.1f);

        Assert.AreEqual(0, distances.Count);
    }

    [TestMethod]
    public void SampleDistances_NegativeTotalDistance_ReturnsEmpty()
    {
        var distances = RoadPathSampler.SampleDistances(_flatCurve, -5f, 0.1f);

        Assert.AreEqual(0, distances.Count);
    }

    [TestMethod]
    public void SampleDistances_NaNTotalDistance_ReturnsEmpty()
    {
        var distances = RoadPathSampler.SampleDistances(_flatCurve, float.NaN, 0.1f);

        Assert.AreEqual(0, distances.Count);
    }

    [TestMethod]
    public void SampleDistances_InfinityTotalDistance_ReturnsEmpty()
    {
        var distances = RoadPathSampler.SampleDistances(_flatCurve, float.PositiveInfinity, 0.1f);

        Assert.AreEqual(0, distances.Count);
    }

    [TestMethod]
    public void SampleDistances_StepCurveYieldsNonFiniteStep_ClampsToMinStep()
    {
        var degenerate = new[] { new StepKey(0f, float.NaN), new StepKey(100f, float.NaN) };

        var distances = RoadPathSampler.SampleDistances(degenerate, totalDistance: 1f, minStep: 0.5f);

        Assert.IsTrue(distances.Count >= 2, "Should produce at least start + end");
        Assert.AreEqual(0f, distances[0]);
        Assert.AreEqual(1f, distances[distances.Count - 1]);
    }

    [TestMethod]
    public void SampleDistances_NonPositiveMinStep_FallsBackToSafeMinimum()
    {
        var distances = RoadPathSampler.SampleDistances(_flatCurve, totalDistance: 1f, minStep: -1f);

        Assert.IsTrue(distances.Count >= 2);
        Assert.AreEqual(0f, distances[0]);
        Assert.AreEqual(1f, distances[distances.Count - 1]);
    }

    [TestMethod]
    public void SampleDistances_DenseCurveAtStart_ProducesMoreSamplesNearStart()
    {
        var dense = new[]
        {
            new StepKey(0f, 0.1f),
            new StepKey(50f, 1f),
            new StepKey(100f, 0.1f),
        };

        var distances = RoadPathSampler.SampleDistances(dense, totalDistance: 100f, minStep: 0.05f);

        int firstQuarter = 0, lastQuarter = 0;
        foreach (var d in distances)
        {
            if (d > 0f && d < 25f) firstQuarter++;
            if (d > 75f && d < 100f) lastQuarter++;
        }

        Assert.IsTrue(firstQuarter > 5, "Dense start should produce >5 samples in 0-25 range");
        Assert.IsTrue(lastQuarter > 5, "Dense end should produce >5 samples in 75-100 range");
    }

    [TestMethod]
    public void SampleDistances_LastEntryIsAlwaysExactlyTotalDistance()
    {
        var distances = RoadPathSampler.SampleDistances(_flatCurve, totalDistance: 7.3f, minStep: 0.1f);

        Assert.AreEqual(7.3f, distances[distances.Count - 1], 0.0001f);
    }
}
