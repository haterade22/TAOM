using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Walking a lane polyline: length, progress, nearest waypoint, points along the arc.</summary>
[TestClass]
public class LaneGeometryTests
{
    /// <summary>A straight lane along +X: eleven waypoints, 10 m apart, West base at x=0, East base at x=100.</summary>
    private static readonly IReadOnlyList<Vec3f> Straight = BuildStraight();

    private static IReadOnlyList<Vec3f> BuildStraight()
    {
        var list = new List<Vec3f>();
        for (var i = 0; i <= 10; i++)
            list.Add(new Vec3f(i * 10f, 0f, 0f));
        return list;
    }

    [TestMethod]
    public void PathLength_StraightLane_IsHundred()
    {
        Assert.AreEqual(100f, LaneGeometry.PathLength(Straight), 1e-4f);
    }

    [TestMethod]
    public void PathLength_FewerThanTwoPoints_IsZero()
    {
        Assert.AreEqual(0f, LaneGeometry.PathLength(new List<Vec3f>()));
        Assert.AreEqual(0f, LaneGeometry.PathLength(new List<Vec3f> { new Vec3f(1, 2, 3) }));
    }

    [TestMethod]
    public void Progress_MidLane_IsHalf()
    {
        Assert.AreEqual(0.5f, LaneGeometry.Progress(Straight, new Vec3f(50, 7, 0)), 1e-4f);
    }

    [TestMethod]
    public void Progress_BeforeStartAndPastEnd_ClampsToZeroAndOne()
    {
        Assert.AreEqual(0f, LaneGeometry.Progress(Straight, new Vec3f(-20, 0, 0)), 1e-4f);
        Assert.AreEqual(1f, LaneGeometry.Progress(Straight, new Vec3f(180, 0, 0)), 1e-4f);
    }

    [TestMethod]
    public void Progress_BentLane_FollowsArcLength()
    {
        var bent = new List<Vec3f> { new Vec3f(0, 0, 0), new Vec3f(30, 0, 0), new Vec3f(30, 10, 0) };

        Assert.AreEqual(0.75f, LaneGeometry.Progress(bent, new Vec3f(30, 0, 0)), 1e-4f);
        Assert.AreEqual(0.875f, LaneGeometry.Progress(bent, new Vec3f(35, 5, 0)), 1e-4f);
    }

    [TestMethod]
    public void Progress_EmptyPath_IsZero()
    {
        Assert.AreEqual(0f, LaneGeometry.Progress(new List<Vec3f>(), new Vec3f(1, 1, 1)));
    }

    [TestMethod]
    public void NearestIndex_PointNearWaypointSeven_ReturnsSeven()
    {
        Assert.AreEqual(7, LaneGeometry.NearestIndex(Straight, new Vec3f(71, 3, 0)));
    }

    [TestMethod]
    public void NearestIndex_EmptyPath_ReturnsMinusOne()
    {
        Assert.AreEqual(-1, LaneGeometry.NearestIndex(new List<Vec3f>(), new Vec3f(0, 0, 0)));
    }

    [TestMethod]
    public void PointAt_ArcDistanceOnBentLane_InterpolatesOnTheRightSegment()
    {
        var bent = new List<Vec3f> { new Vec3f(0, 0, 0), new Vec3f(30, 0, 0), new Vec3f(30, 10, 0) };

        Assert.AreEqual(new Vec3f(15, 0, 0), LaneGeometry.PointAt(bent, 15f));
        Assert.AreEqual(new Vec3f(30, 5, 0), LaneGeometry.PointAt(bent, 35f));
        Assert.AreEqual(new Vec3f(30, 10, 0), LaneGeometry.PointAt(bent, 999f));
        Assert.AreEqual(new Vec3f(0, 0, 0), LaneGeometry.PointAt(bent, -5f));
    }
}
