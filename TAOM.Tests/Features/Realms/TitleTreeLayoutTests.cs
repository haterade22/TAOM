using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Tests.Features.Realms;

[TestClass]
public class TitleTreeLayoutTests
{
    private static readonly TitleTreeNode[] None = Array.Empty<TitleTreeNode>();

    private static TitleTreeNode Leaf(string id) => new(new TitleDefinition(id, TitleRank.Barony, id), false, 0, None);

    private static TitleTreeNode Branch(string id) =>
        new(new TitleDefinition(id, TitleRank.County, id), false, 0, new[] { Leaf(id + "_v") });

    private static IReadOnlyList<TitleTreeNode> Leaves(int n) => Enumerable.Range(0, n).Select(i => Leaf("l" + i)).ToList();

    private static int[] Sizes(IReadOnlyList<IReadOnlyList<TitleTreeNode>> stacks) => stacks.Select(s => s.Count).ToArray();

    [TestMethod]
    public void Branches_keep_their_order_and_leaves_go_to_stacks()
    {
        var (branches, stacks) = TitleTreeLayout.Split(new[] { Leaf("a"), Branch("b"), Leaf("c"), Branch("d") });

        CollectionAssert.AreEqual(new[] { "b", "d" }, branches.Select(n => n.Title.Id).ToArray());
        Assert.AreEqual(1, stacks.Count);
        CollectionAssert.AreEqual(new[] { "a", "c" }, stacks[0].Select(n => n.Title.Id).ToArray());
    }

    [TestMethod]
    public void Leaves_fill_as_few_stacks_as_possible_evened_out()
    {
        CollectionAssert.AreEqual(new[] { 5 }, Sizes(TitleTreeLayout.Split(Leaves(5)).Stacks));
        CollectionAssert.AreEqual(new[] { 3, 3 }, Sizes(TitleTreeLayout.Split(Leaves(6)).Stacks));
        CollectionAssert.AreEqual(new[] { 4, 4, 3 }, Sizes(TitleTreeLayout.Split(Leaves(11)).Stacks));
        CollectionAssert.AreEqual(new[] { 4, 4, 4 }, Sizes(TitleTreeLayout.Split(Leaves(12)).Stacks));
    }

    [TestMethod]
    public void Stacks_keep_the_leaf_order()
    {
        var order = TitleTreeLayout.Split(Leaves(7)).Stacks.SelectMany(s => s).Select(n => n.Title.Id).ToArray();

        CollectionAssert.AreEqual(Leaves(7).Select(n => n.Title.Id).ToArray(), order);
    }

    [TestMethod]
    public void No_leaves_make_no_stacks()
    {
        var (branches, stacks) = TitleTreeLayout.Split(new[] { Branch("a") });

        Assert.AreEqual(1, branches.Count);
        Assert.AreEqual(0, stacks.Count);
        Assert.AreEqual(0, TitleTreeLayout.Split(None).Stacks.Count);
    }
}
