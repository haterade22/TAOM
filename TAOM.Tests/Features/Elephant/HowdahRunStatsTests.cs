using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// Running extremes for one howdah's end-of-battle summary (#627). A non-finite sample is counted as invalid and never
/// folded in, so one NaN from the engine cannot erase a real minimum or maximum.
/// </summary>
[TestClass]
public class HowdahRunStatsTests
{
    [TestMethod]
    public void NewStats_HaveNoExtremes()
    {
        var stats = new HowdahRunStats();
        Assert.AreEqual(0, stats.Ticks);
        Assert.AreEqual(0, stats.Samples);
        Assert.IsTrue(float.IsNaN(stats.MinClearance));
        Assert.IsTrue(float.IsNaN(stats.MaxDrift));
        Assert.IsTrue(float.IsNaN(stats.MaxCarriedSpeed));
    }

    [TestMethod]
    public void CountTick_Counts()
    {
        var stats = new HowdahRunStats();
        stats.CountTick();
        stats.CountTick();
        Assert.AreEqual(2, stats.Ticks);
    }

    [TestMethod]
    public void RecordSample_KeepsTheMinimumClearanceAndTheMaximaOfDriftAndCarriedSpeed()
    {
        var stats = new HowdahRunStats();
        stats.RecordSample(clearance: 0.35f, drift: 0.01f, carriedSpeed: 0.2f);
        stats.RecordSample(clearance: 0.10f, drift: 0.30f, carriedSpeed: 1.5f);
        stats.RecordSample(clearance: 0.40f, drift: 0.02f, carriedSpeed: 0.1f);
        Assert.AreEqual(3, stats.Samples);
        Assert.AreEqual(0.10f, stats.MinClearance, 1e-6f);
        Assert.AreEqual(0.30f, stats.MaxDrift, 1e-6f);
        Assert.AreEqual(1.5f, stats.MaxCarriedSpeed, 1e-6f);
        Assert.AreEqual(0, stats.InvalidValues);
    }

    [TestMethod]
    public void RecordSample_NonFiniteValues_AreCountedNotFolded()
    {
        var stats = new HowdahRunStats();
        stats.RecordSample(clearance: 0.35f, drift: 0.01f, carriedSpeed: 0.2f);
        stats.RecordSample(clearance: float.NaN, drift: float.PositiveInfinity, carriedSpeed: float.NaN);
        Assert.AreEqual(2, stats.Samples);
        Assert.AreEqual(3, stats.InvalidValues);
        Assert.AreEqual(0.35f, stats.MinClearance, 1e-6f);
        Assert.AreEqual(0.01f, stats.MaxDrift, 1e-6f);
        Assert.AreEqual(0.2f, stats.MaxCarriedSpeed, 1e-6f);
    }
}
