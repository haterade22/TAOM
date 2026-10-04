using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Library;
using TAOM.Core.Collections;
using TAOM.Features.AdvancedCombat;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// The grid rebuild reuses its two maps and its cell lists, and a removal goes to the one cell an agent-to-cell
/// index names instead of walking every cell (plan 033). These pin the reused build against the cells the positions
/// name (worked out here, never by the helper under test) and the indexed removal against the old walk, on plain
/// points through the generic helpers. The instance's own use of them is <c>SpatialGridWiringTests</c>.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class SpatialGridReuseTests
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

    private static float Between(Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

    private static List<Point> Layout(Random random, int count)
    {
        var points = new List<Point>(count);
        for (int i = 0; i < count; i++)
            points.Add(new Point(Between(random, -150f, 150f), Between(random, -150f, 150f), Between(random, -5f, 5f)));
        return points;
    }

    // The bucketing rule written out on its own: a point's cell is its x and y over the cell size, floored (so a
    // negative coordinate rounds down), and a cell lists its points in input order.
    private static Dictionary<(int, int), List<Point>> ExpectedCells(IEnumerable<Point> points, Func<Point, bool> include) =>
        points.Where(include)
            .GroupBy(p => ((int)Math.Floor(p.P.x / CellSize), (int)Math.Floor(p.P.y / CellSize)))
            .ToDictionary(group => group.Key, group => group.ToList());

    private static void AssertSameCells(Dictionary<(int, int), List<Point>> expected, Dictionary<(int, int), List<Point>> actual)
    {
        CollectionAssert.AreEquivalent(expected.Keys.ToList(), actual.Keys.ToList(), "the two builds hold different cells");
        foreach (var pair in expected)
            CollectionAssert.AreEqual(pair.Value, actual[pair.Key], $"cell {pair.Key} differs");
    }

    private static void AssertIndexMatchesCells(Dictionary<(int, int), List<Point>> cells, Dictionary<Point, (int, int)> cellOf)
    {
        Assert.AreEqual(cells.Values.Sum(list => list.Count), cellOf.Count, "every bucketed item has exactly one index entry");
        foreach (var pair in cells)
            foreach (Point point in pair.Value)
                Assert.AreEqual(pair.Key, cellOf[point], $"the index names the wrong cell for {point}");
    }

    [TestMethod]
    public void BuildCellsInto_ReusedContainers_MatchTheCellsThePositionsName()
    {
        var random = new Random(33);
        List<Point> points = Layout(random, 400);
        var cells = new Dictionary<(int, int), List<Point>>();
        var cellOf = new Dictionary<Point, (int, int)>();
        var spare = new Stack<List<Point>>();
        SpatialGrid.BuildCellsInto(cells, cellOf, spare, points, Everyone, PositionOf, CellSize);
        foreach (Point point in points)
            point.P = new Vec3(point.P.x + Between(random, -30f, 30f), point.P.y + Between(random, -30f, 30f), point.P.z);

        SpatialGrid.BuildCellsInto(cells, cellOf, spare, points, Everyone, PositionOf, CellSize);

        AssertSameCells(ExpectedCells(points, Everyone), cells);
        AssertIndexMatchesCells(cells, cellOf);
    }

    [TestMethod]
    public void BuildCellsInto_SecondBuild_TakesItsListsFromTheSpareStack()
    {
        List<Point> points = Layout(new Random(34), 200);
        var cells = new Dictionary<(int, int), List<Point>>();
        var cellOf = new Dictionary<Point, (int, int)>();
        var spare = new Stack<List<Point>>();
        SpatialGrid.BuildCellsInto(cells, cellOf, spare, points, Everyone, PositionOf, CellSize);
        List<List<Point>> firstLists = cells.Values.ToList();

        SpatialGrid.BuildCellsInto(cells, cellOf, spare, points, Everyone, PositionOf, CellSize);

        foreach (List<Point> list in cells.Values)
            Assert.IsTrue(firstLists.Any(first => ReferenceEquals(first, list)), "a second build allocated a cell list");
        Assert.AreEqual(cells.Count, cells.Values.Distinct<List<Point>>(ReferenceIdentity.Instance).Count(),
            "one list sits in two cells");
        Assert.AreEqual(0, spare.Count, "every list the first build used went back into a cell");
    }

    [TestMethod]
    public void RemoveFromCells_MatchesTheOldWalkOverEveryCell()
    {
        var random = new Random(35);
        List<Point> points = Layout(random, 300);
        Dictionary<(int, int), List<Point>> oracle = ExpectedCells(points, Everyone);
        var cells = new Dictionary<(int, int), List<Point>>();
        var cellOf = new Dictionary<Point, (int, int)>();
        SpatialGrid.BuildCellsInto(cells, cellOf, new Stack<List<Point>>(), points, Everyone, PositionOf, CellSize);
        List<Point> removals = points.OrderBy(_ => random.Next()).Take(100).ToList();
        List<Point> neverAdded = Layout(random, 10);
        var sequence = new List<(Point point, bool expected)>();
        sequence.AddRange(removals.Select(p => (p, true)));
        sequence.AddRange(neverAdded.Select(p => (p, false)));
        sequence.Add((removals[0], false));

        foreach (var (point, expected) in sequence)
        {
            foreach (List<Point> cell in oracle.Values)
                if (cell.Remove(point)) break;
            bool removed = SpatialGrid.RemoveFromCells(cells, cellOf, point);
            Assert.AreEqual(expected, removed, $"RemoveFromCells answered wrongly for {point}");
        }

        AssertSameCells(oracle, cells);
        AssertIndexMatchesCells(cells, cellOf);
    }

    [TestMethod]
    public void BuildCellsInto_ExcludedItems_HaveNoCellEntry()
    {
        List<Point> points = Layout(new Random(36), 60);
        var excluded = new HashSet<Point>(points.Where((_, i) => i % 3 == 0));
        var cells = new Dictionary<(int, int), List<Point>>();
        var cellOf = new Dictionary<Point, (int, int)>();

        SpatialGrid.BuildCellsInto(cells, cellOf, new Stack<List<Point>>(), points, p => !excluded.Contains(p), PositionOf, CellSize);

        foreach (Point point in excluded)
        {
            Assert.IsFalse(cellOf.ContainsKey(point), $"excluded {point} has an index entry");
            Assert.IsFalse(cells.Values.Any(list => list.Contains(point)), $"excluded {point} sits in a cell");
        }
        Assert.AreEqual(points.Count - excluded.Count, cellOf.Count);
    }

    // SpatialGrid.Rebuild builds into the spare map and swaps it with the published one, both maps drawing on one
    // stack of spare lists (#592, #595): a build must never touch the published map or hand it a list it still holds.
    // The swap below is this test's own, on plain points; the production Rebuild runs in SpatialGridWiringTests.
    // Each round also checks the index of the pair it filled. The points are new every round, so a build into a pair
    // that held round r - 2 leaves that round's entries in the index unless the build clears it first.
    [TestMethod]
    public void BuildCellsInto_TwoMapsOverOneSpareStack_NeverTouchesOrSharesWithThePublishedMap()
    {
        var random = new Random(41);
        var published = new Dictionary<(int, int), List<Point>>();
        var publishedIndex = new Dictionary<Point, (int, int)>();
        var spareMap = new Dictionary<(int, int), List<Point>>();
        var spareIndex = new Dictionary<Point, (int, int)>();
        var spare = new Stack<List<Point>>();

        for (int round = 0; round < 6; round++)
        {
            List<Point> points = Layout(random, 200);
            Dictionary<(int, int), List<Point>> before = published.ToDictionary(cell => cell.Key, cell => cell.Value.ToList());

            SpatialGrid.BuildCellsInto(spareMap, spareIndex, spare, points, Everyone, PositionOf, CellSize);

            Assert.AreEqual(before.Count, published.Count, $"round {round}: the build changed the published map's cells");
            foreach (KeyValuePair<(int, int), List<Point>> cell in before)
                CollectionAssert.AreEqual(cell.Value, published[cell.Key], $"round {round}: the build changed published cell {cell.Key}");
            var publishedLists = new HashSet<object>(published.Values, ReferenceIdentity.Instance);
            Assert.IsFalse(spareMap.Values.Any(list => publishedLists.Contains(list)),
                $"round {round}: the build put a list the published map still holds into the new map");
            AssertSameCells(ExpectedCells(points, Everyone), spareMap);
            AssertIndexMatchesCells(spareMap, spareIndex);

            (published, spareMap) = (spareMap, published);
            (publishedIndex, spareIndex) = (spareIndex, publishedIndex);
        }
    }
}
