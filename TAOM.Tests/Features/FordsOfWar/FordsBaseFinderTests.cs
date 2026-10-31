using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
public class FordsBaseFinderTests
{
    private static List<Vec3f> Rectangle(float width, float height) => new()
    {
        new Vec3f(0f, 0f, 0f), new Vec3f(width, 0f, 0f), new Vec3f(width, height, 0f), new Vec3f(0f, height, 0f),
    };

    private static List<Vec3f> Rotate(IEnumerable<Vec3f> points, double degrees, Vec3f around)
    {
        var a = degrees * Math.PI / 180d;
        var cos = (float)Math.Cos(a);
        var sin = (float)Math.Sin(a);
        return points.Select(p =>
        {
            var x = p.X - around.X;
            var y = p.Y - around.Y;
            return new Vec3f(around.X + x * cos - y * sin, around.Y + x * sin + y * cos, 0f);
        }).ToList();
    }

    [TestMethod]
    public void TryFind_WideRectangle_PutsTheBasesOnTheLongAxisInsetByTenPercent()
    {
        Assert.IsTrue(FordsBaseFinder.TryFind(Rectangle(500f, 250f), 0.1f, out var west, out var east));

        Assert.AreEqual(50f, west.X, 1f);
        Assert.AreEqual(125f, west.Y, 1f);
        Assert.AreEqual(450f, east.X, 1f);
        Assert.AreEqual(125f, east.Y, 1f);
    }

    [TestMethod]
    public void TryFind_TallRectangle_UsesTheYAxisAndKeepsWestOnTheLowerCoordinate()
    {
        Assert.IsTrue(FordsBaseFinder.TryFind(Rectangle(200f, 600f), 0.1f, out var west, out var east));

        Assert.AreEqual(100f, west.X, 1f);
        Assert.AreEqual(100f, east.X, 1f);
        Assert.AreEqual(60f, west.Y, 1f);
        Assert.AreEqual(540f, east.Y, 1f);
    }

    [TestMethod]
    public void TryFind_RotatedRectangle_FollowsTheRotatedLongAxis()
    {
        var corners = Rotate(Rectangle(500f, 250f), 30d, new Vec3f(250f, 125f, 0f));

        Assert.IsTrue(FordsBaseFinder.TryFind(corners, 0.1f, out var west, out var east));

        Assert.AreEqual(400f, west.Distance2D(east), 4f);
        var angle = Math.Atan2(east.Y - west.Y, east.X - west.X) * 180d / Math.PI;
        Assert.AreEqual(30d, angle, 2d);
        Assert.AreEqual(250f, (west.X + east.X) / 2f, 2f);
        Assert.AreEqual(125f, (west.Y + east.Y) / 2f, 2f);
    }

    [TestMethod]
    public void TryFind_FewerThanThreePoints_ReturnsFalse()
    {
        Assert.IsFalse(FordsBaseFinder.TryFind(new List<Vec3f> { new(0f, 0f, 0f), new(10f, 0f, 0f) }, 0.1f, out _, out _));
        Assert.IsFalse(FordsBaseFinder.TryFind(new List<Vec3f>(), 0.1f, out _, out _));
    }

    [TestMethod]
    public void TryFind_AllPointsTheSame_ReturnsFalse()
    {
        var degenerate = new List<Vec3f> { new(5f, 5f, 0f), new(5f, 5f, 0f), new(5f, 5f, 0f) };

        Assert.IsFalse(FordsBaseFinder.TryFind(degenerate, 0.1f, out _, out _));
    }
}
