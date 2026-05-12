using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.EditorCacheRebuild.Diff;

namespace TAOM.Tests.Features.EditorCacheRebuild.Diff;

[TestClass]
public class SettlementDiffTests
{
    [TestMethod]
    public void TotalChanged_SumsAddedRemovedMoved()
    {
        var diff = new SettlementDiff
        {
            Added = new HashSet<string> { "a", "b" },
            Removed = new HashSet<string> { "c" },
            Moved = new HashSet<string> { "d", "e", "f" },
        };

        Assert.AreEqual(6, diff.TotalChanged);
    }

    [TestMethod]
    public void AllChangedIds_UnionsAllThreeSets()
    {
        var diff = new SettlementDiff
        {
            Added = new HashSet<string> { "a" },
            Removed = new HashSet<string> { "b" },
            Moved = new HashSet<string> { "c" },
        };

        var all = diff.AllChangedIds();

        CollectionAssert.AreEquivalent(new[] { "a", "b", "c" }, new List<string>(all));
    }

    [TestMethod]
    public void AllChangedIds_HandlesOverlaps()
    {
        var diff = new SettlementDiff
        {
            Added = new HashSet<string> { "a" },
            Moved = new HashSet<string> { "a", "b" },
        };

        var all = diff.AllChangedIds();

        Assert.AreEqual(2, all.Count);
        Assert.IsTrue(all.Contains("a"));
        Assert.IsTrue(all.Contains("b"));
    }

    [TestMethod]
    public void ForcedFullRebuild_DefaultsFalse()
    {
        var diff = new SettlementDiff();

        Assert.IsFalse(diff.ForcedFullRebuild);
        Assert.IsNull(diff.Reason);
        Assert.AreEqual(0, diff.TotalChanged);
    }
}
