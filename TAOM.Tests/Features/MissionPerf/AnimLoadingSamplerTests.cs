using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.MissionPerf;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The clip-loading sampler's cost gate: it measures the native call 32 times, turns per-frame sampling on
/// only when the median is within the 20 us budget, and turns itself off with one queued reason line on an
/// adapter exception. The clock is fake (ticksPerSecond 1,000,000, so a tick is a microsecond) and moves
/// only inside the adapter call, so each measured cost is exactly the scripted one.
/// </summary>
[TestClass]
public class AnimLoadingSamplerTests
{
    private long _now;
    private IAnimationLoadingAdapter _adapter = null!;

    [TestInitialize]
    public void Setup()
    {
        _now = 1000;
        _adapter = Substitute.For<IAnimationLoadingAdapter>();
    }

    private AnimLoadingSampler Sampler() => new AnimLoadingSampler(_adapter, () => _now, 1_000_000);

    private void ScriptCosts(long[] costs, bool result = false)
    {
        var i = 0;
        _adapter.IsAnyAnimationLoadingFromDisk().Returns(_ =>
        {
            _now += costs[Math.Min(i, costs.Length - 1)];
            i++;
            return result;
        });
    }

    [TestMethod]
    public void MeasureCost_UnderBudget_EnablesSampling_AndReportsTheMedian()
    {
        var costs = new long[32];
        for (var k = 0; k < 32; k++)
            costs[k] = (k * 7 % 32) + 1;
        ScriptCosts(costs);
        var sampler = Sampler();

        var line = sampler.MeasureCost();

        Assert.IsTrue(sampler.Enabled);
        Assert.AreEqual(HitchProbeLines.BuildAnimCostLine(16.5, 32, 20, true), line);
        _adapter.Received(32).IsAnyAnimationLoadingFromDisk();
    }

    [TestMethod]
    public void MeasureCost_OverBudget_DisablesSampling()
    {
        var costs = new long[32];
        for (var k = 0; k < 32; k++)
            costs[k] = 25;
        ScriptCosts(costs);
        var sampler = Sampler();

        var line = sampler.MeasureCost();

        Assert.IsFalse(sampler.Enabled);
        StringAssert.EndsWith(line, "over budget, sampling is off for this process");
        Assert.IsFalse(sampler.Sample(), "An over-budget sampler never samples.");
        _adapter.Received(32).IsAnyAnimationLoadingFromDisk();
    }

    [TestMethod]
    public void MeasureCost_AdapterThrows_DisablesSampling_AndQueuesTheFaultLine()
    {
        var boom = new InvalidOperationException("native gone");
        _adapter.IsAnyAnimationLoadingFromDisk().Returns(_ => throw boom);
        var sampler = Sampler();

        Assert.IsNull(sampler.MeasureCost());

        Assert.IsFalse(sampler.Enabled);
        Assert.AreEqual(HitchProbeLines.BuildAnimFault(boom), sampler.TakePendingFault());
        Assert.IsNull(sampler.TakePendingFault());
    }

    [TestMethod]
    public void Sample_BeforeMeasureCost_ReturnsFalseWithoutCallingTheAdapter()
    {
        _adapter.IsAnyAnimationLoadingFromDisk().Returns(true);
        var sampler = Sampler();

        Assert.IsFalse(sampler.Sample());

        _adapter.DidNotReceive().IsAnyAnimationLoadingFromDisk();
    }

    [TestMethod]
    public void Sample_AdapterThrows_DisablesSampling_AndReportsTheFaultOnce()
    {
        var calls = 0;
        var boom = new InvalidOperationException("mid-battle");
        _adapter.IsAnyAnimationLoadingFromDisk().Returns(_ =>
        {
            calls++;
            _now += 1;
            if (calls > 32)
                throw boom;
            return true;
        });
        var sampler = Sampler();
        sampler.MeasureCost();
        Assert.IsTrue(sampler.Enabled);

        Assert.IsFalse(sampler.Sample());
        Assert.IsFalse(sampler.Sample());

        Assert.IsFalse(sampler.Enabled);
        Assert.AreEqual(33, calls, "Once off, the sampler never calls the adapter again.");
        Assert.AreEqual(HitchProbeLines.BuildAnimFault(boom), sampler.TakePendingFault());
        Assert.IsNull(sampler.TakePendingFault());
    }

    [TestMethod]
    public void MeasureCost_SecondCall_DoesNothingAndReturnsNull()
    {
        ScriptCosts(new long[] { 2 }, result: true);
        var sampler = Sampler();
        Assert.IsNotNull(sampler.MeasureCost());

        Assert.IsNull(sampler.MeasureCost());

        _adapter.Received(32).IsAnyAnimationLoadingFromDisk();
        Assert.IsTrue(sampler.Sample(), "The adapter reports a clip loading.");
    }
}
