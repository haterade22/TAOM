using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The merit sampler's geometry, extracted from <c>EnlistmentMeritMissionBehavior</c> so it is
/// reachable without a live Mission. Defaults are cohesion 25m, commander 40m, engagement 35m —
/// squared, 625 / 1600 / 1225 — and every input here is a SQUARED distance, negative meaning
/// "not observable this tick".
/// </summary>
[TestClass]
public class MeritGeometryAccumulatorTests
{
    private static MeritGeometryAccumulator Sut(MeritScoringConfig? config = null) =>
        new MeritGeometryAccumulator(config ?? new MeritScoringConfig());

    [TestMethod]
    public void AddSample_CaptainInsideCohesionRadius_CountsCohesionHit()
    {
        var sut = Sut();

        sut.AddSample(400f, -1f, -1f);

        Assert.AreEqual(1, sut.Samples);
        Assert.AreEqual(1, sut.CohesionHits);
        Assert.AreEqual(1f, sut.CohesionRatio, 0.0001f);
    }

    [TestMethod]
    public void AddSample_CaptainOutsideCohesionRadius_NoCohesionHit()
    {
        var sut = Sut();

        sut.AddSample(900f, -1f, -1f);

        Assert.AreEqual(1, sut.Samples);
        Assert.AreEqual(0, sut.CohesionHits);
    }

    [TestMethod]
    public void AddSample_CaptainAbsent_StillCountsTheSampleButNoHit()
    {
        var sut = Sut();

        sut.AddSample(-1f, -1f, -1f);

        Assert.AreEqual(1, sut.Samples);
        Assert.AreEqual(0, sut.CohesionHits);
        Assert.AreEqual(0f, sut.CohesionRatio, 0.0001f);
    }

    [TestMethod]
    public void AddSample_NonFiniteDistances_FailEveryGate()
    {
        var sut = Sut();

        sut.AddSample(float.NaN, float.NaN, float.NaN);

        Assert.AreEqual(0, sut.CohesionHits, "NaN must fail the gate, never score a free hit.");
        Assert.AreEqual(0, sut.CommanderHits);
        Assert.AreEqual(0, sut.EngagementHits);
        Assert.AreEqual(-1f, sut.AverageEnemyDistance, 0.0001f);
    }

    [TestMethod]
    public void AddSample_InfiniteEnemyDistance_NotCountedAndNotAveraged()
    {
        var sut = Sut();

        sut.AddSample(-1f, -1f, float.PositiveInfinity);

        Assert.AreEqual(0, sut.EngagementHits);
        Assert.AreEqual(-1f, sut.AverageEnemyDistance, 0.0001f);
    }

    [TestMethod]
    public void AddSample_EnemyBeyondEngagementRadius_NoHitButStillMeasured()
    {
        var sut = Sut();

        sut.AddSample(-1f, -1f, 2500f);

        Assert.AreEqual(0, sut.EngagementHits, "50m is outside the 35m engagement radius.");
        Assert.AreEqual(50f, sut.AverageEnemyDistance, 0.001f, "Role fit needs the measured distance regardless.");
    }

    [TestMethod]
    public void AverageEnemyDistance_TwoMeasuredSamples_ReturnsMeanOfUnsquaredDistances()
    {
        var sut = Sut();

        sut.AddSample(-1f, -1f, 100f);
        sut.AddSample(-1f, -1f, 400f);

        Assert.AreEqual(15f, sut.AverageEnemyDistance, 0.001f, "mean of 10m and 20m");
    }

    [TestMethod]
    public void AverageEnemyDistance_NeverMeasured_ReturnsNegative()
    {
        var sut = Sut();

        sut.AddSample(-1f, -1f, -1f);

        Assert.IsTrue(sut.AverageEnemyDistance < 0f, "Never-measured must stay negative so role fit fails closed.");
    }

    [TestMethod]
    public void CommanderProximityRatio_HalfTheSamplesHit_ReturnsHalf()
    {
        var sut = Sut();

        sut.AddSample(-1f, 100f, -1f);
        sut.AddSample(-1f, -1f, -1f);

        Assert.AreEqual(2, sut.Samples);
        Assert.AreEqual(1, sut.CommanderHits);
        Assert.AreEqual(0.5f, sut.CommanderProximityRatio, 0.0001f);
    }

    [TestMethod]
    public void Ratios_NoSamplesTaken_AllZero()
    {
        var sut = Sut();

        Assert.AreEqual(0f, sut.CohesionRatio, 0.0001f);
        Assert.AreEqual(0f, sut.CommanderProximityRatio, 0.0001f);
        Assert.AreEqual(0f, sut.EngagementRatio, 0.0001f);
    }

    [TestMethod]
    public void AddSample_NonFiniteConfiguredDistance_NothingCanScoreAHit()
    {
        var sut = Sut(new MeritScoringConfig
        {
            CohesionDistance = float.NaN,
            CommanderDistance = float.NegativeInfinity,
            EngagementDistance = -5f,
        });

        // Distance zero would clear any sane threshold; a poisoned config must still score nothing.
        sut.AddSample(0f, 0f, 0f);

        Assert.AreEqual(0, sut.CohesionHits);
        Assert.AreEqual(0, sut.CommanderHits);
        Assert.AreEqual(0, sut.EngagementHits);
    }

    [TestMethod]
    public void AddSample_NullConfig_NothingCanScoreAHit()
    {
        var sut = new MeritGeometryAccumulator(null);

        sut.AddSample(0f, 0f, 0f);

        Assert.AreEqual(1, sut.Samples);
        Assert.AreEqual(0, sut.CohesionHits);
        Assert.AreEqual(0, sut.CommanderHits);
        Assert.AreEqual(0, sut.EngagementHits);
    }
}
