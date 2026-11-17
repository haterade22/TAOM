using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.NavalTravel;

namespace TAOM.Tests.Features.NavalTravel;

[TestClass]
public class NavalLandFallbackTests
{
    private static readonly List<(float X, float Y)> Anchors = new() { (0f, 0f), (10f, 0f), (5f, 5f) };

    [TestMethod]
    public void Nearest_PicksClosestAnchor() =>
        Assert.AreEqual(1, NavalLandFallback.NearestIndex(9f, 1f, Anchors));

    [TestMethod]
    public void Nearest_TieKeepsLowerIndex() =>
        Assert.AreEqual(0, NavalLandFallback.NearestIndex(5f, 0f, new List<(float, float)> { (0f, 0f), (10f, 0f) }));

    [TestMethod]
    public void Nearest_NoAnchors_ReturnsMinusOne() =>
        Assert.AreEqual(-1, NavalLandFallback.NearestIndex(1f, 1f, new List<(float, float)>()));

    [TestMethod]
    public void Nearest_NullAnchors_ReturnsMinusOne() =>
        Assert.AreEqual(-1, NavalLandFallback.NearestIndex(1f, 1f, null!));

    [TestMethod]
    public void Nearest_NonFiniteQuery_ReturnsMinusOne()
    {
        Assert.AreEqual(-1, NavalLandFallback.NearestIndex(float.NaN, 1f, Anchors));
        Assert.AreEqual(-1, NavalLandFallback.NearestIndex(float.PositiveInfinity, 1f, Anchors));
    }

    [TestMethod]
    public void Nearest_SkipsNonFiniteAnchor() =>
        Assert.AreEqual(1, NavalLandFallback.NearestIndex(0f, 0f, new List<(float, float)> { (float.NaN, 0f), (3f, 3f) }));
}
