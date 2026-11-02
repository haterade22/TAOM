using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

[TestClass]
public class LaneFaceBarsTests
{
    [TestMethod]
    public void BarsFor_EachLane_BarsTheOtherLanesIds()
    {
        var bars = new LaneFaceBars(new[] { 1, 2, 3 });

        CollectionAssert.AreEqual(new[] { 2, 3 }, bars.BarsFor(0).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 3 }, bars.BarsFor(1).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2 }, bars.BarsFor(2).ToArray());
    }

    [TestMethod]
    public void BarsFor_UnknownLane_BarsNothing()
    {
        Assert.AreEqual(0, new LaneFaceBars(new[] { 1, 2, 3 }).BarsFor(7).Count);
    }

    [TestMethod]
    public void RegisteredSets_CountsEveryOrderedPrefixOnce()
    {
        // The engine keeps one exclusion set per exact ordered list, and each added id registers a list: [2] [2,3] [1] [1,3] [1,2].
        Assert.AreEqual(5, new LaneFaceBars(new[] { 1, 2, 3 }).RegisteredSets);
    }
}
