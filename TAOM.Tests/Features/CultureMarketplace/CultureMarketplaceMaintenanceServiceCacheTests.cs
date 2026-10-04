using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CultureMarketplace;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Tests.Features.CultureMarketplace;

/// <summary>
/// The daily town pass builds each culture's routed id sets once per service (the routing table never
/// changes after it loads), asks each pool's own id index (CultureItemPool.ContainsItem), and counts a
/// town's guaranteed items in one roster walk. These pin that the caches change no stock decision.
/// Settlements are null: the service only passes them through to the adapter.
/// </summary>
[TestClass]
public class CultureMarketplaceMaintenanceServiceCacheTests
{
    private ICultureItemPoolService _poolService;
    private ITownRosterAdapter _townAdapter;

    [TestInitialize]
    public void Setup()
    {
        _poolService = Substitute.For<ICultureItemPoolService>();
        _townAdapter = Substitute.For<ITownRosterAdapter>();
        _poolService.GetRoutedItemsForCulture(Arg.Any<string>()).Returns(System.Array.Empty<RoutedItem>());
        // Attribute wins, no alias (the CultureMarketplaceMaintenanceServiceFilterTests shape).
        _poolService.ClassifyEffectiveCulture(Arg.Any<string>(), Arg.Any<string>()).Returns(ci => (string)ci[0]);
    }

    private CultureMarketplaceMaintenanceService NewSut() => new(_poolService, _townAdapter);

    private static RoutedItem Routed(string id, int minStock, string culture = "isengard") =>
        new(id, new List<string> { culture }, minStock);

    private static List<RosterItemSnapshot> Rows(params string[] ids) =>
        ids.Select(id => new RosterItemSnapshot(id, "gondor", 1)).ToList();

    private static void StubCounts(ITownRosterAdapter adapter, params (string Id, int Count)[] counts)
    {
        var table = counts.ToDictionary(c => c.Id, c => c.Count);
        adapter.GetItemCounts(Arg.Any<Settlement>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(ci => ((IReadOnlyList<string>)ci[1]).Select(id => table.TryGetValue(id, out var n) ? n : 0).ToArray());
    }

    // ── Guaranteed stock: one walk ──

    [TestMethod]
    public void EnsureGuaranteedStock_CountsEveryGuaranteedItemInOneWalk()
    {
        _poolService.GetRoutedItemsForCulture("isengard").Returns(new List<RoutedItem> { Routed("a", 1), Routed("b", 0), Routed("c", 2) });
        StubCounts(_townAdapter, ("a", 0), ("c", 1));
        _townAdapter.AddItem(null, Arg.Any<string>(), Arg.Any<int>()).Returns(true);

        var added = NewSut().EnsureGuaranteedStock(null, "isengard");

        Assert.AreEqual(2, added);
        _townAdapter.Received(1).GetItemCounts(Arg.Any<Settlement>(), Arg.Any<IReadOnlyList<string>>());
        _townAdapter.Received(1).GetItemCounts(null, Arg.Is<IReadOnlyList<string>>(l => l.SequenceEqual(new[] { "a", "c" })));
        _townAdapter.Received(1).AddItem(null, "a", 1);
        _townAdapter.Received(1).AddItem(null, "c", 1);
        _townAdapter.DidNotReceive().AddItem(null, "b", Arg.Any<int>());
    }

    // The oracle is the old per-item loop: need = MinStock - have when have < MinStock.
    [TestMethod]
    public void EnsureGuaranteedStock_SameTopUpsAsCountingEachItemAlone()
    {
        var routed = new List<RoutedItem> { Routed("a", 1), Routed("b", 3), Routed("c", 2) };
        int cases = 0;
        for (int ha = 0; ha <= 3; ha++)
        for (int hb = 0; hb <= 3; hb++)
        for (int hc = 0; hc <= 3; hc++)
        {
            var pool = Substitute.For<ICultureItemPoolService>();
            var town = Substitute.For<ITownRosterAdapter>();
            pool.GetRoutedItemsForCulture("isengard").Returns(routed);
            StubCounts(town, ("a", ha), ("b", hb), ("c", hc));
            town.AddItem(null, Arg.Any<string>(), Arg.Any<int>()).Returns(true);

            var have = new Dictionary<string, int> { ["a"] = ha, ["b"] = hb, ["c"] = hc };
            int expectedTotal = 0;
            var expectedNeed = new Dictionary<string, int>();
            foreach (var entry in routed)
            {
                if (entry.MinStock <= 0 || have[entry.ItemId] >= entry.MinStock) continue;
                expectedNeed[entry.ItemId] = entry.MinStock - have[entry.ItemId];
                expectedTotal += expectedNeed[entry.ItemId];
            }

            var added = new CultureMarketplaceMaintenanceService(pool, town).EnsureGuaranteedStock(null, "isengard");

            var label = $"have a={ha} b={hb} c={hc}";
            Assert.AreEqual(expectedTotal, added, label);
            foreach (var entry in routed)
            {
                if (expectedNeed.TryGetValue(entry.ItemId, out var need))
                    town.Received(1).AddItem(null, entry.ItemId, need);
                else
                    town.DidNotReceive().AddItem(null, entry.ItemId, Arg.Any<int>());
            }
            Assert.AreEqual(expectedNeed.Count,
                town.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ITownRosterAdapter.AddItem)), label);
            cases++;
        }
        Assert.AreEqual(64, cases);
    }

    [TestMethod]
    public void EnsureGuaranteedStock_ShortCountArray_TreatsMissingCountsAsZero()
    {
        _poolService.GetRoutedItemsForCulture("isengard").Returns(new List<RoutedItem> { Routed("a", 1) });
        _townAdapter.GetItemCounts(Arg.Any<Settlement>(), Arg.Any<IReadOnlyList<string>>()).Returns(new int[0]);
        _townAdapter.AddItem(null, "a", 1).Returns(true);

        Assert.AreEqual(1, NewSut().EnsureGuaranteedStock(null, "isengard"));
        _townAdapter.Received(1).AddItem(null, "a", 1);
    }

    // ── Per-culture sets ──

    [TestMethod]
    public void RoutedItems_AreRequestedOncePerCulture()
    {
        _poolService.GetRoutedItemsForCulture("isengard").Returns(new List<RoutedItem> { Routed("warg_brown", 1) });
        StubCounts(_townAdapter, ("warg_brown", 1));
        _townAdapter.EnumerateRoster(null).Returns(Rows("warg_brown"));
        var sut = NewSut();

        for (int i = 0; i < 3; i++)
        {
            sut.EnsureGuaranteedStock(null, "isengard");
            sut.FilterForeignCultureItems(null, "isengard", 6);
        }

        _poolService.Received(1).GetRoutedItemsForCulture("isengard");
    }

    [TestMethod]
    public void FilterForeignCultureItems_ReadsNoPoolEntries_ThePoolIndexesItsOwnIds()
    {
        var items = new CountingEntries(new ItemPoolEntry("x", 1f), new ItemPoolEntry("y", 1f), new ItemPoolEntry("z", 1f));
        var pool = new CultureItemPool("isengard", items);
        items.IndexerReads = 0; // the pool's constructor read every entry to sum the weights and index the ids
        _poolService.GetPool("isengard").Returns(pool);
        _townAdapter.EnumerateRoster(null).Returns(Rows("x"));
        var sut = NewSut();

        for (int i = 0; i < 3; i++)
            sut.FilterForeignCultureItems(null, "isengard", 6);

        Assert.AreEqual(0, items.IndexerReads, "the filter asks the pool, it never walks the pool's items");
        _townAdapter.DidNotReceiveWithAnyArgs().RemoveItem(default!, default!, default);
    }

    [TestMethod]
    public void CultureItemPool_ContainsItem_IsOrdinalAndCoversEveryEntry()
    {
        var pool = new CultureItemPool("isengard", new[] { new ItemPoolEntry("x", 1f), new ItemPoolEntry("y", 2f) });

        Assert.IsTrue(pool.ContainsItem("x"));
        Assert.IsTrue(pool.ContainsItem("y"));
        Assert.IsFalse(pool.ContainsItem("X"), "ordinal, as the per-call set was");
        Assert.IsFalse(pool.ContainsItem("z"));
        Assert.IsFalse(pool.ContainsItem(null!));
        Assert.IsFalse(new CultureItemPool("isengard", new ItemPoolEntry[0]).ContainsItem("x"), "an empty pool keeps nothing");
    }

    [TestMethod]
    public void FilterForeignCultureItems_NewPoolObject_UsesTheNewPoolsIds()
    {
        var p1 = new CultureItemPool("isengard", new[] { new ItemPoolEntry("x", 1f) });
        var p2 = new CultureItemPool("isengard", new[] { new ItemPoolEntry("y", 1f) });
        _poolService.GetPool("isengard").Returns(p1, p2);
        _townAdapter.EnumerateRoster(null).Returns(Rows("y"));
        _townAdapter.RemoveItem(null, "y", 1).Returns(true);
        var sut = NewSut();

        Assert.AreEqual(1, sut.FilterForeignCultureItems(null, "isengard", 6), "y is foreign to pool P1");
        Assert.AreEqual(0, sut.FilterForeignCultureItems(null, "isengard", 6), "y is in the current pool P2");
        _townAdapter.Received(1).RemoveItem(null, "y", 1);
    }

    [TestMethod]
    public void FilterForeignCultureItems_TwoCulturesOnOneService_KeepTheirOwnSets()
    {
        _poolService.GetRoutedItemsForCulture("isengard").Returns(new List<RoutedItem> { Routed("isen_routed", 1, "isengard") });
        _poolService.GetRoutedItemsForCulture("lindon").Returns(new List<RoutedItem> { Routed("lin_routed", 1, "lindon") });
        _poolService.GetPool("isengard").Returns(new CultureItemPool("isengard", new[] { new ItemPoolEntry("isen_pooled", 1f) }));
        _poolService.GetPool("lindon").Returns(new CultureItemPool("lindon", new[] { new ItemPoolEntry("lin_pooled", 1f) }));
        var everyRow = new[] { "isen_routed", "isen_pooled", "lin_routed", "lin_pooled" };
        _townAdapter.EnumerateRoster(null).Returns(Rows(everyRow), Rows(everyRow));
        _townAdapter.RemoveItem(null, Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        var sut = NewSut();

        Assert.AreEqual(2, sut.FilterForeignCultureItems(null, "isengard", 6));
        _townAdapter.Received(1).RemoveItem(null, "lin_routed", 1);
        _townAdapter.Received(1).RemoveItem(null, "lin_pooled", 1);
        _townAdapter.DidNotReceive().RemoveItem(null, "isen_routed", Arg.Any<int>());
        _townAdapter.DidNotReceive().RemoveItem(null, "isen_pooled", Arg.Any<int>());
        _townAdapter.ClearReceivedCalls();

        Assert.AreEqual(2, sut.FilterForeignCultureItems(null, "lindon", 6));
        _townAdapter.Received(1).RemoveItem(null, "isen_routed", 1);
        _townAdapter.Received(1).RemoveItem(null, "isen_pooled", 1);
        _townAdapter.DidNotReceive().RemoveItem(null, "lin_routed", Arg.Any<int>());
        _townAdapter.DidNotReceive().RemoveItem(null, "lin_pooled", Arg.Any<int>());
    }

    // ── The one error line a failed count logs ──

    [TestMethod]
    public void CountFailureLine_IsTheLiteralErrorLine()
        => Assert.AreEqual(
            "[CultureMarketplace] GetItemCounts('warg_brown','warg_dark' @ town_I1) failed: boom",
            TownRosterAdapter.CountFailureLine("town_I1", new[] { "warg_brown", "warg_dark" }, "boom"));

    // The adapter's engine-free paths: a null settlement or a null id list never reaches the roster.
    [TestMethod]
    public void GetItemCounts_NullSettlementOrIds_ReturnsZeroesOnePerId()
    {
        var logger = Substitute.For<IModLogger>();
        var adapter = new TownRosterAdapter(logger);

        CollectionAssert.AreEqual(new[] { 0, 0 }, adapter.GetItemCounts(null!, new[] { "a", "b" }));
        Assert.AreEqual(0, adapter.GetItemCounts(null!, null!).Length);
        logger.DidNotReceiveWithAnyArgs().LogError(default!);
    }

    /// <summary>A pool item list that counts indexer reads, to prove the filter never walks a pool.</summary>
    private sealed class CountingEntries : IReadOnlyList<ItemPoolEntry>
    {
        private readonly ItemPoolEntry[] _entries;
        public int IndexerReads;

        public CountingEntries(params ItemPoolEntry[] entries) => _entries = entries;

        public int Count => _entries.Length;

        public ItemPoolEntry this[int index]
        {
            get
            {
                IndexerReads++;
                return _entries[index];
            }
        }

        public IEnumerator<ItemPoolEntry> GetEnumerator() => ((IEnumerable<ItemPoolEntry>)_entries).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
