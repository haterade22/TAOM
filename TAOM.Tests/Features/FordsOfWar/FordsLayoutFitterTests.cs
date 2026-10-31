using System;
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

    [TestMethod]
    public void Fit_EverythingWalkable_SnapsHeightsAndChangesNothingElse()
    {
        var layout = Generated();

        var fit = FordsLayoutFitter.Fit(layout, Ground, _ => true);

        Assert.AreEqual(0, fit.Moved);
        Assert.AreEqual(0, fit.Dropped);
        Assert.AreEqual(0, fit.Unfixed);
        Assert.AreEqual(layout.Towers.Count, fit.Layout.Towers.Count);
        Assert.IsTrue(fit.Layout.Towers.All(t => t.Position.Z == 7f));
        Assert.IsTrue(fit.Layout.Lanes.SelectMany(l => l.Path).All(p => p.Z == 7f));
        Assert.AreEqual(layout.Towers[0].Position.X, fit.Layout.Towers[0].Position.X);
    }

    [TestMethod]
    public void Fit_TowerOnABadSpot_MovesTowardTheCenterLineAndIsCounted()
    {
        var layout = Generated();
        var sideTower = layout.Towers.First(t => t.Kind == TowerKind.Tower && Math.Abs(t.Position.Y) > 30f);

        var fit = FordsLayoutFitter.Fit(layout, Ground, p => p.Distance2D(sideTower.Position) > 3f);

        var moved = fit.Layout.Towers.Single(t => t.Id == sideTower.Id);
        Assert.IsTrue(Math.Abs(moved.Position.Y) < Math.Abs(sideTower.Position.Y));
        Assert.AreEqual(sideTower.Yaw, moved.Yaw);
        Assert.IsTrue(fit.Moved >= 1);
        Assert.AreEqual(0, fit.Dropped);
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
}
