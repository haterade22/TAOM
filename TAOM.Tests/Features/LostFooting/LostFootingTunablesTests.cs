using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LostFooting.Domain;

namespace TAOM.Tests.Features.LostFooting;

/// <summary>The Lost Footing numbers are compiled defaults, not settings; this pins the shipped values.</summary>
[TestClass]
public class LostFootingTunablesTests
{
    [TestMethod]
    public void Defaults_AreThePlannedNumbers()
    {
        var t = new LostFootingTunables();

        Assert.AreEqual(1f, t.SweepSeconds);
        Assert.AreEqual(64, t.AgentsPerTick);
        Assert.AreEqual(10f, t.CensusIntervalSeconds);
        Assert.AreEqual(2f, t.OffMeshConfirmSeconds);
        Assert.AreEqual(2f, t.WaterStillSeconds);
        Assert.AreEqual(0.35f, t.StillSpeed);
        Assert.AreEqual(3f, t.MinDestinationMeters);
        Assert.AreEqual(5, t.SamplesPerClass);
        Assert.AreEqual(5f, t.HotspotCellMeters);
    }

    [TestMethod]
    public void EveryDefault_IsPositive_SoNoGateStartsOpen()
    {
        var t = new LostFootingTunables();

        Assert.IsTrue(t.SweepSeconds > 0f && t.AgentsPerTick > 0 && t.CensusIntervalSeconds > 0f);
        Assert.IsTrue(t.OffMeshConfirmSeconds > 0f && t.WaterStillSeconds > 0f && t.StillSpeed > 0f);
        Assert.IsTrue(t.MinDestinationMeters > 0f && t.SamplesPerClass > 0 && t.HotspotCellMeters > 0f);
    }
}
