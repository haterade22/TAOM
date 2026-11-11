using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Tests.Features.Realms;

[TestClass]
public class TitleTreeTests
{
    // e > k > d > (c: town with lordships l1, l2) and (b: castle with lordship l3)
    private static TitleCatalog Catalog() => new(new[]
    {
        new TitleDefinition("e", TitleRank.Empire, "E"),
        new TitleDefinition("k", TitleRank.Kingdom, "K", "e"),
        new TitleDefinition("d", TitleRank.Duchy, "D", "k"),
        new TitleDefinition("c", TitleRank.County, "C", "d", "town"),
        new TitleDefinition("b", TitleRank.Barony, "B", "d", "castle"),
        new TitleDefinition("l2", TitleRank.Lordship, "L2", "c", "v2"),
        new TitleDefinition("l1", TitleRank.Lordship, "L1", "c", "v1"),
        new TitleDefinition("l3", TitleRank.Lordship, "L3", "b", "v3"),
    });

    private static TitleTreeNode Find(TitleTreeNode node, string id) =>
        node.Title.Id == id ? node : node.Children.Select(c => Find(c, id)).FirstOrDefault(n => n != null)!;

    [TestMethod]
    public void Roots_at_the_highest_liege_and_marks_only_the_focus()
    {
        var tree = TitleTree.Build(Catalog(), "d")!;

        Assert.AreEqual("e", tree.Root.Title.Id);
        Assert.IsTrue(Find(tree.Root, "d").IsFocus);
        Assert.IsFalse(Find(tree.Root, "k").IsFocus);
        Assert.IsFalse(Find(tree.Root, "c").IsFocus);
    }

    [TestMethod]
    public void Folds_lordships_away_from_the_focus()
    {
        var tree = TitleTree.Build(Catalog(), "d")!;
        var county = Find(tree.Root, "c");
        var barony = Find(tree.Root, "b");

        Assert.AreEqual(2, county.FoldedLordships);
        Assert.AreEqual(0, county.Children.Count);
        Assert.AreEqual(1, barony.FoldedLordships);
    }

    [TestMethod]
    public void Draws_the_lordships_of_the_focus_and_of_the_focus_lordships_parent()
    {
        var atCounty = TitleTree.Build(Catalog(), "c")!;
        var county = Find(atCounty.Root, "c");
        Assert.AreEqual(0, county.FoldedLordships);
        CollectionAssert.AreEqual(new[] { "l1", "l2" }, county.Children.Select(n => n.Title.Id).ToArray());
        Assert.AreEqual(1, Find(atCounty.Root, "b").FoldedLordships);

        var atLordship = TitleTree.Build(Catalog(), "l2")!;
        Assert.IsTrue(Find(atLordship.Root, "l2").IsFocus);
        Assert.AreEqual(0, Find(atLordship.Root, "c").FoldedLordships);
    }

    [TestMethod]
    public void Orders_vassals_highest_rank_first_and_handles_a_root_title()
    {
        var tree = TitleTree.Build(Catalog(), "e")!;
        CollectionAssert.AreEqual(new[] { "c", "b" }, Find(tree.Root, "d").Children.Select(n => n.Title.Id).ToArray());
        Assert.IsTrue(tree.Root.IsFocus);
        Assert.IsNull(TitleTree.Build(Catalog(), "missing"));
    }
}
