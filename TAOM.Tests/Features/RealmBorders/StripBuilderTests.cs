using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Tests.Features.RealmBorders;

/// <summary>
/// Pins the band geometry every border look is drawn with: a strip between two signed offsets from
/// the line (positive is left of travel), mitred at corners with a cap, and a texture coordinate that
/// grows with distance along the line so a dash or a cord never stretches.
/// </summary>
[TestClass]
public class StripBuilderTests
{
    private const float Tolerance = 1e-3f;

    private static MapPoint[] Points(params (float X, float Y)[] xy) => xy.Select(p => new MapPoint(p.X, p.Y)).ToArray();

    private static void AssertNear(MapPoint expected, MapPoint actual, string message = "")
    {
        Assert.AreEqual(expected.X, actual.X, Tolerance, message);
        Assert.AreEqual(expected.Y, actual.Y, Tolerance, message);
    }

    [TestMethod]
    public void Build_StraightLine_PutsPositiveOffsetsOnTheLeft()
    {
        var strip = StripBuilder.Build(Points((0, 0), (10, 0)), closed: false, nearOffset: 1f, farOffset: 3f);

        AssertNear(new MapPoint(0, 1), strip.Near[0]);
        AssertNear(new MapPoint(10, 1), strip.Near[1]);
        AssertNear(new MapPoint(10, 3), strip.Far[1]);
        Assert.AreEqual(10f, strip.V[1], Tolerance, "V is the distance along the line");
    }

    [TestMethod]
    public void Build_NegativeOffsets_GoToTheRight()
    {
        var strip = StripBuilder.Build(Points((0, 0), (10, 0)), closed: false, nearOffset: -1f, farOffset: -3f);

        AssertNear(new MapPoint(0, -1), strip.Near[0]);
        AssertNear(new MapPoint(10, -3), strip.Far[1]);
    }

    [TestMethod]
    public void Build_LeftTurn_MitresTheInsideCorner()
    {
        var strip = StripBuilder.Build(Points((0, 0), (10, 0), (10, 10)), closed: false, nearOffset: 1f, farOffset: 2f);

        AssertNear(new MapPoint(9, 1), strip.Near[1], "the offset lines of both legs meet one unit inside the corner");
        Assert.AreEqual(20f, strip.V[2], Tolerance);
    }

    [TestMethod]
    public void Build_HairpinTurn_CapsTheMitre()
    {
        var line = Points((0, 0), (10, 0), (0, 0.5f));

        var strip = StripBuilder.Build(line, closed: false, nearOffset: 1f, farOffset: 2f, miterLimit: 2.5f);

        Assert.IsTrue((strip.Near[1] - line[1]).Length <= 2.5f + Tolerance, "a near-reversal must not throw the corner off to infinity");
    }

    [TestMethod]
    public void Build_ClosedRing_RepeatsItsFirstVertexWithTheFullLength()
    {
        var square = Points((0, 0), (10, 0), (10, 10), (0, 10));

        var strip = StripBuilder.Build(square, closed: true, nearOffset: 1f, farOffset: 2f);

        Assert.AreEqual(5, strip.Near.Count);
        AssertNear(strip.Near[0], strip.Near[4]);
        Assert.AreEqual(40f, strip.V[4], Tolerance, "forty units round");
        Assert.IsTrue(strip.IsClosed);
    }

    [TestMethod]
    public void Build_RepeatedPoint_StaysFinite()
    {
        var strip = StripBuilder.Build(Points((0, 0), (5, 0), (5, 0), (10, 0)), closed: false, nearOffset: 1f, farOffset: 2f);

        Assert.IsTrue(strip.Near.Concat(strip.Far).All(p => !float.IsNaN(p.X) && !float.IsNaN(p.Y)));
        Assert.AreEqual(3, strip.Near.Count, "the duplicate is dropped");
    }

    [TestMethod]
    public void Build_SinglePoint_IsEmpty()
    {
        var strip = StripBuilder.Build(Points((3, 3)), closed: false, nearOffset: 1f, farOffset: 2f);

        Assert.AreEqual(0, strip.Near.Count);
    }

}
