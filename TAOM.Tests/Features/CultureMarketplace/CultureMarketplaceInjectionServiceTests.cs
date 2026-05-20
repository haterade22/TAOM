using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CultureMarketplace;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Tests.Features.CultureMarketplace;

[TestClass]
public class CultureMarketplaceInjectionServiceTests
{
    private ICultureItemPoolService _poolService;
    private IModLogger _logger;
    private MarketplaceTuning _tuning;

    [TestInitialize]
    public void Setup()
    {
        _poolService = Substitute.For<ICultureItemPoolService>();
        _logger = Substitute.For<IModLogger>();
        _tuning = new MarketplaceTuning(itemsPerTownPerDay: 6, perTownTotalRosterCap: 60);
    }

    private CultureMarketplaceInjectionService NewSut(MarketplaceTuning tuning = null)
        => new(_poolService, tuning ?? _tuning, _logger);

    private static CultureItemPool MakePool(string cultureId, params (string id, float weight)[] entries)
    {
        var list = new List<ItemPoolEntry>();
        foreach (var (id, w) in entries)
            list.Add(new ItemPoolEntry(id, w));
        return new CultureItemPool(cultureId, list);
    }

    [TestMethod]
    public void SelectItems_NullCulture_ReturnsEmpty()
    {
        var sut = NewSut();
        var result = sut.SelectItems(null, currentRosterCount: 0, new Random(42));
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void SelectItems_UnknownCulture_ReturnsEmpty()
    {
        _poolService.GetPool("undefined").Returns((CultureItemPool)null);
        var sut = NewSut();

        var result = sut.SelectItems("undefined", 0, new Random(42));

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void SelectItems_AtCap_ReturnsEmpty()
    {
        _poolService.GetPool("gondor").Returns(MakePool("gondor", ("a", 1f), ("b", 1f)));
        var sut = NewSut();

        var result = sut.SelectItems("gondor", currentRosterCount: 60, new Random(42));

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void SelectItems_NearCap_ClampsToHeadroom()
    {
        _poolService.GetPool("gondor").Returns(MakePool("gondor", ("a", 1f), ("b", 1f)));
        var sut = NewSut();

        // Cap 60, current 58 → headroom 2 → drawCount = min(6,2) = 2
        var result = sut.SelectItems("gondor", currentRosterCount: 58, new Random(42));

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void SelectItems_TypicalCase_ReturnsConfiguredCount()
    {
        _poolService.GetPool("gondor").Returns(MakePool("gondor",
            ("a", 1f), ("b", 1f), ("c", 1f), ("d", 1f)));
        var sut = NewSut();

        var result = sut.SelectItems("gondor", currentRosterCount: 0, new Random(42));

        Assert.AreEqual(_tuning.ItemsPerTownPerDay, result.Count);
    }

    [TestMethod]
    public void SelectItems_AllPicksFromCulturePool()
    {
        _poolService.GetPool("gondor").Returns(MakePool("gondor",
            ("a", 1f), ("b", 1f), ("c", 1f)));
        var sut = NewSut();

        var result = sut.SelectItems("gondor", currentRosterCount: 0, new Random(42));

        foreach (var id in result)
            Assert.IsTrue(id == "a" || id == "b" || id == "c", $"unexpected item '{id}'");
    }

    [TestMethod]
    public void SelectItems_WeightBiasHolds_OverManyDraws()
    {
        // Boosted item has weight 10, two normal items have weight 1 each → boosted should win
        // roughly 10/12 ≈ 83% of the time. Allow generous bounds for variance.
        _poolService.GetPool("gondor").Returns(MakePool("gondor",
            ("boosted", 10f), ("normal_a", 1f), ("normal_b", 1f)));
        var tuning = new MarketplaceTuning(itemsPerTownPerDay: 1, perTownTotalRosterCap: 100);
        var sut = NewSut(tuning);

        var rng = new Random(1234);
        var boostedHits = 0;
        const int trials = 2000;
        for (var i = 0; i < trials; i++)
        {
            var pick = sut.SelectItems("gondor", currentRosterCount: 0, rng);
            if (pick.Count == 1 && pick[0] == "boosted")
                boostedHits++;
        }

        var ratio = (float)boostedHits / trials;
        Assert.IsTrue(ratio > 0.7f && ratio < 0.95f, $"boosted ratio {ratio:F3} outside expected window [0.70, 0.95]");
    }

    [TestMethod]
    public void SelectItems_EmptyPool_ReturnsEmpty()
    {
        _poolService.GetPool("gondor").Returns(MakePool("gondor"));
        var sut = NewSut();

        var result = sut.SelectItems("gondor", 0, new Random(42));

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void SelectItems_PoolWithZeroTotalWeight_ReturnsEmpty()
    {
        _poolService.GetPool("gondor").Returns(MakePool("gondor", ("a", 0f), ("b", 0f)));
        var sut = NewSut();

        var result = sut.SelectItems("gondor", 0, new Random(42));

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void SelectItems_NullRng_Throws()
    {
        _poolService.GetPool("gondor").Returns(MakePool("gondor", ("a", 1f)));
        var sut = NewSut();

        sut.SelectItems("gondor", 0, null);
    }
}
