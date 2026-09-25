using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SupplyLines;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// The cross-market goods search is a pure function over a catalogue the VM builds from the
/// orderable sources: no engine, no campaign. Every rule the screen relies on (minimum query
/// length, diacritic/case folding, the nearest-then-cheapest order, the hit cap with an honest
/// total) is pinned here so a change in the engine shows up as a red test, not a confused player.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SupplyGoodsSearchTests
{
    private static SupplySourceInfo Source(string id, string name)
        => new SupplySourceInfo { SettlementId = id, DisplayName = name, CanOrder = true };

    private static SupplyLineItem Line(string id, string name, int available, int unitPrice)
        => new SupplyLineItem { Id = id, Name = name, Available = available, UnitPrice = unitPrice };

    private static SupplyGoodsCatalogueEntry Entry(SupplySourceInfo source, float distance, params SupplyLineItem[] goods)
        => SupplyGoodsCatalogueEntry.Create(source, distance, goods);

    private static List<SupplyGoodsCatalogueEntry> Catalogue(params SupplyGoodsCatalogueEntry[] entries)
        => new List<SupplyGoodsCatalogueEntry>(entries);

    // --- query gating ---

    [TestMethod]
    public void IsActive_OneCharacter_False()
    {
        Assert.IsFalse(SupplyGoodsSearch.IsActive("g"));
    }

    [TestMethod]
    public void IsActive_TwoCharacters_True()
    {
        Assert.IsTrue(SupplyGoodsSearch.IsActive("gr"));
    }

    [TestMethod]
    public void IsActive_WhitespaceOnly_False()
    {
        Assert.IsFalse(SupplyGoodsSearch.IsActive("   "));
    }

    [TestMethod]
    public void IsActive_Null_False()
    {
        Assert.IsFalse(SupplyGoodsSearch.IsActive(null));
    }

    [TestMethod]
    public void IsActive_PaddedTwoCharacters_TrueAfterTrim()
    {
        Assert.IsTrue(SupplyGoodsSearch.IsActive("  gr  "));
    }

    [TestMethod]
    public void IsActive_OneIdeograph_True()
    {
        // One CJK character carries a word; vanilla's encyclopedia lowers the floor the same way.
        Assert.IsTrue(SupplyGoodsSearch.IsActive("米"));
    }

    [TestMethod]
    public void Search_QueryBelowMinimum_ReturnsNothingAndZeroTotal()
    {
        var catalogue = Catalogue(Entry(Source("a", "Bree"), 10f, Line("grain", "Grain", 5, 10)));

        var hits = SupplyGoodsSearch.Search(catalogue, "g", out var total);

        Assert.AreEqual(0, hits.Count);
        Assert.AreEqual(0, total);
    }

    [TestMethod]
    public void Search_NullQuery_ReturnsNothing()
    {
        var catalogue = Catalogue(Entry(Source("a", "Bree"), 10f, Line("grain", "Grain", 5, 10)));

        var hits = SupplyGoodsSearch.Search(catalogue, null, out var total);

        Assert.AreEqual(0, hits.Count);
        Assert.AreEqual(0, total);
    }

    // --- matching ---

    [TestMethod]
    public void Search_CaseDiffers_Matches()
    {
        var catalogue = Catalogue(Entry(Source("a", "Bree"), 10f, Line("grain", "Grain", 5, 10)));

        var hits = SupplyGoodsSearch.Search(catalogue, "GRA", out _);

        Assert.AreEqual(1, hits.Count);
        Assert.AreEqual("grain", hits[0].Item.Id);
    }

    [TestMethod]
    public void Search_DiacriticInName_MatchesPlainQuery()
    {
        var catalogue = Catalogue(Entry(Source("a", "Harad"), 10f, Line("mumak_hide", "Mûmak hide", 2, 300)));

        var hits = SupplyGoodsSearch.Search(catalogue, "mumak", out _);

        Assert.AreEqual(1, hits.Count);
    }

    [TestMethod]
    public void Search_DiacriticInQuery_MatchesPlainName()
    {
        var catalogue = Catalogue(Entry(Source("a", "Harad"), 10f, Line("mumak_hide", "Mumak hide", 2, 300)));

        var hits = SupplyGoodsSearch.Search(catalogue, "mûmak", out _);

        Assert.AreEqual(1, hits.Count);
    }

    [TestMethod]
    public void Search_SubstringInsideName_Matches()
    {
        var catalogue = Catalogue(Entry(Source("a", "Bree"), 10f, Line("wool", "Raw wool", 5, 10)));

        var hits = SupplyGoodsSearch.Search(catalogue, "woo", out _);

        Assert.AreEqual(1, hits.Count);
    }

    [TestMethod]
    public void Search_OneIdeographQuery_Matches()
    {
        var catalogue = Catalogue(Entry(Source("a", "Bree"), 10f, Line("grain", "米", 5, 10)));

        var hits = SupplyGoodsSearch.Search(catalogue, "米", out _);

        Assert.AreEqual(1, hits.Count);
    }

    [TestMethod]
    public void Search_NoMatch_ReturnsNothingAndZeroTotal()
    {
        var catalogue = Catalogue(Entry(Source("a", "Bree"), 10f, Line("grain", "Grain", 5, 10)));

        var hits = SupplyGoodsSearch.Search(catalogue, "zz", out var total);

        Assert.AreEqual(0, hits.Count);
        Assert.AreEqual(0, total);
    }

    // --- catalogue building drops what can never match ---

    [TestMethod]
    public void Create_NoName_FallsBackToTheId()
    {
        // SupplyGoodRowVM shows the id when the name is empty; the search matches the same text.
        var entry = Entry(Source("a", "Bree"), 10f, Line("grain", null!, 5, 10));

        var hits = SupplyGoodsSearch.Search(Catalogue(entry), "gr", out _);

        Assert.AreEqual(1, hits.Count);
        Assert.AreEqual("grain", hits[0].Item.Id);
    }

    [TestMethod]
    public void Create_NoNameAndNoId_Dropped()
    {
        var entry = Entry(Source("a", "Bree"), 10f, Line(null!, string.Empty, 5, 10));

        Assert.AreEqual(0, entry.Goods.Count);
    }

    [TestMethod]
    public void Create_ZeroStock_Dropped()
    {
        var entry = Entry(Source("a", "Bree"), 10f, Line("grain", "Grain", 0, 10));

        Assert.AreEqual(0, entry.Goods.Count);
    }

    [TestMethod]
    public void Create_NullItem_Dropped()
    {
        var entry = Entry(Source("a", "Bree"), 10f, null!, Line("grain", "Grain", 5, 10));

        Assert.AreEqual(1, entry.Goods.Count);
    }

    [TestMethod]
    public void Create_NullGoodsList_EmptyEntry()
    {
        var entry = SupplyGoodsCatalogueEntry.Create(Source("a", "Bree"), 1f, null);

        Assert.AreEqual(0, entry.Goods.Count);
        Assert.AreEqual(1f, entry.Distance);
    }

    [TestMethod]
    public void Create_FoldsTheNameOnce()
    {
        var entry = Entry(Source("a", "Harad"), 10f, Line("mumak_hide", "  Mûmak hide ", 2, 300));

        Assert.AreEqual("Mumak hide", entry.Goods[0].SearchName);
    }

    [TestMethod]
    public void Search_NullEntry_Skipped()
    {
        var catalogue = Catalogue(null!, Entry(Source("b", "Dale"), 10f, Line("grain", "Grain", 5, 10)));

        var hits = SupplyGoodsSearch.Search(catalogue, "gr", out _);

        Assert.AreEqual(1, hits.Count);
    }

    [TestMethod]
    public void Search_EntryWithNullGoods_Skipped()
    {
        var catalogue = Catalogue(
            new SupplyGoodsCatalogueEntry { Source = Source("a", "Bree"), Distance = 1f, Goods = null! },
            Entry(Source("b", "Dale"), 10f, Line("grain", "Grain", 5, 10)));

        var hits = SupplyGoodsSearch.Search(catalogue, "gr", out _);

        Assert.AreEqual(1, hits.Count);
        Assert.AreEqual("b", hits[0].Source.SettlementId);
    }

    [TestMethod]
    public void Search_EmptyCatalogue_ReturnsNothing()
    {
        var hits = SupplyGoodsSearch.Search(new List<SupplyGoodsCatalogueEntry>(), "gr", out var total);

        Assert.AreEqual(0, hits.Count);
        Assert.AreEqual(0, total);
    }

    [TestMethod]
    public void Search_NullCatalogue_ReturnsNothing()
    {
        var hits = SupplyGoodsSearch.Search(null, "gr", out var total);

        Assert.AreEqual(0, hits.Count);
        Assert.AreEqual(0, total);
    }

    // --- shape of a hit ---

    [TestMethod]
    public void Search_Hit_CarriesSourceDistanceAndItem()
    {
        var source = Source("a", "Bree");
        var item = Line("grain", "Grain", 5, 10);
        var catalogue = Catalogue(Entry(source, 12.5f, item));

        var hits = SupplyGoodsSearch.Search(catalogue, "gr", out _);

        Assert.AreSame(source, hits[0].Source);
        Assert.AreSame(item, hits[0].Item);
        Assert.AreEqual(12.5f, hits[0].Distance);
    }

    [TestMethod]
    public void Search_SameQueryMatchesTwoGoodsAtOneSource_TwoHits()
    {
        var catalogue = Catalogue(
            Entry(Source("a", "Bree"), 10f, Line("grain", "Grain", 5, 10), Line("grapes", "Grapes", 2, 30)));

        var hits = SupplyGoodsSearch.Search(catalogue, "gra", out var total);

        Assert.AreEqual(2, hits.Count);
        Assert.AreEqual(2, total);
    }

    // --- ordering ---

    [TestMethod]
    public void Search_Ordering_NearestFirst()
    {
        var catalogue = Catalogue(
            Entry(Source("far", "Dale"), 40f, Line("grain", "Grain", 5, 5)),
            Entry(Source("near", "Bree"), 10f, Line("grain", "Grain", 5, 50)));

        var hits = SupplyGoodsSearch.Search(catalogue, "gr", out _);

        Assert.AreEqual("near", hits[0].Source.SettlementId);
        Assert.AreEqual("far", hits[1].Source.SettlementId);
    }

    [TestMethod]
    public void Search_Ordering_SameDistance_CheapestFirst()
    {
        var catalogue = Catalogue(
            Entry(Source("a", "Bree"), 10f, Line("grapes", "Grapes", 5, 30), Line("grain", "Grain", 5, 10)));

        var hits = SupplyGoodsSearch.Search(catalogue, "gra", out _);

        Assert.AreEqual("grain", hits[0].Item.Id);
        Assert.AreEqual("grapes", hits[1].Item.Id);
    }

    [TestMethod]
    public void Search_Ordering_SameDistanceAndPrice_ByNameOrdinal()
    {
        var catalogue = Catalogue(
            Entry(Source("a", "Bree"), 10f, Line("b", "Grapes", 5, 10), Line("a", "Grain", 5, 10)));

        var hits = SupplyGoodsSearch.Search(catalogue, "gra", out _);

        Assert.AreEqual("Grain", hits[0].Item.Name);
        Assert.AreEqual("Grapes", hits[1].Item.Name);
    }

    [TestMethod]
    public void Search_Ordering_EqualKeys_KeepCatalogueOrder()
    {
        // Two villages at the same distance selling the same good at the same price must not swap
        // between keystrokes, or the list reshuffles under the player's cursor.
        var catalogue = Catalogue(
            Entry(Source("first", "Village A"), 10f, Line("grain", "Grain", 5, 10)),
            Entry(Source("second", "Village B"), 10f, Line("grain", "Grain", 5, 10)));

        var hits = SupplyGoodsSearch.Search(catalogue, "gr", out _);

        Assert.AreEqual("first", hits[0].Source.SettlementId);
        Assert.AreEqual("second", hits[1].Source.SettlementId);
    }

    // --- cap ---

    [TestMethod]
    public void Search_MoreThanMaxHits_CapsAndReportsTotal()
    {
        var catalogue = new List<SupplyGoodsCatalogueEntry>();
        int count = SupplyGoodsSearch.MaxHits + 15;
        for (int i = 0; i < count; i++)
            catalogue.Add(Entry(Source($"s{i}", $"Town {i}"), i, Line("grain", "Grain", 5, 10)));

        var hits = SupplyGoodsSearch.Search(catalogue, "gr", out var total);

        Assert.AreEqual(SupplyGoodsSearch.MaxHits, hits.Count);
        Assert.AreEqual(count, total);
        // The cap keeps the NEAREST ones.
        Assert.AreEqual(0f, hits[0].Distance);
        Assert.AreEqual(SupplyGoodsSearch.MaxHits - 1, (int)hits.Last().Distance);
    }

    [TestMethod]
    public void Search_ExactlyMaxHits_NotCapped()
    {
        var catalogue = new List<SupplyGoodsCatalogueEntry>();
        for (int i = 0; i < SupplyGoodsSearch.MaxHits; i++)
            catalogue.Add(Entry(Source($"s{i}", $"Town {i}"), i, Line("grain", "Grain", 5, 10)));

        var hits = SupplyGoodsSearch.Search(catalogue, "gr", out var total);

        Assert.AreEqual(SupplyGoodsSearch.MaxHits, hits.Count);
        Assert.AreEqual(SupplyGoodsSearch.MaxHits, total);
    }

    // --- normalisation helper ---

    [TestMethod]
    public void Normalize_Null_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, SupplyGoodsSearch.Normalize(null));
    }

    [TestMethod]
    public void Normalize_TrimsAndStripsDiacritics()
    {
        Assert.AreEqual("Mumak", SupplyGoodsSearch.Normalize("  Mûmak "));
    }
}
