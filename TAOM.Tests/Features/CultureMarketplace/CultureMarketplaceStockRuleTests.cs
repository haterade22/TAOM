using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CultureMarketplace;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Tests.Features.CultureMarketplace;

/// <summary>
/// &lt;Stock&gt; rows name what a culture's markets carry beyond its own items (#755, Mike 2026-10-08):
/// Arthedain sells the Arnor kit and Numenorean blades first and Gondor's goods as a supplement;
/// Lindon sells Rivendell's silver armour and all of Rivendell's weapons. A culture with Stock rows
/// takes its market armour from them, not from armour_from, which still names its lord-kit donor.
/// </summary>
[TestClass]
public class CultureMarketplaceStockRuleTests
{
    private IItemPoolAdapter _adapter = null!;
    private ICultureMarketplaceConfigProvider _config = null!;
    private Dictionary<string, MarketplaceConfigOverride> _overrides = null!;
    private Dictionary<string, RoutedItem> _routing = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<IItemPoolAdapter>();
        _config = Substitute.For<ICultureMarketplaceConfigProvider>();
        _overrides = new Dictionary<string, MarketplaceConfigOverride>(StringComparer.OrdinalIgnoreCase);
        _config.GetOverridesByCulture().Returns(_overrides);
        _routing = new Dictionary<string, RoutedItem>(StringComparer.Ordinal);
        _config.GetItemRouting().Returns(_routing);
        _logger = Substitute.For<IModLogger>();
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("sk_ar_art_chest_a", "gondor", null, isCharacterArmour: true),
            new("sk_gd_chest_a", "gondor", null, isCharacterArmour: true),
            new("gondor_sword", "gondor", null, isWeapon: true),
            new("gondor_salt", "gondor", null),
            new("numenorean_sword", null, null, isWeapon: true),
            new("riv_body_silver_a", "rivendell", null, isCharacterArmour: true),
            new("riv_helm_silvergold", "rivendell", null, isCharacterArmour: true),
            new("riv_body_gold_a", "rivendell", null, isCharacterArmour: true),
            new("riv_sword", "rivendell", null, isWeapon: true),
            new("riv_bow", "rivendell", null, isWeapon: true),
            new("riv_sword_silver", "rivendell", null, isWeapon: true),
        });
    }

    private static StockRule Rule(string from, StockKind kind, string match, float weight = 1f) =>
        new(from, kind, match == null ? null : new Regex(match), weight);

    private void Stocks(string cultureId, string armourFrom, params StockRule[] rules) =>
        _overrides[cultureId] = new MarketplaceConfigOverride(cultureId, new HashSet<string>(),
            new Dictionary<string, float>(), armourFrom, rules);

    private CultureItemPool Pool(string cultureId)
    {
        var sut = new CultureItemPoolService(_adapter, _config, _logger);
        sut.BuildPools();
        return sut.GetPool(cultureId);
    }

    private static List<string> Ids(CultureItemPool pool) => pool.Items.Select(e => e.ItemId).OrderBy(i => i).ToList();

    [TestMethod]
    public void BuildPools_Arthedain_ArnorAndNumenoreanFirst_GondorAsSupplement()
    {
        Stocks("arthedain", "gondor",
            Rule(null, StockKind.Any, "^sk_ar_art_", 3f),
            Rule(null, StockKind.Any, "^numenorean_", 3f),
            Rule("gondor", StockKind.Any, null, 0.5f));

        var pool = Pool("arthedain");

        CollectionAssert.AreEqual(new[] { "gondor_salt", "gondor_sword", "numenorean_sword", "sk_ar_art_chest_a", "sk_gd_chest_a" }, Ids(pool));
        var weight = pool.Items.ToDictionary(e => e.ItemId, e => e.Weight);
        Assert.AreEqual(3f, weight["sk_ar_art_chest_a"], "the first matching row sets the weight");
        Assert.AreEqual(3f, weight["numenorean_sword"]);
        Assert.AreEqual(0.5f, weight["sk_gd_chest_a"]);
    }

    [TestMethod]
    public void BuildPools_Lindon_SilverArmourAndEveryWeapon_NotTheGold()
    {
        Stocks("lindon", "rivendell",
            Rule("rivendell", StockKind.Armour, "silver"),
            Rule("rivendell", StockKind.Weapons, null));

        CollectionAssert.AreEqual(new[] { "riv_body_silver_a", "riv_bow", "riv_helm_silvergold", "riv_sword", "riv_sword_silver" }, Ids(Pool("lindon")));
    }

    [TestMethod]
    public void BuildPools_ACultureWithoutStockRows_StillDrawsItsArmourFromItsDonor()
    {
        Stocks("lothlorien", "rivendell");

        CollectionAssert.AreEqual(new[] { "riv_body_gold_a", "riv_body_silver_a", "riv_helm_silvergold" }, Ids(Pool("lothlorien")));
    }

    [TestMethod]
    public void BuildPools_StockRows_HonourTheBlacklist()
    {
        _overrides["lindon"] = new MarketplaceConfigOverride("lindon", new HashSet<string> { "riv_bow" },
            new Dictionary<string, float>(), "rivendell", new[] { Rule("rivendell", StockKind.Weapons, null) });

        CollectionAssert.AreEqual(new[] { "riv_sword", "riv_sword_silver" }, Ids(Pool("lindon")));
    }

    [TestMethod]
    public void BuildPools_ANotArmourRow_TakesEverythingButCharacterArmour()
    {
        Stocks("arthedain", "gondor", Rule("gondor", StockKind.NotArmour, null));

        CollectionAssert.AreEqual(new[] { "gondor_salt", "gondor_sword" }, Ids(Pool("arthedain")));
    }

    [TestMethod]
    public void BuildPools_AnArmourRow_TakesNoWeaponWhoseIdMatches()
    {
        Stocks("lindon", "rivendell", Rule("rivendell", StockKind.Armour, "silver"));

        CollectionAssert.AreEqual(new[] { "riv_body_silver_a", "riv_helm_silvergold" }, Ids(Pool("lindon")));
    }

    [TestMethod]
    public void BuildPools_AFromRow_SkipsAnItemRoutedAwayFromTheDonor()
    {
        _routing["riv_sword"] = new RoutedItem("riv_sword", new[] { "mirkwood" }, 0);
        Stocks("lindon", "rivendell", Rule("rivendell", StockKind.Weapons, null));

        CollectionAssert.AreEqual(new[] { "riv_bow", "riv_sword_silver" }, Ids(Pool("lindon")));
    }

    [TestMethod]
    public void BuildPools_AFromRow_TakesAnItemRoutedToTheDonor()
    {
        _routing["gondor_sword"] = new RoutedItem("gondor_sword", new[] { "rivendell" }, 0);
        Stocks("lindon", "rivendell", Rule("rivendell", StockKind.Weapons, null));

        CollectionAssert.Contains(Ids(Pool("lindon")), "gondor_sword");
    }

    [TestMethod]
    public void BuildPools_AFromRow_HonoursTheDonorsBlacklist()
    {
        _overrides["rivendell"] = new MarketplaceConfigOverride("rivendell", new HashSet<string> { "riv_bow" },
            new Dictionary<string, float>());
        Stocks("lindon", "rivendell", Rule("rivendell", StockKind.Weapons, null));

        CollectionAssert.AreEqual(new[] { "riv_sword", "riv_sword_silver" }, Ids(Pool("lindon")));
    }

    [TestMethod]
    public void BuildPools_AnItemTheCultureOwns_IsNeitherDuplicatedNorReweighted()
    {
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("lin_bow", "lindon", null, isWeapon: true),
            new("riv_bow", "rivendell", null, isWeapon: true),
        });
        Stocks("lindon", "rivendell", Rule(null, StockKind.Weapons, "_bow$", 5f));

        var pool = Pool("lindon");

        CollectionAssert.AreEqual(new[] { "lin_bow", "riv_bow" }, Ids(pool));
        Assert.AreEqual(1f, pool.Items.Single(e => e.ItemId == "lin_bow").Weight);
        Assert.AreEqual(5f, pool.Items.Single(e => e.ItemId == "riv_bow").Weight);
    }

    [TestMethod]
    public void BuildPools_ABoost_BeatsAStockRowsWeight()
    {
        _overrides["lindon"] = new MarketplaceConfigOverride("lindon", new HashSet<string>(),
            new Dictionary<string, float> { ["riv_bow"] = 7f }, "rivendell",
            new[] { Rule("rivendell", StockKind.Weapons, null, 2f) });

        var weight = Pool("lindon").Items.ToDictionary(e => e.ItemId, e => e.Weight);

        Assert.AreEqual(7f, weight["riv_bow"]);
        Assert.AreEqual(2f, weight["riv_sword"]);
    }

    [TestMethod]
    public void BuildPools_ARowThatAddsNothing_IsWarned()
    {
        Stocks("arthedain", "gondor", Rule("gondr", StockKind.Any, null), Rule(null, StockKind.Any, "^sk_ar_art_"));

        Pool("arthedain");

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("gondr") && s.Contains("adds no item")));
    }

    // --- Config ---

    private string _tempDir = null!;

    private (ICultureMarketplaceConfigProvider Provider, IModLogger Logger) ProviderFor(string cultureXml)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "taom_stock_rules_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "culture_marketplace"));
        File.WriteAllText(Path.Combine(_tempDir, "culture_marketplace", "culture_marketplace_config.xml"),
            "<CultureMarketplaceConfig>" + cultureXml + "</CultureMarketplaceConfig>");
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(_tempDir);
        var logger = Substitute.For<IModLogger>();
        return (new CultureMarketplaceConfigProvider(paths, logger), logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_tempDir != null && Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void Config_ReadsNotArmour()
    {
        var (provider, _) = ProviderFor(@"<Culture id=""arthedain""><Stock from=""gondor"" kind=""not_armour"" /></Culture>");

        Assert.AreEqual(StockKind.NotArmour, provider.GetOverridesByCulture()["arthedain"].Stock[0].Kind);
    }

    [TestMethod]
    public void Config_ReadsStockRowsInOrder()
    {
        var (provider, logger) = ProviderFor(@"<Culture id=""lindon"" armour_from=""rivendell"">
  <Stock from=""rivendell"" kind=""armour"" match=""silver"" weight=""2"" />
  <Stock from=""rivendell"" kind=""weapons"" />
</Culture>");

        var rules = provider.GetOverridesByCulture()["lindon"].Stock;

        Assert.AreEqual(2, rules.Count);
        Assert.AreEqual("rivendell", rules[0].From);
        Assert.AreEqual(StockKind.Armour, rules[0].Kind);
        Assert.IsTrue(rules[0].Match.IsMatch("riv_body_silver_a"));
        Assert.AreEqual(2f, rules[0].Weight);
        Assert.AreEqual(StockKind.Weapons, rules[1].Kind);
        Assert.IsNull(rules[1].Match);
        Assert.AreEqual(1f, rules[1].Weight);
        Assert.AreEqual("rivendell", provider.GetArmourDonor("lindon"), "armour_from still names the donor");
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ShippedConfig_ArthedainAndLindon_StockTheirOwnGearFirst()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(TAOM.Tests.Core.CultureDataFixture.ModuleDataPath());
        var logger = Substitute.For<IModLogger>();
        var overrides = new CultureMarketplaceConfigProvider(paths, logger).GetOverridesByCulture();

        var arthedain = overrides["arthedain"].Stock;
        Assert.IsTrue(arthedain.Any(r => r.Match != null && r.Match.IsMatch("sk_ar_art_chest_inf_heavy_a") && r.Weight > 1f), "Arnor armour first");
        Assert.IsTrue(arthedain.Any(r => r.Match != null && r.Match.IsMatch("numenorean_bastard_heavy_a") && r.Weight > 1f), "Numenorean blades first");
        Assert.IsTrue(arthedain.Any(r => r.From == "gondor" && r.Kind == StockKind.NotArmour && r.Weight < 1f),
            "Gondor only fills the slots the Arnor kit lacks");
        Assert.IsTrue(overrides["arthedain"].Blacklist.Contains("sk_ar_art_crown_king_a"), "the crown is never sold");
        Assert.IsTrue(overrides["gondor"].Blacklist.Contains("sk_ar_art_crown_king_a"), "nor in Gondor's markets");

        var lindon = overrides["lindon"].Stock;
        Assert.IsTrue(lindon.Any(r => r.From == "rivendell" && r.Kind == StockKind.Armour
                                      && r.Match.IsMatch("rivendell_torso_heavy_tier1_silvergold") && !r.Match.IsMatch("rivendell_body_gold_a")));
        Assert.IsTrue(lindon.Any(r => r.From == "rivendell" && r.Kind == StockKind.Weapons && r.Match == null));
        Assert.IsTrue(lindon.Any(r => r.From == "rivendell" && r.Kind == StockKind.Armour && r.Match != null
                                      && r.Match.IsMatch("rivendell_cape_a") && !r.Match.IsMatch("rivendell_torso_heavy_tier1")),
            "the plain cloth and leather pieces, never the gold");
        Assert.AreEqual("rivendell", overrides["lindon"].ArmourFrom, "the lord kit still comes from Rivendell");
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Config_ARefusedArmourFromChain_KeepsTheStockRows()
    {
        var (provider, _) = ProviderFor(@"<Culture id=""lindon"" armour_from=""lothlorien""><Stock from=""rivendell"" kind=""weapons"" /></Culture>
<Culture id=""lothlorien"" armour_from=""rivendell"" />");

        var lindon = provider.GetOverridesByCulture()["lindon"];

        Assert.IsNull(lindon.ArmourFrom);
        Assert.AreEqual(1, lindon.Stock.Count);
    }

    [DataTestMethod]
    [DataRow(@"<Stock match=""[unclosed"" />", "match")]
    [DataRow(@"<Stock from=""gondor"" kind=""spears"" />", "not_armour")]
    [DataRow(@"<Stock kind=""armour"" />", "neither from nor match")]
    public void Config_ARowThatCannotWork_IsSkippedWithAWarning(string row, string named)
    {
        var (provider, logger) = ProviderFor(@"<Culture id=""arthedain"">" + row + "</Culture>");

        Assert.AreEqual(0, provider.GetOverridesByCulture()["arthedain"].Stock.Count);
        logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(named)));
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("-1")]
    [DataRow("2000")]
    [DataRow("heavy")]
    public void Config_ABadWeight_RevertsToOneWithAWarning(string weight)
    {
        var (provider, logger) = ProviderFor(@"<Culture id=""arthedain""><Stock match=""^sk_ar_art_"" weight=""" + weight + @""" /></Culture>");

        var rules = provider.GetOverridesByCulture()["arthedain"].Stock;

        Assert.AreEqual(1, rules.Count);
        Assert.AreEqual(1f, rules[0].Weight);
        logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("weight")));
    }
}
