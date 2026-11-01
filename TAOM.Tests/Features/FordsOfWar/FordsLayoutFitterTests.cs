using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
public class FordsLayoutFitterTests
{
    private static Vec3f Ground(Vec3f p) => new(p.X, p.Y, 7f);

    private static MapLayout Generated() =>
        LaneLayoutGenerator.Generate("test_scene", new Vec3f(0f, 0f, 0f), new Vec3f(1000f, 0f, 0f), new LayoutOptions());

    private static Func<Vec3f, bool> NotIn(Vec3f center, float radius) => p => p.Distance2D(center) > radius;

    private static float DistanceToPolyline(Vec3f point, System.Collections.Generic.IReadOnlyList<Vec3f> path)
    {
        var best = float.MaxValue;
        for (var i = 1; i < path.Count; i++)
        {
            var a = path[i - 1];
            var b = path[i];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var t = Math.Max(0f, Math.Min(1f, ((point.X - a.X) * dx + (point.Y - a.Y) * dy) / (dx * dx + dy * dy)));
            best = Math.Min(best, point.Distance2D(new Vec3f(a.X + dx * t, a.Y + dy * t, 0f)));
        }

        return best;
    }

    [TestMethod]
    public void Fit_EverythingWalkable_SnapsHeightsAndChangesNothingElse()
    {
        var layout = Generated();

        var fit = FordsLayoutFitter.Fit(layout, Ground, _ => true);

        Assert.AreEqual(0, fit.Moved);
        Assert.AreEqual(0, fit.Dropped);
        Assert.AreEqual(0, fit.Unfixed);
        Assert.IsFalse(fit.Rejected);
        Assert.AreEqual(layout.Towers.Count, fit.Layout.Towers.Count);
        Assert.IsTrue(fit.Layout.Towers.All(t => t.Position.Z == 7f));
        Assert.IsTrue(fit.Layout.Lanes.SelectMany(l => l.Path).All(p => p.Z == 7f));
        Assert.AreEqual(layout.Towers[0].Position.X, fit.Layout.Towers[0].Position.X);
    }

    [TestMethod]
    public void Fit_TowerInAHole_SlidesAlongItsLaneAndKeepsItsId()
    {
        var layout = Generated();
        var sideTower = layout.Towers.First(t => t.Kind == TowerKind.Tower && Math.Abs(t.Position.Y) > 30f);

        var fit = FordsLayoutFitter.Fit(layout, Ground, NotIn(sideTower.Position, 3f));

        var slid = fit.Layout.Towers.Single(t => t.Id == sideTower.Id);
        var distance = slid.Position.Distance2D(sideTower.Position);
        Assert.IsTrue(distance > 3f && distance <= 31f, $"slid {distance} m");
        Assert.IsTrue(DistanceToPolyline(slid.Position, fit.Layout.Lanes.Single(l => l.Index == sideTower.Lane).Path) < 1f);
        Assert.AreEqual(sideTower.Yaw, slid.Yaw);
        Assert.AreEqual(0, fit.Dropped);
        Assert.IsFalse(fit.Rejected);
        Assert.AreEqual(sideTower.Id, fit.MovedTowers.Single().Id);
        Assert.AreEqual(10f, fit.MovedTowers.Single().Distance, 0.001f, "the first slide step toward its own base");
    }

    [TestMethod]
    public void Fit_TowerSlidesPastABendInItsLane_RecordsTheArcOffsetNotTheStraightLine()
    {
        var tower = new TowerSlot("tower_west_0_0", MatchSide.West, 0, 0, TowerKind.Tower, new Vec3f(95f, 0f, 0f), 0f);
        var layout = new MapLayout("bend",
            new[] { new LaneLayout(0, new[] { new Vec3f(0f, 0f, 0f), new Vec3f(100f, 0f, 0f), new Vec3f(100f, 100f, 0f) }) },
            new[]
            {
                new TowerSlot("keep_west", MatchSide.West, 0, 0, TowerKind.Keep, new Vec3f(0f, 0f, 0f), 0f), tower,
                new TowerSlot("keep_east", MatchSide.East, 0, 0, TowerKind.Keep, new Vec3f(100f, 100f, 0f), 0f),
            },
            new List<CapturePointSlot>(), new List<CampSlot>());

        // Its own spot and the 10 m step toward its base are blocked, so it slides 10 m on past the corner, 7.07 m away in a straight line.
        var fit = FordsLayoutFitter.Fit(layout, Ground, p => p.Distance2D(tower.Position) > 3f && p.Distance2D(new Vec3f(85f, 0f, 0f)) > 3f);

        var slid = fit.Layout.Towers.Single(t => t.Id == tower.Id);
        Assert.AreEqual(100f, slid.Position.X, 0.01f);
        Assert.AreEqual(5f, slid.Position.Y, 0.01f);
        Assert.AreEqual(10f, fit.MovedTowers.Single().Distance, 0.001f);
    }

    [TestMethod]
    public void Fit_MiddleLaneTowerInAHole_IsSlidNotDropped()
    {
        var layout = Generated();
        var tower = layout.Towers.Single(t => t.Id == "tower_west_1_0");

        var fit = FordsLayoutFitter.Fit(layout, Ground, NotIn(tower.Position, 3f));

        var slid = fit.Layout.Towers.Single(t => t.Id == "tower_west_1_0");
        Assert.IsTrue(slid.Position.Distance2D(tower.Position) <= 31f);
        Assert.IsTrue(Math.Abs(slid.Position.Y) < 6f, "still on the middle lane");
        Assert.AreEqual(0, fit.Dropped);
        Assert.AreEqual(0, fit.DroppedTowers.Count);
    }

    [TestMethod]
    public void Fit_MiddleLaneWaypointInAHole_IsMovedNotDropped()
    {
        var layout = Generated();
        var lane = layout.Lanes.Single(l => l.Index == 1);
        var bad = lane.Path[10];

        var fit = FordsLayoutFitter.Fit(layout, Ground, NotIn(bad, 3f));

        var fitted = fit.Layout.Lanes.Single(l => l.Index == 1).Path;
        Assert.AreEqual(0, fit.Dropped);
        Assert.AreEqual(1, fit.Moved);
        Assert.AreEqual(lane.Path.Count, fitted.Count);
        var moved = fitted[10];
        Assert.IsTrue(moved.Distance2D(bad) > 3f && moved.Distance2D(bad) <= 12f);
        Assert.AreEqual(1, fit.Lanes.Single(s => s.Lane == 1).Moved);
    }

    [TestMethod]
    public void Fit_MiddleLaneWaypointInAHole_StepsSidewaysFirst()
    {
        var layout = Generated();
        var bad = layout.Lanes.Single(l => l.Index == 1).Path[10];

        var fit = FordsLayoutFitter.Fit(layout, Ground, NotIn(bad, 3f));

        var moved = fit.Layout.Lanes.Single(l => l.Index == 1).Path[10];
        Assert.AreEqual(bad.X, moved.X, 0.1f, "the lane runs along X, so the nearest free spot is straight across it");
        Assert.AreEqual(4f, Math.Abs(moved.Y), 0.1f);
    }

    [TestMethod]
    public void Fit_SideLanePointNearTheBase_NeverMovesAcrossTheNeighbourLane()
    {
        var layout = Generated();
        var lane = layout.Lanes.Single(l => l.Index == 0);
        var bad = lane.Path[3];
        Assert.IsTrue(bad.Y < -1f && bad.Y > -4f, "a point still close to the middle line (the bow has barely opened)");

        var fit = FordsLayoutFitter.Fit(layout, Ground, NotIn(bad, 3f));

        var moved = fit.Layout.Lanes.Single(l => l.Index == 0).Path[3];
        Assert.AreEqual(0, fit.Dropped);
        Assert.IsTrue(moved.Y < bad.Y, $"the point stayed on its own side of the middle lane, y={moved.Y}");
        Assert.IsTrue(DistanceToPolyline(moved, layout.Lanes.Single(l => l.Index == 1).Path) >= Math.Abs(bad.Y) / 2f);
    }

    [TestMethod]
    public void Fit_AllSpotsBadForALanesTowers_RejectsAndNamesTheTowers()
    {
        var layout = Generated();

        // A band 28 m wide across the middle lane from x 100 to 460 holds both of West's lane 1 towers (x 200 and 360).
        var fit = FordsLayoutFitter.Fit(layout, Ground, p => !(Math.Abs(p.Y) < 14f && p.X > 100f && p.X < 460f));

        Assert.IsTrue(fit.Rejected);
        StringAssert.Contains(fit.Reason, "tower_west_1_0");
        StringAssert.Contains(fit.Reason, "tower_west_1_1");
        CollectionAssert.AreEquivalent(new[] { "tower_west_1_0", "tower_west_1_1" }, fit.DroppedTowers.ToList());
        Assert.IsFalse(fit.Layout.Towers.Any(t => t.Lane == 1 && t.Owner == MatchSide.West));
    }

    [TestMethod]
    public void Fit_OneTowerOfALaneLost_DropsItWithoutRejecting()
    {
        var layout = Generated();

        // The band holds only the outer tower (x 360) and is wider than the 30 m slide either way.
        var fit = FordsLayoutFitter.Fit(layout, Ground, p => !(Math.Abs(p.Y) < 14f && p.X > 320f && p.X < 400f));

        Assert.IsFalse(fit.Rejected);
        Assert.AreEqual("", fit.Reason);
        CollectionAssert.AreEqual(new[] { "tower_west_1_0" }, fit.DroppedTowers.ToList());
        Assert.IsTrue(fit.Layout.Towers.Any(t => t.Id == "tower_west_1_1"));
    }

    [TestMethod]
    public void Fit_NothingWalkable_DropsTowersPointsAndCampsButKeepsKeepsAndLaneEnds()
    {
        var layout = Generated();

        var fit = FordsLayoutFitter.Fit(layout, Ground, _ => false);

        Assert.IsTrue(fit.Layout.Towers.All(t => t.Kind == TowerKind.Keep));
        Assert.AreEqual(2, fit.Layout.Towers.Count);
        Assert.AreEqual(0, fit.Layout.CapturePoints.Count);
        Assert.AreEqual(0, fit.Layout.Camps.Count);
        Assert.IsTrue(fit.Layout.Lanes.All(l => l.Path.Count == 2));
        Assert.IsTrue(fit.Dropped > 0);
        Assert.IsTrue(fit.Unfixed >= 2);
        Assert.IsTrue(fit.Rejected);
    }

    [TestMethod]
    public void Fit_KeepAndLaneEndOnBadSpots_KeepTheirPositionsOnTheGround()
    {
        var layout = Generated();
        var keep = layout.Towers.Single(t => t.Id == "keep_west");

        var fit = FordsLayoutFitter.Fit(layout, Ground, _ => false);

        var fitted = fit.Layout.Towers.Single(t => t.Id == "keep_west");
        Assert.AreEqual(keep.Position.X, fitted.Position.X);
        Assert.AreEqual(7f, fitted.Position.Z);
        Assert.AreEqual(layout.Lanes[0].Path[0].X, fit.Layout.Lanes[0].Path[0].X);
    }

    [TestMethod]
    public void Fit_InteriorPathPointOnABadSpot_IsMovedNotDropped()
    {
        var layout = Generated();
        var lane = layout.Lanes[0];
        var bad = lane.Path[lane.Path.Count / 2];

        var fit = FordsLayoutFitter.Fit(layout, Ground, p => p.Distance2D(bad) > 2f);

        Assert.AreEqual(lane.Path.Count, fit.Layout.Lanes[0].Path.Count);
        Assert.IsTrue(fit.Moved >= 1);
        Assert.AreEqual(0, fit.Dropped);
    }

    [TestMethod]
    public void Fit_HoleWiderThanTheSearch_DropsTheLanePointsAndReportsTheGap()
    {
        var layout = Generated();
        var center = layout.Lanes.Single(l => l.Index == 1).Path[40];

        var fit = FordsLayoutFitter.Fit(layout, Ground, NotIn(center, 30f));

        var stats = fit.Lanes.Single(s => s.Lane == 1);
        var original = layout.Lanes.Single(l => l.Index == 1).Path;
        Assert.AreEqual(original.Count, stats.Waypoints);
        Assert.IsTrue(stats.Dropped >= 3);
        Assert.AreEqual(stats.Waypoints - stats.Dropped, stats.Kept);
        Assert.IsTrue(stats.MaxGap >= 55f, $"max gap {stats.MaxGap}");
        Assert.AreEqual(0, stats.BadSegments);
        Assert.IsTrue(fit.Lanes.Where(s => s.Lane != 1).All(s => s.MaxGap < 14f));
    }

    [TestMethod]
    public void Fit_SegmentCheckFailsOnOneLane_CountsTheBadSegmentOnThatLaneOnly()
    {
        var layout = Generated();

        var fit = FordsLayoutFitter.Fit(layout, Ground, _ => true,
            (a, b) => !(Math.Min(a.X, b.X) < 500f && Math.Max(a.X, b.X) >= 500f && Math.Abs(a.Y) < 10f));

        Assert.AreEqual(1, fit.Lanes.Single(s => s.Lane == 1).BadSegments);
        Assert.AreEqual(0, fit.Lanes.Single(s => s.Lane == 0).BadSegments);
        Assert.AreEqual(0, fit.Lanes.Single(s => s.Lane == 2).BadSegments);
        Assert.IsFalse(fit.Rejected);
    }

    [TestMethod]
    public void Fit_WithoutASegmentCheck_ReportsNoBadSegments()
    {
        var fit = FordsLayoutFitter.Fit(Generated(), Ground, _ => true);

        Assert.AreEqual(3, fit.Lanes.Count);
        Assert.IsTrue(fit.Lanes.All(s => s.BadSegments == 0));
    }
}
