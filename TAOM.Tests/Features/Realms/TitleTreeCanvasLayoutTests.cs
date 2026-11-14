using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.Realms.Popups;

namespace TAOM.Tests.Features.Realms;

/// <summary>
/// The top-down title tree canvas (#1255): every title placed once in pre-order, the realm's great titles and chains as
/// pennants, leaf vassals as evened stacks of compact rows, vassals below their liege without overlap, connectors lit
/// exactly on the path to the selected title, and the liege and vassal lookups the details panel shows.
/// </summary>
[TestClass]
public class TitleTreeCanvasLayoutTests
{
    private sealed class N
    {
        public N(string id, params N[] children) { Id = id; Children = children; }
        public string Id { get; }
        public N[] Children { get; }
        public bool Focus { get; set; }
        public bool Lands { get; set; }
        public override string ToString() => Id;
    }

    private const float Tol = 0.01f;

    private static TitleTreeCanvas<N> Lay(N root) => TitleTreeCanvasLayout.Build(root, n => n.Children, n => n.Focus);

    private static IEnumerable<N> All(N n) => new[] { n }.Concat(n.Children.SelectMany(All));

    private static N Leaves(string id, int count) =>
        new(id, Enumerable.Range(0, count).Select(i => new N(id + "_" + i)).ToArray());

    private static int IndexOf(TitleTreeCanvas<N> canvas, string id) =>
        canvas.Cards.Select((c, i) => (c, i)).Single(p => p.c.Item.Id == id).i;

    private static TitleTreeCardPlacement<N> Card(TitleTreeCanvas<N> canvas, string id) => canvas.Cards[IndexOf(canvas, id)];

    // The reference: a realm over three great titles; the side ones hold leaf rows, the middle one a chain down to a barony.
    private static N Reference() => new("realm",
        new N("w1", new N("l1"), new N("l2"), new N("l3")),
        new N("w2", new N("county", new N("barony"))),
        new N("w3", new N("l4"), new N("l5")));

    [TestMethod]
    public void Build_Singleton_PlacesOneRootCardWithNoConnectors()
    {
        var canvas = Lay(new N("solo") { Focus = true });

        Assert.AreEqual(1, canvas.Cards.Count);
        Assert.AreEqual(TitleTreeCardKind.Root, canvas.Cards[0].Kind);
        Assert.AreEqual(0, canvas.Segments.Count);
        Assert.AreEqual(0, canvas.FocusIndex);
    }

    [TestMethod]
    public void Build_PlacesEveryTitleOnce_ParentBeforeItsSubtree()
    {
        var root = Reference();
        var canvas = Lay(root);

        CollectionAssert.AreEquivalent(All(root).Select(n => n.Id).ToList(), canvas.Cards.Select(c => c.Item.Id).ToList());
        for (int i = 1; i < canvas.Cards.Count; i++)
        {
            int parent = canvas.Cards[i].Parent;
            Assert.IsTrue(parent >= 0 && parent < i, canvas.Cards[i].Item.Id);
            Assert.IsTrue(canvas.Cards[parent].Item.Children.Contains(canvas.Cards[i].Item), canvas.Cards[i].Item.Id);
        }
    }

    [TestMethod]
    public void Build_GreatTitlesAndChainsArePennants_LeafVassalsAreRows()
    {
        var canvas = Lay(Reference());

        Assert.AreEqual(TitleTreeCardKind.Root, Card(canvas, "realm").Kind);
        foreach (var id in new[] { "w1", "w2", "w3", "county", "barony" })
            Assert.AreEqual(TitleTreeCardKind.Node, Card(canvas, id).Kind, id);
        foreach (var id in new[] { "l1", "l2", "l3", "l4", "l5" })
            Assert.AreEqual(TitleTreeCardKind.Row, Card(canvas, id).Kind, id);
    }

    [TestMethod]
    public void Build_RealmLeafVassals_StayPennants()
    {
        var canvas = Lay(Leaves("realm", 3));

        Assert.IsTrue(canvas.Cards.Skip(1).All(c => c.Kind == TitleTreeCardKind.Node));
    }

    [TestMethod]
    public void Build_SixLeaves_StackThreeAndThree()
    {
        var canvas = Lay(new N("realm", Leaves("w", 6)));

        var rows = canvas.Cards.Where(c => c.Kind == TitleTreeCardKind.Row).ToList();
        Assert.AreEqual(6, rows.Count);
        var columns = rows.GroupBy(r => r.X).Select(g => g.Count()).ToList();
        CollectionAssert.AreEqual(new[] { 3, 3 }, columns);
    }

    [TestMethod]
    public void Build_VassalsSitBelowTheirLiege_AndNoCardsOverlap()
    {
        var root = new N("realm", Reference().Children.Concat(new[] { new N("w4", Leaves("x", 7).Children) }).ToArray());
        var canvas = Lay(root);

        foreach (var card in canvas.Cards.Where(c => c.Parent >= 0))
        {
            var liege = canvas.Cards[card.Parent];
            Assert.IsTrue(card.Y >= liege.Y + liege.Height + TitleTreeCanvasLayout.LevelGap - Tol, card.Item.Id);
        }
        for (int a = 0; a < canvas.Cards.Count; a++)
            for (int b = a + 1; b < canvas.Cards.Count; b++)
            {
                var p = canvas.Cards[a];
                var q = canvas.Cards[b];
                bool apart = p.X + p.Width <= q.X + Tol || q.X + q.Width <= p.X + Tol
                    || p.Y + p.Height <= q.Y + Tol || q.Y + q.Height <= p.Y + Tol;
                Assert.IsTrue(apart, $"{p.Item.Id} overlaps {q.Item.Id}");
            }
        foreach (var card in canvas.Cards)
            Assert.IsTrue(card.X >= 0f && card.Y >= 0f && card.X + card.Width <= canvas.Width + Tol
                && card.Y + card.Height <= canvas.Height + Tol, card.Item.Id);
    }

    [TestMethod]
    public void Build_ChainRunsStraightDown_AndTheRealmIsCentredOverItsGreatTitles()
    {
        var canvas = Lay(Reference());
        float Centre(string id) => Card(canvas, id).X + Card(canvas, id).Width / 2f;

        Assert.AreEqual(Centre("w2"), Centre("county"), Tol);
        Assert.AreEqual(Centre("county"), Centre("barony"), Tol);
        Assert.AreEqual((Centre("w1") + Centre("w3")) / 2f, Centre("realm"), Tol);
        Assert.AreEqual(canvas.Width / 2f, Centre("realm"), Tol, "a narrow tree sits centred in the viewport");
    }

    [TestMethod]
    public void Build_SmallTree_FillsTheViewport_LargeTreeGrowsTheCanvas()
    {
        var small = Lay(Reference());
        Assert.AreEqual(TitleTreeCanvasLayout.ViewportWidth, small.Width, Tol);
        Assert.AreEqual(TitleTreeCanvasLayout.ViewportHeight, small.Height, Tol);

        var wide = Lay(new N("realm", Enumerable.Range(0, 8).Select(i => new N("w" + i)).ToArray()));
        Assert.IsTrue(wide.Width > TitleTreeCanvasLayout.ViewportWidth);
        Assert.AreEqual(TitleTreeCanvasLayout.Padding, wide.Cards.Min(c => c.X), Tol);

        var node = new N("d6");
        for (int d = 5; d >= 0; d--)
            node = new N("d" + d, node);
        var deep = Lay(node);
        Assert.IsTrue(deep.Height >= deep.Cards.Max(c => c.Y + c.Height) + TitleTreeCanvasLayout.Padding - Tol);
        Assert.IsTrue(deep.Height > TitleTreeCanvasLayout.ViewportHeight);
    }

    [TestMethod]
    public void Build_TallerRow_PushesTheRowsBelowItDown()
    {
        var canvas = TitleTreeCanvasLayout.Build(Reference(), n => n.Children, n => n.Focus,
            (n, kind) => n.Id == "l1" ? 120f : 0f);

        Assert.AreEqual(120f, Card(canvas, "l1").Height, Tol);
        Assert.AreEqual(Card(canvas, "l1").Y + 120f + TitleTreeCanvasLayout.RowGap, Card(canvas, "l2").Y, Tol);
        Assert.AreEqual(TitleTreeCanvasLayout.RowHeight, Card(canvas, "l2").Height, Tol);
    }

    [TestMethod]
    public void Build_TallerNode_DropsItsVassalsAndItsStemBelowIt()
    {
        var canvas = TitleTreeCanvasLayout.Build(Reference(), n => n.Children, n => n.Focus,
            (n, kind) => n.Id == "county" ? 250f : 0f);
        var county = Card(canvas, "county");

        Assert.AreEqual(250f, county.Height, Tol);
        Assert.AreEqual(county.Y + 250f + TitleTreeCanvasLayout.LevelGap, Card(canvas, "barony").Y, Tol);
        int index = IndexOf(canvas, "county");
        Assert.IsTrue(canvas.Segments.Where(s => s.LitFrom == index + 1).All(s => s.Y >= county.Y + 250f - Tol),
            "the connector under the county starts at its new bottom");
    }

    [TestMethod]
    public void Build_ShorterEstimate_KeepsEachKindsHeight()
    {
        var plain = Lay(Reference());
        var fitted = TitleTreeCanvasLayout.Build(Reference(), n => n.Children, n => n.Focus, (n, kind) => 1f);

        for (int i = 0; i < plain.Cards.Count; i++)
        {
            Assert.AreEqual(plain.Cards[i].Y, fitted.Cards[i].Y, Tol, plain.Cards[i].Item.Id);
            Assert.AreEqual(plain.Cards[i].Height, fitted.Cards[i].Height, Tol, plain.Cards[i].Item.Id);
        }
    }

    [TestMethod]
    public void PennantOf_TallerRow_CentresThePennant()
    {
        var pennant = TitleTreeCanvasLayout.PennantOf(TitleTreeCardKind.Row, 120f);

        Assert.AreEqual((120f - pennant.Height) / 2f, pennant.Y, Tol);
        Assert.AreEqual(TitleTreeCanvasLayout.PennantOf(TitleTreeCardKind.Node, 300f).Y,
            TitleTreeCanvasLayout.PennantOf(TitleTreeCardKind.Node, TitleTreeCanvasLayout.NodeHeight).Y, Tol, "a pennant node's banner stays at its top");
    }

    [TestMethod]
    public void Segments_LightExactlyThePathToTheSelectedTitle()
    {
        var canvas = Lay(Reference());
        int barony = IndexOf(canvas, "barony");

        var lit = canvas.Segments.Where(s => s.Lights(barony)).ToList();
        Assert.AreEqual(6, lit.Count, "a stem and a drop for each of realm, w2 and county; w2 and the chain are centred, so no bar pieces");
        float baronyCentre = canvas.Cards[barony].X + canvas.Cards[barony].Width / 2f;
        foreach (var segment in lit)
            Assert.AreEqual(baronyCentre, segment.X + segment.Width / 2f, 1f, "the lit path runs straight down the chain");

        Assert.AreEqual(0, canvas.Segments.Count(s => s.Lights(0)), "the realm lights nothing");
        int l1 = IndexOf(canvas, "l1");
        Assert.IsTrue(canvas.Segments.Where(s => s.Lights(l1)).All(s => !s.Lights(barony) || s.LitFrom == 1),
            "a row's path shares only the realm's stem with the barony's");
    }

    [TestMethod]
    public void FocusIndex_IsTheFocusedTitlesCard()
    {
        var root = Reference();
        All(root).Single(n => n.Id == "county").Focus = true;

        var canvas = Lay(root);

        Assert.AreEqual("county", canvas.Cards[canvas.FocusIndex].Item.Id);
    }

    [TestMethod]
    public void LiegeOf_RealmHasNone_LandsHaveNone_OthersTheirParent()
    {
        var root = Reference();
        All(root).Single(n => n.Id == "w3").Lands = true;
        var canvas = Lay(root);

        Assert.AreEqual(-1, canvas.LiegeOf(0, n => n.Lands));
        Assert.AreEqual(-1, canvas.LiegeOf(IndexOf(canvas, "w3"), n => n.Lands));
        Assert.AreEqual(IndexOf(canvas, "w2"), canvas.LiegeOf(IndexOf(canvas, "county"), n => n.Lands));
        Assert.AreEqual(IndexOf(canvas, "w3"), canvas.LiegeOf(IndexOf(canvas, "l4"), n => n.Lands), "a vassal of lands has its liege");
    }

    [TestMethod]
    public void SubordinatesOf_KeepsDomainOrder_AndLeavesLandsOut()
    {
        var root = new N("realm", new N("w1", new N("leaf1"), new N("mid", new N("x")), new N("leaf2")), new N("w2"));
        All(root).Single(n => n.Id == "w2").Lands = true;
        var canvas = Lay(root);

        var underW1 = canvas.SubordinatesOf(IndexOf(canvas, "w1"), n => n.Children, n => n.Lands)
            .Select(i => canvas.Cards[i].Item.Id).ToList();
        CollectionAssert.AreEqual(new[] { "leaf1", "mid", "leaf2" }, underW1);

        var underRealm = canvas.SubordinatesOf(0, n => n.Children, n => n.Lands).Select(i => canvas.Cards[i].Item.Id).ToList();
        CollectionAssert.AreEqual(new[] { "w1" }, underRealm);
        Assert.AreEqual(0, canvas.SubordinatesOf(IndexOf(canvas, "leaf1"), n => n.Children, n => n.Lands).Count);
    }
}
