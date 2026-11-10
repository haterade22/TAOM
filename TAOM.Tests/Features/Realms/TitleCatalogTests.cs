using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Tests.Features.Realms;

[TestClass]
public class TitleCatalogTests
{
    [TestMethod]
    public void Builds_parent_child_links_and_settlement_index()
    {
        var catalog = new TitleCatalog(new[]
        {
            new TitleDefinition("k", TitleRank.Kingdom, "K"),
            new TitleDefinition("c", TitleRank.County, "C", "k", "town"),
        });

        Assert.AreEqual(0, catalog.Problems.Count);
        Assert.AreEqual("c", catalog.ChildrenOf("k").Single().Id);
        Assert.AreEqual("c", catalog.ForSettlement("town")!.Id);
        CollectionAssert.AreEqual(new[] { "k" }, catalog.AncestorsOf("c").Select(t => t.Id).ToArray());
    }

    [TestMethod]
    public void Drops_duplicate_ids_and_reports_them()
    {
        var catalog = new TitleCatalog(new[]
        {
            new TitleDefinition("x", TitleRank.County, "first"),
            new TitleDefinition("x", TitleRank.Barony, "second"),
        });

        Assert.AreEqual(1, catalog.Count);
        Assert.AreEqual("first", catalog.Get("x")!.Name);
        Assert.AreEqual(1, catalog.Problems.Count);
    }

    [TestMethod]
    public void Detaches_a_title_whose_parent_is_missing_or_not_higher()
    {
        var catalog = new TitleCatalog(new[]
        {
            new TitleDefinition("b", TitleRank.Barony, "B"),
            new TitleDefinition("c", TitleRank.County, "C", "b"),
            new TitleDefinition("l", TitleRank.Lordship, "L", "nowhere"),
        });

        Assert.IsNull(catalog.Get("c")!.ParentId);
        Assert.IsNull(catalog.Get("l")!.ParentId);
        Assert.AreEqual(2, catalog.Problems.Count);
    }
}
