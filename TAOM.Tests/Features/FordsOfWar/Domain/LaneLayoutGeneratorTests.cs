using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>The generated layout: lane shapes, towers, keeps, capture points and camps (build-spec 3.3).</summary>
[TestClass]
public class LaneLayoutGeneratorTests
{
    private static readonly Vec3f WestBase = new(0f, 0f, 10f);
    private static readonly Vec3f EastBase = new(600f, 0f, 10f);

    private static MapLayout Generate(LayoutOptions? options = null) =>
        LaneLayoutGenerator.Generate("test_scene", WestBase, EastBase, options ?? new LayoutOptions());

    private static TowerSlot Tower(MapLayout l, MatchSide owner, int lane, int order) =>
        l.Towers.Single(t => t.Kind == TowerKind.Tower && t.Owner == owner && t.Lane == lane && t.Order == order);

    [TestMethod]
    public void LayoutOptions_Defaults_MatchSpec()
    {
        var o = new LayoutOptions();

        Assert.AreEqual(3, o.LaneCount);
        Assert.AreEqual(45f, o.LaneSpread);
        Assert.AreEqual(12f, o.WaypointStep);
        CollectionAssert.AreEqual(new[] { 0.36f, 0.20f }, o.TowerFractions.ToArray());
        Assert.AreEqual(0.04f, o.KeepInset);
    }

    [TestMethod]
    public void Generate_ThreeLanes_ProducesThreeLanesWithIndices()
    {
        var l = Generate();

        Assert.AreEqual("test_scene", l.SceneName);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, l.Lanes.Select(x => x.Index).ToArray());
    }

    [TestMethod]
    public void Generate_EveryLane_StartsAtWestBaseAndEndsAtEastBase()
    {
        foreach (var lane in Generate().Lanes)
        {
            Assert.IsTrue(lane.Path[0].Distance(WestBase) < 0.01f, "lane " + lane.Index + " start");
            Assert.IsTrue(lane.Path[lane.Path.Count - 1].Distance(EastBase) < 0.01f, "lane " + lane.Index + " end");
        }
    }

    [TestMethod]
    public void Generate_MiddleLane_IsTheStraightLineBetweenTheBases()
    {
        var middle = Generate().Lanes[1];

        Assert.IsTrue(middle.Path.All(p => System.Math.Abs(p.Y) < 1e-3f));
        Assert.IsTrue(middle.Path.All(p => System.Math.Abs(p.Z - 10f) < 1e-3f));
    }

    [TestMethod]
    public void Generate_EveryLane_WaypointSpacingStaysWithinTheStep()
    {
        foreach (var lane in Generate().Lanes)
            for (var i = 1; i < lane.Path.Count; i++)
                Assert.IsTrue(lane.Path[i].Distance(lane.Path[i - 1]) <= 12f + 1e-3f, "lane " + lane.Index + " segment " + i);
    }

    [TestMethod]
    public void Generate_MiddleLane_WaypointsAreEvenlySpaced()
    {
        var path = Generate().Lanes[1].Path;
        var first = path[1].Distance(path[0]);

        for (var i = 2; i < path.Count; i++)
            Assert.AreEqual(first, path[i].Distance(path[i - 1]), 0.01f);
    }

    [TestMethod]
    public void Generate_SideLanes_BowOutToTheLaneSpread()
    {
        var l = Generate();

        var maxOut0 = l.Lanes[0].Path.Max(p => System.Math.Abs(p.Y));
        var maxOut2 = l.Lanes[2].Path.Max(p => System.Math.Abs(p.Y));

        Assert.AreEqual(45f, maxOut0, 0.5f);
        Assert.AreEqual(45f, maxOut2, 0.5f);
    }

    [TestMethod]
    public void Generate_SideLanes_SitOnOppositeSidesOfTheMiddleLine()
    {
        var l = Generate();

        var y0 = l.Lanes[0].Path.Sum(p => p.Y);
        var y2 = l.Lanes[2].Path.Sum(p => p.Y);

        Assert.IsTrue(y0 * y2 < 0f, "side lanes must lie on opposite sides");
    }

    [TestMethod]
    public void Generate_SideLanes_ConvergeNearTheBasesAndRunParallelInTheMiddle()
    {
        var l = Generate();
        var side = l.Lanes[0].Path;

        var atTenMetres = side.First(p => p.X >= 10f);
        var atMid = side.First(p => p.X >= 300f);
        var atQuarterPast = side.First(p => p.X >= 200f);

        Assert.IsTrue(System.Math.Abs(atTenMetres.Y) < 5f, "close to the base the lane is still near the middle line");
        Assert.AreEqual(45f, System.Math.Abs(atMid.Y), 0.5f);
        Assert.AreEqual(45f, System.Math.Abs(atQuarterPast.Y), 0.5f);
    }

    [TestMethod]
    public void Generate_OuterLanes_MirrorEachOther()
    {
        var l = Generate();
        var a = l.Lanes[0].Path;
        var b = l.Lanes[2].Path;

        Assert.AreEqual(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.AreEqual(a[i].X, b[i].X, 0.01f);
            Assert.AreEqual(a[i].Y, -b[i].Y, 0.01f);
        }
    }

    [TestMethod]
    public void Generate_Towers_TwoPerSidePerLanePlusTwoKeeps()
    {
        var l = Generate();

        Assert.AreEqual(2 * 3 * 2 + 2, l.Towers.Count);
        Assert.AreEqual(2, l.Towers.Count(t => t.Kind == TowerKind.Keep));
        Assert.AreEqual(l.Towers.Count, l.Towers.Select(t => t.Id).Distinct().Count());
    }

    [TestMethod]
    public void Generate_Towers_OrderZeroIsTheOutermost()
    {
        var l = Generate();

        foreach (var side in new[] { MatchSide.West, MatchSide.East })
        {
            var ownBase = side == MatchSide.West ? WestBase : EastBase;
            for (var lane = 0; lane < 3; lane++)
            {
                var outer = Tower(l, side, lane, 0);
                var inner = Tower(l, side, lane, 1);
                Assert.IsTrue(outer.Position.Distance2D(ownBase) > inner.Position.Distance2D(ownBase),
                    side + " lane " + lane);
            }
        }
    }

    [TestMethod]
    public void Generate_MiddleLaneTowers_SitAtTheConfiguredFractionsFromEachBase()
    {
        var l = Generate();

        Assert.AreEqual(0.36f * 600f, Tower(l, MatchSide.West, 1, 0).Position.X, 0.5f);
        Assert.AreEqual(0.20f * 600f, Tower(l, MatchSide.West, 1, 1).Position.X, 0.5f);
        Assert.AreEqual(600f - 0.36f * 600f, Tower(l, MatchSide.East, 1, 0).Position.X, 0.5f);
        Assert.AreEqual(600f - 0.20f * 600f, Tower(l, MatchSide.East, 1, 1).Position.X, 0.5f);
    }

    [TestMethod]
    public void Generate_Towers_AreSymmetricBetweenTheSides()
    {
        var l = Generate();

        for (var lane = 0; lane < 3; lane++)
        {
            for (var order = 0; order < 2; order++)
            {
                var w = Tower(l, MatchSide.West, lane, order);
                var e = Tower(l, MatchSide.East, lane, order);
                Assert.AreEqual(w.Position.Distance2D(WestBase), e.Position.Distance2D(EastBase), 0.5f);
                Assert.AreEqual(w.Position.Y, e.Position.Y, 0.5f);
            }
        }
    }

    [TestMethod]
    public void Generate_Towers_SitOnTheirOwnLanePath()
    {
        var l = Generate();

        foreach (var t in l.Towers.Where(t => t.Kind == TowerKind.Tower))
        {
            var path = l.Lanes[t.Lane].Path;
            var nearest = path.Min(p => p.Distance2D(t.Position));
            Assert.IsTrue(nearest <= 6.01f, t.Id + " is " + nearest + " m from its lane");
        }
    }

    [TestMethod]
    public void Generate_TowerIds_NameSideLaneAndOrder()
    {
        var l = Generate();

        Assert.AreEqual("tower_west_2_1", Tower(l, MatchSide.West, 2, 1).Id);
        Assert.AreEqual("tower_east_0_0", Tower(l, MatchSide.East, 0, 0).Id);
    }

    [TestMethod]
    public void Generate_Towers_FaceTheEnemy()
    {
        var l = Generate();

        Assert.AreEqual(0f, Tower(l, MatchSide.West, 1, 0).Yaw, 0.01f);
        Assert.AreEqual((float)System.Math.PI, System.Math.Abs(Tower(l, MatchSide.East, 1, 0).Yaw), 0.01f);
    }

    [TestMethod]
    public void Generate_Keeps_SitInsetFromEachBaseOnTheMiddleLine()
    {
        var keeps = Generate().Towers.Where(t => t.Kind == TowerKind.Keep).ToList();

        var west = keeps.Single(k => k.Owner == MatchSide.West);
        var east = keeps.Single(k => k.Owner == MatchSide.East);

        Assert.AreEqual("keep_west", west.Id);
        Assert.AreEqual("keep_east", east.Id);
        Assert.AreEqual(-1, west.Lane);
        Assert.AreEqual(99, west.Order);
        Assert.AreEqual(0.04f * 600f, west.Position.X, 0.01f);
        Assert.AreEqual(600f - 0.04f * 600f, east.Position.X, 0.01f);
        Assert.AreEqual(0f, west.Position.Y, 0.01f);
    }

    [TestMethod]
    public void Generate_CapturePoints_OneBetweenEachPairOfNeighbourLanes()
    {
        var points = Generate().CapturePoints;

        Assert.AreEqual(2, points.Count);
        Assert.IsTrue(points.All(p => System.Math.Abs(p.Position.X - 300f) < 1f), "points sit at the middle of the length");
        Assert.AreEqual(2, points.Select(p => p.Id).Distinct().Count());
        Assert.AreEqual(22.5f, System.Math.Abs(points[0].Position.Y), 1f);
        Assert.IsTrue(points[0].Position.Y * points[1].Position.Y < 0f);
    }

    [TestMethod]
    public void Generate_Camps_OnePerSidePerGapAtThirtyFivePercent()
    {
        var camps = Generate().Camps;

        Assert.AreEqual(4, camps.Count);
        Assert.AreEqual(4, camps.Select(c => c.Id).Distinct().Count());
        Assert.AreEqual(2, camps.Count(c => c.Position.X < 300f));
        Assert.AreEqual(2, camps.Count(c => c.Position.X > 300f));
        Assert.IsTrue(camps.Where(c => c.Position.X < 300f).All(c => System.Math.Abs(c.Position.X - 0.35f * 600f) < 5f));
        Assert.IsTrue(camps.Where(c => c.Position.X > 300f).All(c => System.Math.Abs(c.Position.X - 0.65f * 600f) < 5f));
    }

    [TestMethod]
    public void Generate_Camps_LeaveRewardAndTroopForTheEngineLayer()
    {
        Assert.IsTrue(Generate().Camps.All(c => c.TroopId == "" && c.Count == 0 && c.Reward == 0));
    }

    [TestMethod]
    public void Generate_FourLanes_CountsAndMirror()
    {
        var l = Generate(new LayoutOptions(LaneCount: 4));

        Assert.AreEqual(4, l.Lanes.Count);
        Assert.AreEqual(2 * 4 * 2 + 2, l.Towers.Count);
        Assert.AreEqual(3, l.CapturePoints.Count);
        Assert.AreEqual(6, l.Camps.Count);
        for (var i = 0; i < l.Lanes[0].Path.Count; i++)
            Assert.AreEqual(l.Lanes[0].Path[i].Y, -l.Lanes[3].Path[i].Y, 0.01f);
        for (var i = 0; i < l.Lanes[1].Path.Count; i++)
            Assert.AreEqual(l.Lanes[1].Path[i].Y, -l.Lanes[2].Path[i].Y, 0.01f);
    }

    [TestMethod]
    public void Generate_FourLanes_NeighbourSeparationIsTheLaneSpread()
    {
        var l = Generate(new LayoutOptions(LaneCount: 4));
        var mid = 300f;

        var ys = l.Lanes.Select(lane => lane.Path.First(p => p.X >= mid).Y).OrderBy(y => y).ToArray();

        for (var i = 1; i < ys.Length; i++)
            Assert.AreEqual(45f, ys[i] - ys[i - 1], 0.5f);
    }

    [TestMethod]
    public void Generate_OneLane_HasNoCapturePointsOrCamps()
    {
        var l = Generate(new LayoutOptions(LaneCount: 1));

        Assert.AreEqual(1, l.Lanes.Count);
        Assert.AreEqual(0, l.CapturePoints.Count);
        Assert.AreEqual(0, l.Camps.Count);
    }

    [TestMethod]
    public void Generate_NonPositiveLaneCount_ClampsToOneLane()
    {
        Assert.AreEqual(1, Generate(new LayoutOptions(LaneCount: 0)).Lanes.Count);
    }

    [TestMethod]
    public void Generate_FractionsGivenAscending_StillMakeOrderZeroTheOutermost()
    {
        var l = Generate(new LayoutOptions(TowerFractions: new[] { 0.20f, 0.36f }));

        Assert.AreEqual(0.36f * 600f, Tower(l, MatchSide.West, 1, 0).Position.X, 0.5f);
    }

    [TestMethod]
    public void Generate_ThreeTowerFractions_ProduceThreeOrdersPerLane()
    {
        var l = Generate(new LayoutOptions(TowerFractions: new[] { 0.40f, 0.28f, 0.16f }));

        Assert.AreEqual(2 * 3 * 3 + 2, l.Towers.Count);
        Assert.AreEqual(0.16f * 600f, Tower(l, MatchSide.West, 1, 2).Position.X, 0.5f);
    }

    [TestMethod]
    public void Generate_BaseHeightsDiffer_HeightsAreInterpolated()
    {
        var l = LaneLayoutGenerator.Generate("s", new Vec3f(0, 0, 0), new Vec3f(600, 0, 60), new LayoutOptions());

        Assert.AreEqual(30f, l.Lanes[1].Path.First(p => p.X >= 300f).Z, 1f);
    }

    [TestMethod]
    public void Generate_BaseAlongYAxis_RotatesTheWholeLayout()
    {
        var l = LaneLayoutGenerator.Generate("s", new Vec3f(0, 0, 0), new Vec3f(0, 600, 0), new LayoutOptions());

        Assert.IsTrue(l.Lanes[1].Path.All(p => System.Math.Abs(p.X) < 1e-3f));
        Assert.AreEqual(45f, l.Lanes[0].Path.Max(p => System.Math.Abs(p.X)), 0.5f);
    }

    [TestMethod]
    public void Generate_BothBasesAtTheSamePoint_DoesNotThrow()
    {
        var p = new Vec3f(5, 5, 5);

        var l = LaneLayoutGenerator.Generate("s", p, p, new LayoutOptions());

        Assert.AreEqual(3, l.Lanes.Count);
        Assert.IsTrue(l.Lanes.All(x => x.Path.Count >= 2));
    }
}
