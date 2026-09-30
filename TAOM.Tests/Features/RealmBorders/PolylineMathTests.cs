using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Tests.Features.RealmBorders;

/// <summary>
/// Pins how grid stair-steps become drawn curves: simplify first, then corner-cut. Open lines keep
/// their endpoints so borders still meet at three-realm corners; closed rings stay closed.
/// </summary>
[TestClass]
public class PolylineMathTests
{
    private static MapPoint[] Points(params (float X, float Y)[] xy) => xy.Select(p => new MapPoint(p.X, p.Y)).ToArray();

    [TestMethod]
    public void Simplify_CollinearPoints_KeepsOnlyTheEnds()
    {
        var simplified = PolylineMath.Simplify(Points((0, 0), (1, 0), (2, 0), (3, 0)), 0.1f);

        CollectionAssert.AreEqual(Points((0, 0), (3, 0)), simplified.ToArray());
    }

    [TestMethod]
    public void Simplify_CornerBeyondTolerance_IsKept()
    {
        var corner = Points((0, 0), (2, 0), (2, 2));

        CollectionAssert.AreEqual(corner, PolylineMath.Simplify(corner, 0.5f).ToArray());
    }

    [TestMethod]
    public void Simplify_StairSteps_BecomeADiagonal()
    {
        var stairs = Enumerable.Range(0, 9).Select(i => new MapPoint(i / 2 + i % 2, i / 2)).ToArray();

        Assert.AreEqual(2, PolylineMath.Simplify(stairs, 0.9f).Count);
    }

    [TestMethod]
    public void Chaikin_OpenLine_KeepsItsEndpoints()
    {
        var line = Points((0, 0), (1, 1), (2, 0));

        var smooth = PolylineMath.Chaikin(line, 3, closed: false);

        Assert.AreEqual(line[0], smooth.First());
        Assert.AreEqual(line[2], smooth.Last());
    }

    [TestMethod]
    public void Chaikin_ClosedRing_DoublesItsCornersEachPass()
    {
        var square = Points((0, 0), (1, 0), (1, 1), (0, 1));

        Assert.AreEqual(16, PolylineMath.Chaikin(square, 2, closed: true).Count);
    }

    [TestMethod]
    public void Smooth_ClosedRing_StaysAClosedRingWithoutARepeatedPoint()
    {
        var ring = Points((2, 2), (2, 3), (2, 4), (3, 4), (4, 4), (4, 3), (4, 2), (3, 2));

        var smooth = PolylineMath.Smooth(ring, closed: true, tolerance: 0.9f, iterations: 3);

        Assert.IsTrue(smooth.Count > ring.Length);
        Assert.AreNotEqual(smooth.First(), smooth.Last());
    }

    [TestMethod]
    public void Smooth_OpenLine_KeepsItsJunctionEndpoints()
    {
        var line = Points((0, 0), (0, 1), (1, 1), (1, 2), (2, 2), (2, 3));

        var smooth = PolylineMath.Smooth(line, closed: false, tolerance: 0.9f, iterations: 4);

        Assert.AreEqual(line.First(), smooth.First());
        Assert.AreEqual(line.Last(), smooth.Last());
    }
}
