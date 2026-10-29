using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>Point E: the union of the markers of the lord's hall's last fight area (docs/features/staged-siege.md).</summary>
[TestClass]
public class HallZoneTests
{
    private static HallMarker M(int area, float x, float y, float z = 10f, float r = 3f) => new(area, x, y, z, r);

    [TestMethod]
    public void Build_Empty_NoZone()
    {
        Assert.IsTrue(HallZone.Build(new List<HallMarker>()).IsEmpty);
        Assert.IsTrue(HallZone.Build(null).IsEmpty);
        Assert.IsFalse(HallZone.Build(new List<HallMarker>()).Contains(0f, 0f, 0f));
    }

    [TestMethod]
    public void Build_TakesEveryMarkerOfTheHighestAreaIndex()
    {
        var zone = HallZone.Build(new[] { M(1, 0, 0), M(3, 100, 100), M(2, 50, 50), M(3, 104, 100), M(3, 102, 106) });

        Assert.IsFalse(zone.IsEmpty);
        Assert.AreEqual(3, zone.AreaIndex);
        Assert.AreEqual(3, zone.MarkerCount);
        Assert.IsFalse(zone.Contains(0f, 0f, 10f), "area 1 is not point E");
        Assert.IsFalse(zone.Contains(50f, 50f, 10f), "area 2 is not point E");
        Assert.IsTrue(zone.Contains(100f, 100f, 10f));
        Assert.IsTrue(zone.Contains(104f, 100f, 10f));
        Assert.IsTrue(zone.Contains(102f, 106f, 10f));
    }

    [TestMethod]
    public void Build_SingleArea_ItIsE()
    {
        var zone = HallZone.Build(new[] { M(1, 5, 5) });

        Assert.AreEqual(1, zone.AreaIndex);
        Assert.AreEqual(1, zone.MarkerCount);
    }

    [TestMethod]
    public void Contains_RadiusEdgeIsInside_JustPastIsOut()
    {
        var zone = HallZone.Build(new[] { M(2, 0, 0, 0, 3f) });

        Assert.IsTrue(zone.Contains(3f, 0f, 0f), "exactly the radius is inside (vanilla IsPositionInRange is <=)");
        Assert.IsFalse(zone.Contains(3.001f, 0f, 0f));
    }

    [TestMethod]
    public void Contains_IsThreeDimensional()
    {
        var zone = HallZone.Build(new[] { M(2, 0, 0, 10f, 3f) });

        Assert.IsTrue(zone.Contains(0f, 0f, 12.9f));
        Assert.IsFalse(zone.Contains(0f, 0f, 13.5f), "a floor above the hall does not count");
        Assert.IsTrue(zone.Contains(2f, 2f, 11f));
        Assert.IsFalse(zone.Contains(2.5f, 2.5f, 12f), "3D distance past the radius");
    }

    [TestMethod]
    public void Contains_AnyMarkerInTheUnionCounts_EachWithItsOwnRadius()
    {
        var zone = HallZone.Build(new[] { M(2, 0, 0, 10f, 1f), M(2, 20, 0, 10f, 4f) });

        Assert.IsTrue(zone.Contains(0.9f, 0f, 10f));
        Assert.IsFalse(zone.Contains(2f, 0f, 10f), "the small marker's radius does not borrow the big one's");
        Assert.IsTrue(zone.Contains(23.5f, 0f, 10f));
    }

    [TestMethod]
    public void Contains_NaNPosition_IsNotInside()
    {
        var zone = HallZone.Build(new[] { M(2, 0, 0) });

        Assert.IsFalse(zone.Contains(float.NaN, 0f, 10f));
        Assert.IsFalse(zone.Contains(0f, float.NaN, 10f));
        Assert.IsFalse(zone.Contains(0f, 0f, float.NaN));
        Assert.IsFalse(zone.Contains(float.PositiveInfinity, 0f, 10f));
    }

    [TestMethod]
    public void Build_UnusableMarkers_AreDropped_NotCounted()
    {
        var zone = HallZone.Build(new[]
        {
            M(5, float.NaN, 0), M(5, 0, float.PositiveInfinity), M(5, 1, 1, float.NaN), M(5, 1, 1, 10f, float.NaN), M(5, 1, 1, 10f, -2f),
            M(5, 1, 1, 10f, 0f), M(2, 40, 40),
        });

        Assert.AreEqual(2, zone.AreaIndex, "area 5 held only unusable markers");
        Assert.AreEqual(1, zone.MarkerCount);
        Assert.IsTrue(HallZone.Build(new[] { M(1, float.NaN, 0) }).IsEmpty);
    }

    [TestMethod]
    public void Centroid_IsTheAverageOfTheEMarkers()
    {
        var zone = HallZone.Build(new[] { M(3, 100, 100, 10), M(3, 104, 100, 12), M(3, 102, 106, 14), M(1, 0, 0, 0) });

        Assert.AreEqual(102f, zone.Centroid.X, 1e-4f);
        Assert.AreEqual(102f, zone.Centroid.Y, 1e-4f);
        Assert.AreEqual(12f, zone.Centroid.Z, 1e-4f);
    }
}
