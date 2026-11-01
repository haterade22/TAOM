using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>
/// The per-lane path table: it must give the numbers <see cref="LaneGeometry.ArcPosition"/> gives, then answer the lead
/// group, waypoint index and hold point questions from the cached segments.
/// </summary>
[TestClass]
public class LanePathTests
{
    /// <summary>A straight lane along +X with a vertex every 100 m: West base at x=0, East base at x=1000.</summary>
    private static readonly IReadOnlyList<Vec3f> Long = Enumerable.Range(0, 11).Select(i => new Vec3f(i * 100f, 0f, 0f)).ToList();

    private static List<Vec3f> At(float x, int count, float y = 0f) =>
        Enumerable.Range(0, count).Select(_ => new Vec3f(x, y, 0f)).ToList();

    /// <summary>A bent lane with a zero-length segment (a repeated vertex), so ties and degenerate segments are all in play.</summary>
    private static readonly IReadOnlyList<Vec3f> Bent = new List<Vec3f>
    {
        new(0f, 0f, 0f), new(100f, 0f, 0f), new(100f, 0f, 0f), new(100f, 100f, 0f), new(40f, 160f, 0f), new(0f, 160f, 0f),
    };

    // ArcPosition parity with LaneGeometry

    [TestMethod]
    public void ArcPosition_EveryVertexOfABentLane_EqualsLaneGeometryExactly()
    {
        var table = new LanePath(Bent);

        foreach (var vertex in Bent)
            Assert.AreEqual(LaneGeometry.ArcPosition(Bent, vertex), table.ArcPosition(vertex));
    }

    [TestMethod]
    public void ArcPosition_PointEquidistantFromTwoSegments_EqualsLaneGeometryAndTheFirstSegmentWins()
    {
        var table = new LanePath(Bent);
        var tie = new Vec3f(50f, 50f, 0f);

        Assert.AreEqual(LaneGeometry.ArcPosition(Bent, tie), table.ArcPosition(tie));
        Assert.AreEqual(50f, table.ArcPosition(tie), 1e-3f);
    }

    [TestMethod]
    public void ArcPosition_OutsideTheCornerAtAZeroLengthSegment_EqualsLaneGeometry()
    {
        var table = new LanePath(Bent);

        foreach (var point in new[] { new Vec3f(150f, -50f, 0f), new Vec3f(100f, -1f, 0f), new Vec3f(130f, 30f, 0f) })
            Assert.AreEqual(LaneGeometry.ArcPosition(Bent, point), table.ArcPosition(point));
    }

    [TestMethod]
    public void ArcPosition_PointsPastBothEnds_EqualsLaneGeometry()
    {
        var table = new LanePath(Bent);
        var straight = new LanePath(Long);

        foreach (var point in new[] { new Vec3f(-80f, 5f, 0f), new Vec3f(-80f, 200f, 0f), new Vec3f(-3f, 170f, 0f), new Vec3f(-30f, 160f, 0f) })
            Assert.AreEqual(LaneGeometry.ArcPosition(Bent, point), table.ArcPosition(point));

        foreach (var point in new[] { new Vec3f(-40f, 0f, 0f), new Vec3f(1400f, 9f, 0f) })
            Assert.AreEqual(LaneGeometry.ArcPosition(Long, point), straight.ArcPosition(point));
    }

    [TestMethod]
    public void ArcPosition_GridOfPointsAroundTheLane_EqualsLaneGeometryExactly()
    {
        var table = new LanePath(Bent);
        var checkedPoints = 0;

        for (var x = -50f; x <= 200f; x += 7.3f)
        {
            for (var y = -50f; y <= 220f; y += 11.7f)
            {
                var point = new Vec3f(x, y, 0f);
                Assert.AreEqual(LaneGeometry.ArcPosition(Bent, point), table.ArcPosition(point), $"at ({x}, {y})");
                checkedPoints++;
            }
        }

        Assert.IsTrue(checkedPoints > 500);
    }

    [TestMethod]
    public void ArcPosition_FewerThanTwoPoints_IsZero()
    {
        Assert.AreEqual(0f, new LanePath(new List<Vec3f>()).ArcPosition(new Vec3f(1f, 2f, 0f)));
        Assert.AreEqual(0f, new LanePath(new List<Vec3f> { new(5f, 5f, 0f) }).ArcPosition(new Vec3f(1f, 2f, 0f)));
    }

    [TestMethod]
    public void Length_BentLane_EqualsLaneGeometryPathLength()
    {
        Assert.AreEqual(LaneGeometry.PathLength(Bent), new LanePath(Bent).Length);
        Assert.AreEqual(0f, new LanePath(new List<Vec3f>()).Length);
    }

    // Lead group

    [TestMethod]
    public void LeadGroup_WestFreshSpawnsAtTheBase_IgnoresTheStragglers()
    {
        var positions = At(300f, 40);
        positions.AddRange(At(3f, 10));

        var lead = new LanePath(Long).LeadGroup(MatchSide.West, positions, 25f)!.Value;

        Assert.AreEqual(40, lead.Count);
        Assert.AreEqual(300f, lead.Position.X, 1e-3f);
        Assert.AreEqual(300f, lead.Arc, 1e-3f);
    }

    [TestMethod]
    public void LeadGroup_EastAdvancesTowardLowerArc_IgnoresTheStragglersAtItsBase()
    {
        var positions = At(700f, 40);
        positions.AddRange(At(997f, 10));

        var lead = new LanePath(Long).LeadGroup(MatchSide.East, positions, 25f)!.Value;

        Assert.AreEqual(40, lead.Count);
        Assert.AreEqual(700f, lead.Position.X, 1e-3f);
        Assert.AreEqual(700f, lead.Arc, 1e-3f);
    }

    [TestMethod]
    public void LeadGroup_UnitsInsideTheBandOfTheFront_AreAveraged()
    {
        var positions = new List<Vec3f> { new(300f, 0f, 0f), new(310f, 4f, 0f), new(330f, 8f, 0f) };

        var lead = new LanePath(Long).LeadGroup(MatchSide.West, positions, 25f)!.Value;

        Assert.AreEqual(2, lead.Count);
        Assert.AreEqual(new Vec3f(305f, 2f, 0f), lead.Position);
        Assert.AreEqual(305f, lead.Arc, 1e-3f);
    }

    [TestMethod]
    public void LeadGroup_UnitExactlyOneBandAheadOrBehindTheFront_IsInTheGroup()
    {
        var positions = new List<Vec3f> { new(305f, 0f, 0f), new(330f, 0f, 0f), new(330f, 0f, 0f), new(355f, 0f, 0f) };

        var lead = new LanePath(Long).LeadGroup(MatchSide.West, positions, 25f)!.Value;

        Assert.AreEqual(4, lead.Count);
    }

    [TestMethod]
    public void LeadGroup_MembersOnABentLane_ArcIsTheMeanOfTheirArcs()
    {
        var bent = new List<Vec3f> { new(0f, 0f, 0f), new(30f, 0f, 0f), new(30f, 100f, 0f) };
        var positions = new List<Vec3f> { new(30f, 40f, 0f), new(30f, 60f, 0f) };

        var lead = new LanePath(bent).LeadGroup(MatchSide.West, positions, 25f)!.Value;

        Assert.AreEqual(80f, lead.Arc, 1e-3f);
    }

    [TestMethod]
    public void LeadGroup_NoPositions_IsNull()
    {
        Assert.IsNull(new LanePath(Long).LeadGroup(MatchSide.West, new List<Vec3f>(), 25f));
    }

    [TestMethod]
    public void LeadGroup_UnreadablePosition_IsSkipped()
    {
        var positions = new List<Vec3f> { new(float.NaN, 0f, 0f), new(200f, 0f, 0f) };

        var lead = new LanePath(Long).LeadGroup(MatchSide.East, positions, 25f)!.Value;

        Assert.AreEqual(1, lead.Count);
        Assert.AreEqual(200f, lead.Position.X, 1e-3f);
    }

    [TestMethod]
    public void LeadGroup_EmptyPath_IsNull()
    {
        Assert.IsNull(new LanePath(new List<Vec3f>()).LeadGroup(MatchSide.West, At(5f, 3), 25f));
    }

    [TestMethod]
    public void LeadGroup_OneRunawayUnitAheadOf43_TheMainBodyLeads()
    {
        var positions = At(300f, 43);
        positions.Add(new Vec3f(360f, 0f, 0f));

        var lead = new LanePath(Long).LeadGroup(MatchSide.West, positions, 25f)!.Value;

        Assert.AreEqual(43, lead.Count);
        Assert.AreEqual(300f, lead.Position.X, 1e-3f);
    }

    [TestMethod]
    public void LeadGroup_OneRunawayUnitAheadOf43OnTheEastSide_TheMainBodyLeads()
    {
        var positions = At(700f, 43);
        positions.Add(new Vec3f(640f, 0f, 0f));

        var lead = new LanePath(Long).LeadGroup(MatchSide.East, positions, 25f)!.Value;

        Assert.AreEqual(43, lead.Count);
        Assert.AreEqual(700f, lead.Position.X, 1e-3f);
    }

    [TestMethod]
    public void LeadGroup_FourUnitsOfWhichTwoRunAhead_TheThirdMostAdvancedSetsTheFront()
    {
        var positions = new List<Vec3f> { new(500f, 0f, 0f), new(480f, 0f, 0f), new(100f, 0f, 0f), new(100f, 0f, 0f) };

        var lead = new LanePath(Long).LeadGroup(MatchSide.West, positions, 25f)!.Value;

        Assert.AreEqual(2, lead.Count);
        Assert.AreEqual(100f, lead.Position.X, 1e-3f);
    }

    [TestMethod]
    public void LeadGroup_SeventyUnitsWithTenAhead_TheSeventhMostAdvancedSetsTheFront()
    {
        var positions = At(200f, 60);
        positions.AddRange(At(260f, 5));
        positions.AddRange(At(330f, 5));

        var lead = new LanePath(Long).LeadGroup(MatchSide.West, positions, 25f)!.Value;

        Assert.AreEqual(5, lead.Count);
        Assert.AreEqual(260f, lead.Position.X, 1e-3f);
    }

    [TestMethod]
    public void LeadGroup_ThirtyUnits_UsesRankThreeNotFour()
    {
        var positions = At(100f, 27);
        positions.AddRange(At(700f, 3));

        var lead = new LanePath(Long).LeadGroup(MatchSide.West, positions, 25f)!.Value;

        Assert.AreEqual(3, lead.Count);
        Assert.AreEqual(700f, lead.Position.X, 1e-3f);
    }

    // Waypoint index

    [TestMethod]
    public void WaypointIndex_WestLeadBetweenWaypoints_IsTheNextOne()
    {
        Assert.AreEqual(3, new LanePath(Long).WaypointIndex(MatchSide.West, 250f, 6f));
    }

    [TestMethod]
    public void WaypointIndex_WestLeadWithinReachInArc_CountsTheWaypointReached()
    {
        var table = new LanePath(Long);

        Assert.AreEqual(4, table.WaypointIndex(MatchSide.West, 295f, 6f));
        Assert.AreEqual(3, table.WaypointIndex(MatchSide.West, 293f, 6f));
    }

    [TestMethod]
    public void WaypointIndex_EastLeadBetweenWaypoints_IsTheNextOneTowardTheWestBase()
    {
        var table = new LanePath(Long);

        Assert.AreEqual(7, table.WaypointIndex(MatchSide.East, 750f, 6f));
        Assert.AreEqual(6, table.WaypointIndex(MatchSide.East, 705f, 6f));
        Assert.AreEqual(7, table.WaypointIndex(MatchSide.East, 707f, 6f));
    }

    [TestMethod]
    public void WaypointIndex_LeadPastTheEnemyEnd_ClampsToTheLastWaypoint()
    {
        var table = new LanePath(Long);

        Assert.AreEqual(10, table.WaypointIndex(MatchSide.West, 1000f, 6f));
        Assert.AreEqual(0, table.WaypointIndex(MatchSide.East, 0f, 6f));
    }

    [TestMethod]
    public void WaypointIndex_EmptyPath_IsMinusOne()
    {
        Assert.AreEqual(-1, new LanePath(new List<Vec3f>()).WaypointIndex(MatchSide.West, 10f, 6f));
    }

    // Hold point

    [TestMethod]
    public void HoldPoint_WestHolderBeforeTheMidpoint_SnapsToTheVertexBehindTheHoldArc()
    {
        var p = new LanePath(Long).HoldPoint(new Vec3f(640f, 0f, 0f), MatchSide.West, 200f, towerIsKeep: false);

        Assert.AreEqual(new Vec3f(400f, 0f, 0f), p);
    }

    [TestMethod]
    public void HoldPoint_EastHolderBeforeTheMidpoint_SnapsToTheVertexBehindTheHoldArc()
    {
        var p = new LanePath(Long).HoldPoint(new Vec3f(360f, 0f, 0f), MatchSide.East, 200f, towerIsKeep: false);

        Assert.AreEqual(new Vec3f(600f, 0f, 0f), p);
    }

    [TestMethod]
    public void HoldPoint_WestHoldArcPastTheMidpoint_HoldsAtTheMidpointVertex()
    {
        var p = new LanePath(Long).HoldPoint(new Vec3f(640f, 0f, 0f), MatchSide.West, 60f, towerIsKeep: false);

        Assert.AreEqual(new Vec3f(500f, 0f, 0f), p);
    }

    [TestMethod]
    public void HoldPoint_EastHoldArcPastTheMidpoint_HoldsAtTheMidpointVertex()
    {
        var p = new LanePath(Long).HoldPoint(new Vec3f(360f, 0f, 0f), MatchSide.East, 60f, towerIsKeep: false);

        Assert.AreEqual(new Vec3f(500f, 0f, 0f), p);
    }

    [TestMethod]
    public void HoldPoint_TargetIsAKeep_HoldsAtTheMidpointWhateverTheDistance()
    {
        var table = new LanePath(Long);

        Assert.AreEqual(new Vec3f(500f, 0f, 0f), table.HoldPoint(new Vec3f(960f, 0f, 0f), MatchSide.West, 60f, towerIsKeep: true));
        Assert.AreEqual(new Vec3f(500f, 0f, 0f), table.HoldPoint(new Vec3f(40f, 0f, 0f), MatchSide.East, 60f, towerIsKeep: true));
    }

    [TestMethod]
    public void HoldPoint_MidpointBetweenVertices_SnapsToTheVertexOnTheHoldersSide()
    {
        var uneven = new List<Vec3f>
        {
            new(0f, 0f, 0f), new(130f, 0f, 0f), new(260f, 0f, 0f), new(410f, 0f, 0f), new(600f, 0f, 0f), new(800f, 0f, 0f), new(1000f, 0f, 0f),
        };
        var table = new LanePath(uneven);

        Assert.AreEqual(new Vec3f(410f, 0f, 0f), table.HoldPoint(new Vec3f(960f, 0f, 0f), MatchSide.West, 60f, towerIsKeep: true));
        Assert.AreEqual(new Vec3f(600f, 0f, 0f), table.HoldPoint(new Vec3f(40f, 0f, 0f), MatchSide.East, 60f, towerIsKeep: true));
    }

    [TestMethod]
    public void HoldPoint_DistanceLongerThanTheLane_ClampsToTheHoldersBaseEnd()
    {
        var table = new LanePath(Long);

        Assert.AreEqual(new Vec3f(0f, 0f, 0f), table.HoldPoint(new Vec3f(20f, 0f, 0f), MatchSide.West, 80f, towerIsKeep: false));
        Assert.AreEqual(new Vec3f(1000f, 0f, 0f), table.HoldPoint(new Vec3f(980f, 0f, 0f), MatchSide.East, 80f, towerIsKeep: false));
    }

    [TestMethod]
    public void HoldPoint_EmptyPath_IsTheZeroVector()
    {
        Assert.AreEqual(default(Vec3f), new LanePath(new List<Vec3f>()).HoldPoint(new Vec3f(1f, 1f, 1f), MatchSide.West, 60f, false));
    }
}
