using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionMap.Widgets;

namespace TAOM.Tests.Features.FactionMap;

// Pure "x,y;x,y" point-list parsing/formatting extracted from PolygonWidget (T4 refactor).
// Pinned 1-for-1: invariant culture, malformed entries skipped, F3 formatting on round-trip.
[TestClass]
public class PolygonPointParserTests
{
    [TestMethod]
    public void Parse_TwoValidPairs_ReturnsBothPoints()
    {
        var (xs, ys) = PolygonPointParser.Parse("0.5,0.25;0.75,1.0");
        Assert.AreEqual(2, xs.Length);
        Assert.AreEqual(0.5f, xs[0]);
        Assert.AreEqual(0.25f, ys[0]);
        Assert.AreEqual(0.75f, xs[1]);
        Assert.AreEqual(1.0f, ys[1]);
    }

    [TestMethod]
    public void Parse_EmptyOrWhitespace_ReturnsEmpty()
    {
        Assert.AreEqual(0, PolygonPointParser.Parse("").Xs.Length);
        Assert.AreEqual(0, PolygonPointParser.Parse("   ").Xs.Length);
        Assert.AreEqual(0, PolygonPointParser.Parse(null).Xs.Length);
    }

    [TestMethod]
    public void Parse_MalformedEntry_IsSkipped()
    {
        var (xs, ys) = PolygonPointParser.Parse("0.1,0.2;garbage;0.3,0.4");
        Assert.AreEqual(2, xs.Length);
        Assert.AreEqual(0.3f, xs[1]);
        Assert.AreEqual(0.4f, ys[1]);
    }

    [TestMethod]
    public void Parse_TrimsWhitespaceAroundCoords()
    {
        var (xs, ys) = PolygonPointParser.Parse(" 0.1 , 0.2 ; 0.3 , 0.4 ");
        Assert.AreEqual(2, xs.Length);
        Assert.AreEqual(0.1f, xs[0]);
    }

    [TestMethod]
    public void Format_RoundTripsWithF3Precision()
    {
        var (xs, ys) = PolygonPointParser.Parse("0.500,0.250;0.750,1.000");
        Assert.AreEqual("0.500,0.250;0.750,1.000", PolygonPointParser.Format(xs, ys, xs.Length));
    }

    [TestMethod]
    public void Format_EmptyPoints_ReturnsEmptyString()
        => Assert.AreEqual("", PolygonPointParser.Format(System.Array.Empty<float>(), System.Array.Empty<float>(), 0));
}
