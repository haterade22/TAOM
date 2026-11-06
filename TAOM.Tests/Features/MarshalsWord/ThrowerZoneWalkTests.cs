using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>How the control step measures a company crossing the gaps at which a run could stand (build-spec 9.4): the zone is above 12 m and at most 36 m for a 32 m stand gap.</summary>
[TestClass]
public class ThrowerZoneWalkTests
{
    private static ThrowerZoneWalk Zone() => new(32f);

    private static GroundPoint At(float y) => new(0f, y);

    [TestMethod]
    public void Slowest_IsNotANumber_UntilTwoSamplesInTheZone()
    {
        var zone = Zone();

        zone.Sample(At(60f), 10.0, 30f);

        Assert.IsTrue(float.IsNaN(zone.Slowest));
        Assert.AreEqual(0f, zone.Walked, 1e-6f);
    }

    [TestMethod]
    public void ASampleOneMetreOnAQuarterSecondLater_IsFourMetresASecond()
    {
        var zone = Zone();

        zone.Sample(At(60f), 10.0, 30f);
        zone.Sample(At(61f), 10.25, 29f);

        Assert.AreEqual(4f, zone.Slowest, 1e-4f);
        Assert.AreEqual(1f, zone.Walked, 1e-4f);
    }

    [TestMethod]
    public void ASampleUnderAQuarterSecondAfterTheLast_IsIgnored()
    {
        var zone = Zone();

        zone.Sample(At(60f), 10.0, 30f);
        zone.Sample(At(60.5f), 10.1, 29.5f);
        zone.Sample(At(61f), 10.25, 29f);

        Assert.AreEqual(4f, zone.Slowest, 1e-4f, "without the wait rule the first pair reads about 5 m/s and the second about 3.3");
        Assert.AreEqual(1f, zone.Walked, 1e-4f);
    }

    [TestMethod]
    public void AGapOfTwelveOrLess_IsOutside_AndBreaksThePair()
    {
        var zone = Zone();

        zone.Sample(At(60f), 10.0, 30f);
        zone.Sample(At(70f), 10.25, 12f);
        zone.Sample(At(71f), 10.5, 11f);

        Assert.IsTrue(float.IsNaN(zone.Slowest), "without the rule the pair reads 40 m/s");
    }

    [TestMethod]
    public void AGapAboveTheTop_IsOutside()
    {
        var zone = Zone();

        zone.Sample(At(60f), 10.0, 37f);
        zone.Sample(At(61f), 10.25, 36.5f);

        Assert.IsTrue(float.IsNaN(zone.Slowest), "the first sample is above the stand gap plus the arrival distance, so only one sample is inside");
        Assert.AreEqual(0f, zone.Walked, 1e-6f);
    }

    [TestMethod]
    public void TheLeastSpeedWins_AndWalkedAddsUp()
    {
        var zone = Zone();

        zone.Sample(At(60f), 10.0, 30f);
        zone.Sample(At(61f), 10.25, 29f);
        zone.Sample(At(61.1f), 10.5, 28.9f);

        Assert.AreEqual(0.4f, zone.Slowest, 1e-3f);
        Assert.AreEqual(1.1f, zone.Walked, 1e-3f);
    }

    [TestMethod]
    public void NoPosition_BreaksThePair()
    {
        var zone = Zone();

        zone.Sample(At(60f), 10.0, 30f);
        zone.Sample(null, 10.25, 29f);
        zone.Sample(At(61f), 10.5, 28f);

        Assert.IsTrue(float.IsNaN(zone.Slowest));
    }

    [TestMethod]
    public void APositionThatIsNotANumber_BreaksThePair()
    {
        var zone = Zone();

        zone.Sample(At(60f), 10.0, 30f);
        zone.Sample(At(float.NaN), 10.25, 29f);
        zone.Sample(At(61f), 10.5, 28f);

        Assert.IsTrue(float.IsNaN(zone.Slowest));
    }
}
