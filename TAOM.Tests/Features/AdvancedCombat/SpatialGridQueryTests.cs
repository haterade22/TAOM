using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Library;
using TAOM.Features.AdvancedCombat;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// Pins SpatialGrid's radius query against a brute-force sphere scan. The Agent-typed API reads
/// native positions, so these tests drive the generic BuildCells / CollectInRadius helpers it
/// delegates to, on plain points (plan 015).
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class SpatialGridQueryTests
{
    private const float CellSize = 20f;

    private sealed class Point
    {
        public Point(float x, float y, float z) => P = new Vec3(x, y, z);
        public Vec3 P { get; set; }
        public override string ToString() => $"({P.x}, {P.y}, {P.z})";
    }

    private static readonly Func<Point, Vec3> PositionOf = p => p.P;
    private static readonly Func<Point, bool> Everyone = _ => true;

    private static List<Point> Query(List<Point> points, Vec3 center, float radius)
    {
        var cells = SpatialGrid.BuildCells(points, Everyone, PositionOf, CellSize);
        var buffer = new List<Point>();
        SpatialGrid.CollectInRadius(cells, center, radius, CellSize, PositionOf, buffer);
        return buffer;
    }

    private static List<Point> BruteForce(List<Point> points, Vec3 center, float radius)
    {
        float radiusSquared = radius * radius;
        return points.Where(p =>
        {
            float dx = p.P.x - center.x;
            float dy = p.P.y - center.y;
            float dz = p.P.z - center.z;
            return dx * dx + dy * dy + dz * dz <= radiusSquared;
        }).ToList();
    }

    [TestMethod]
    public void CollectInRadius_RandomPointsAndCentres_MatchesABruteForceSphereScan()
    {
        var rng = new Random(15);
        float Next(float min, float max) => (float)(min + rng.NextDouble() * (max - min));
        var points = new List<Point>();
        for (int i = 0; i < 600; i++)
            points.Add(new Point(Next(-150f, 150f), Next(-150f, 150f), Next(-45f, 45f)));

        foreach (float radius in new[] { 0.5f, 10f, 20f, 60f, 75f })
        {
            for (int c = 0; c < 25; c++)
            {
                var center = new Vec3(Next(-120f, 120f), Next(-120f, 120f), Next(-30f, 30f));
                CollectionAssert.AreEquivalent(BruteForce(points, center, radius), Query(points, center, radius),
                    $"radius {radius}, center ({center.x}, {center.y}, {center.z})");
            }
        }
    }

    [TestMethod]
    public void CollectInRadius_PointExactlyOnTheSphere_IsIncluded()
    {
        var onTheSphere = new Point(3f, 4f, 0f); // 3-4-5 triangle: distance squared is exactly 25
        var result = Query(new List<Point> { onTheSphere }, new Vec3(0f, 0f, 0f), 5f);
        CollectionAssert.AreEqual(new List<Point> { onTheSphere }, result);
    }

    [TestMethod]
    public void CollectInRadius_SameColumnDifferentHeights_KeepsOnlyThoseInsideTheSphere()
    {
        var ground = new Point(1f, 1f, 0f);
        var below = new Point(1f, 1f, -9f);    // 83 square metres: inside a 10 m sphere
        var wallTop = new Point(1f, 1f, 45f);  // same (x, y) column, 45 m up: outside
        var result = Query(new List<Point> { ground, below, wallTop }, new Vec3(0f, 0f, 0f), 10f);
        CollectionAssert.AreEquivalent(new List<Point> { ground, below }, result);
    }

    [TestMethod]
    public void BuildCells_ExcludedItems_AreNeverReturned()
    {
        var kept = new Point(1f, 1f, 0f);
        var excluded = new Point(2f, 2f, 0f);
        var cells = SpatialGrid.BuildCells(new List<Point> { kept, excluded }, p => p != excluded, PositionOf, CellSize);
        var buffer = new List<Point>();
        SpatialGrid.CollectInRadius(cells, new Vec3(0f, 0f, 0f), 10f, CellSize, PositionOf, buffer);
        CollectionAssert.AreEqual(new List<Point> { kept }, buffer);
    }

    [TestMethod]
    public void CollectInRadius_BufferHoldsAnEarlierResult_ClearsItBeforeFilling()
    {
        var stale = new Point(500f, 500f, 0f);
        var near = new Point(1f, 0f, 0f);
        var cells = SpatialGrid.BuildCells(new List<Point> { near }, Everyone, PositionOf, CellSize);
        var buffer = new List<Point> { stale };
        SpatialGrid.CollectInRadius(cells, new Vec3(0f, 0f, 0f), 10f, CellSize, PositionOf, buffer);
        CollectionAssert.AreEqual(new List<Point> { near }, buffer);
    }

    [TestMethod]
    public void CollectInRadius_SixtyMetreQuery_ProbesSevenBySevenColumns()
    {
        var cells = SpatialGrid.BuildCells(new List<Point>(), Everyone, PositionOf, CellSize);
        int probes = SpatialGrid.CollectInRadius(cells, new Vec3(5f, 5f, 5f), 60f, CellSize, PositionOf, new List<Point>());
        Assert.AreEqual(49, probes, "the warg's 60 m scan should look up 7 x 7 columns, not 7 x 7 x 7 cells");
    }

    [TestMethod]
    public void CollectInRadius_TenMetreQuery_ProbesTwoByTwoColumns()
    {
        var cells = SpatialGrid.BuildCells(new List<Point>(), Everyone, PositionOf, CellSize);
        int probes = SpatialGrid.CollectInRadius(cells, new Vec3(5f, 5f, 5f), 10f, CellSize, PositionOf, new List<Point>());
        Assert.AreEqual(4, probes);
    }

    [TestMethod]
    public void CollectInRadius_OneColumnAtTwoHeights_ReturnsThemInBuildOrder()
    {
        // Order inside a column is the rebuild's order; it decides which of two in-reach targets a
        // stop-on-first-hit bite lands on, and which of two equidistant spiders' prey is engaged.
        // The z-keyed grid returned the lower height band first.
        var high = new Point(1f, 1f, 25f);
        var low = new Point(1f, 1f, 5f);
        var result = Query(new List<Point> { high, low }, new Vec3(0f, 0f, 15f), 30f);
        CollectionAssert.AreEqual(new List<Point> { high, low }, result);
    }

    [TestMethod]
    public void CollectInRadius_PointMovedVerticallySinceTheBuild_IsJudgedOnItsCurrentPosition()
    {
        // The grid is rebuilt every 2 s and queried with live positions. A point bucketed at z 21 that
        // has since dropped to z 18 is inside a 10 m sphere around z 9. The z-keyed grid never probed
        // its old z cell and missed it; the (x, y) column finds it (Codex review of plan 015, P2).
        var moving = new Point(1f, 0f, 21f);
        var cells = SpatialGrid.BuildCells(new List<Point> { moving }, Everyone, PositionOf, CellSize);
        moving.P = new Vec3(1f, 0f, 18f);
        var buffer = new List<Point>();
        SpatialGrid.CollectInRadius(cells, new Vec3(0f, 0f, 9f), 10f, CellSize, PositionOf, buffer);
        CollectionAssert.AreEqual(new List<Point> { moving }, buffer);
    }
}
