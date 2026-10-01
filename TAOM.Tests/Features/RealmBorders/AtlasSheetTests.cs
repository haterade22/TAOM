using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Tests.Features.RealmBorders;

/// <summary>
/// The parchment sheet laid over the whole map at full zoom-out: its grid, where the picture lands on
/// it, and how it fades in with the camera.
/// </summary>
[TestClass]
public class AtlasSheetTests
{
    private static List<BorderVertex> Corners(IEnumerable<IReadOnlyList<BorderQuad>> tiles) =>
        tiles.SelectMany(t => t).SelectMany(q => new[] { q.NearStart, q.FarStart, q.FarEnd, q.NearEnd }).ToList();

    [TestMethod]
    public void Tiles_CoverTheSquareInEvenCells()
    {
        var tiles = AtlasSheet.Tiles(1600f, tilesPerSide: 4, cellsPerTile: 2, White);

        Assert.AreEqual(16, tiles.Count);
        Assert.IsTrue(tiles.All(t => t.Count == 4), "each tile holds its 2 x 2 cells");
        var xs = Corners(tiles).Select(v => v.Position.X).Distinct().OrderBy(x => x).ToList();
        CollectionAssert.AreEqual(Enumerable.Range(0, 9).Select(i => i * 200f).ToList(), xs);
        var ys = Corners(tiles).Select(v => v.Position.Y).Distinct().OrderBy(y => y).ToList();
        CollectionAssert.AreEqual(Enumerable.Range(0, 9).Select(i => i * 200f).ToList(), ys);
    }

    private const uint White = 0xFFFFFFFFu;

    [TestMethod]
    public void Tiles_TextureRowsCountFromTheBottom_SoThePicturesTopRowIsTheNorthEdge()
    {
        var corners = Corners(AtlasSheet.Tiles(1600f, 2, 2, White));

        var southWest = corners.First(v => v.Position.X == 0f && v.Position.Y == 0f);
        var northEast = corners.First(v => v.Position.X == 1600f && v.Position.Y == 1600f);
        var inside = corners.First(v => v.Position.X == 800f && v.Position.Y == 400f);
        Assert.AreEqual((0f, 0f), (southWest.U, southWest.V), "v 0 is the picture's bottom row, the south edge");
        Assert.AreEqual((1f, 1f), (northEast.U, northEast.V));
        Assert.AreEqual((0.5f, 0.25f), (inside.U, inside.V));
    }

    [TestMethod]
    public void Tiles_EveryCornerTakesTheTint()
    {
        Assert.IsTrue(Corners(AtlasSheet.Tiles(1600f, 2, 2, 0xFFFFF0D8u)).All(v => v.Colour == 0xFFFFF0D8u));
    }

    [DataTestMethod]
    [DataRow(100f, 0f)]      // well inside the zoom range
    [DataRow(400f, 0f)]      // exactly at the start, 80% of 500
    [DataRow(437.5f, 0.5f)]  // halfway through the band
    [DataRow(475f, 1f)]      // at the full point, 95%
    [DataRow(500f, 1f)]      // all the way out
    public void Alpha_FadesInOverTheLastStretchOfTheZoom(float distance, float expected)
    {
        Assert.AreEqual(expected, AtlasSheet.Alpha(distance, 500f, 0.8f, 0.95f), 1e-4f);
    }

    [DataTestMethod]
    [DataRow(float.NaN, 500f)]
    [DataRow(float.PositiveInfinity, 500f)]
    [DataRow(-5f, 500f)]
    [DataRow(480f, float.NaN)]
    [DataRow(480f, 0f)]
    [DataRow(480f, -10f)]
    [DataRow(480f, float.PositiveInfinity)]
    public void Alpha_NotAUsableDistance_ShowsNothing(float distance, float max)
    {
        Assert.AreEqual(0f, AtlasSheet.Alpha(distance, max, 0.8f, 0.95f));
    }

    [DataTestMethod]
    [DataRow(0.95f, 0.8f)]
    [DataRow(0.9f, 0.9f)]
    [DataRow(float.NaN, 0.95f)]
    [DataRow(0.8f, float.NaN)]
    public void Alpha_NotAUsableBand_ShowsNothing(float start, float full)
    {
        Assert.AreEqual(0f, AtlasSheet.Alpha(500f, 500f, start, full));
    }
}
