using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Tests.Features.RealmBorders;

/// <summary>
/// Pins how live ownership turns the fixed province chains into realm borders: a chain is drawn only
/// between two different realms, chains that continue the same border merge into one line through
/// the junctions where a third province of either realm meets them, and a capture re-selects.
/// </summary>
[TestClass]
public class RealmBorderSelectorTests
{
    private static RealmBorderLine[] Select(ProvinceMap map, params string?[] realmOfProvince) =>
        RealmBorderSelector.Select(BoundaryTracer.Trace(map), realmOfProvince).ToArray();

    [TestMethod]
    public void Select_ProvincesOfOneRealm_DrawNoLine()
    {
        var map = BoundaryTracerTests.Map("001122", "001122");

        var lines = Select(map, "gondor", "gondor", "mordor");

        Assert.AreEqual(1, lines.Length, "only the gondor|mordor edge is a realm border");
        Assert.AreEqual("gondor", lines[0].LeftRealm);
        Assert.AreEqual("mordor", lines[0].RightRealm);
    }

    [TestMethod]
    public void Select_BorderPassingAJunctionWithinOneRealm_MergesIntoOneLine()
    {
        // provinces 0 (south-west) and 1 (north-west) are both Gondor; 2 (east) is Mordor
        var map = BoundaryTracerTests.Map("111222", "111222", "000222", "000222");

        var line = Select(map, "gondor", "gondor", "mordor").Single();

        Assert.IsFalse(line.IsClosed);
        CollectionAssert.AreEqual(
            Enumerable.Range(0, 5).Select(r => new LatticePoint(3, r)).ToArray(),
            line.Points.ToArray());
    }

    [TestMethod]
    public void Select_AfterACapture_TheBorderMovesWithTheFief()
    {
        var map = BoundaryTracerTests.Map("111222", "111222", "000222", "000222");

        var line = Select(map, "gondor", "mordor", "mordor").Single();

        Assert.AreEqual(new LatticePoint(3, 0), line.Points.First());
        Assert.AreEqual(new LatticePoint(0, 2), line.Points.Last(), "the border now wraps under the captured north-west fief");
        Assert.AreEqual(6, line.Points.Count);
    }

    [TestMethod]
    public void Select_RealmNamesOutOfOrder_OrientsTheLineSoTheLowerNameIsLeft()
    {
        var map = BoundaryTracerTests.Map("0011", "0011", "0011");

        var line = Select(map, "rohan", "gondor").Single();

        Assert.AreEqual("gondor", line.LeftRealm);
        Assert.AreEqual("rohan", line.RightRealm);
        Assert.AreEqual(new LatticePoint(2, 3), line.Points.First(), "gondor lies east, so the line runs south");
        Assert.AreEqual(new LatticePoint(2, 0), line.Points.Last());
    }

    [TestMethod]
    public void Select_ProvinceWithoutARealm_DrawsNoLine()
    {
        var map = BoundaryTracerTests.Map("0011", "0011");

        Assert.AreEqual(0, Select(map, "gondor", null).Length);
    }

    [TestMethod]
    public void Select_FiefHeldInsideAnotherRealm_IsRinged()
    {
        var map = BoundaryTracerTests.Map("000000", "001100", "001100", "000000");

        var line = Select(map, "rohan", "isengard").Single();

        Assert.IsTrue(line.IsClosed);
        Assert.AreEqual("isengard", line.LeftRealm);
    }

    [TestMethod]
    public void Select_ProvinceIndexWithNoRealmEntry_IsTreatedAsUnowned()
    {
        var map = BoundaryTracerTests.Map("0011", "0011");

        Assert.AreEqual(0, Select(map, "gondor").Length, "province 1 has no entry in the realm list");
    }
}
