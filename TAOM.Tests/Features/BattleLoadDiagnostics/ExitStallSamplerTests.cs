using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

/// <summary>
/// Pure decision logic of the exit-stall stack sampler (#331 round 2). The threading +
/// Thread.Suspend capture is game-only per ADR-008; the schedule decision is what unit
/// tests can pin: samples fire once each, in order, at their thresholds.
/// </summary>
[TestClass]
public class ExitStallSamplerTests
{
    [TestMethod]
    public void ShouldSample_BeforeFirstThreshold_ReturnsFalse()
    {
        // 14.9s < the 15s first threshold — a healthy ~9.5s tournament exit (the measured
        // post-fix residual) must never produce a false stall sample.
        Assert.IsFalse(ExitStallSampler.ShouldSample(elapsedSeconds: 14.9, samplesTaken: 0));
    }

    [TestMethod]
    public void ShouldSample_AtEachThreshold_FiresInOrder()
    {
        Assert.IsTrue(ExitStallSampler.ShouldSample(15.0, 0));
        Assert.IsFalse(ExitStallSampler.ShouldSample(15.0, 1), "second sample must wait for its own threshold");
        Assert.IsTrue(ExitStallSampler.ShouldSample(30.0, 1));
        Assert.IsFalse(ExitStallSampler.ShouldSample(30.0, 2), "third sample must wait for its own threshold");
        Assert.IsTrue(ExitStallSampler.ShouldSample(60.0, 2));
    }

    [TestMethod]
    public void ShouldSample_AllSamplesTaken_ReturnsFalse()
    {
        Assert.IsFalse(ExitStallSampler.ShouldSample(999.0, 3));
    }

    [TestMethod]
    public void ShouldSample_LateArm_CatchesUpOneAtATime()
    {
        // Poll granularity means elapsed may already be past several thresholds; each poll
        // still takes exactly one sample so the samples stay distinct in time.
        Assert.IsTrue(ExitStallSampler.ShouldSample(60.0, 0));
        Assert.IsTrue(ExitStallSampler.ShouldSample(60.0, 1));
        Assert.IsTrue(ExitStallSampler.ShouldSample(60.0, 2));
        Assert.IsFalse(ExitStallSampler.ShouldSample(60.0, 3));
    }
}
