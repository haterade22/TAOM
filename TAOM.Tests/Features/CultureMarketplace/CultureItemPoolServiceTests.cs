using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CultureMarketplace;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Tests.Features.CultureMarketplace;

[TestClass]
public class CultureItemPoolServiceTests
{
    private IItemPoolAdapter _adapter;
    private ICultureMarketplaceConfigProvider _config;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<IItemPoolAdapter>();
        _config = Substitute.For<ICultureMarketplaceConfigProvider>();
        _logger = Substitute.For<IModLogger>();

        _config.GetOverridesByCulture().Returns(new Dictionary<string, MarketplaceConfigOverride>());
        _config.GetItemRouting().Returns(new Dictionary<string, IReadOnlyList<string>>());
    }

    private CultureItemPoolService NewSut() => new(_adapter, _config, _logger);

    [TestMethod]
    public void BuildPools_GroupsItemsByCultureAttribute()
    {
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("a1", "gondor", null),
            new("a2", "gondor", null),
            new("b1", "mordor", null),
        });
        var sut = NewSut();

        sut.BuildPools();

        Assert.AreEqual(2, sut.CultureCount);
        Assert.AreEqual(2, sut.GetPool("gondor").Items.Count);
        Assert.AreEqual(1, sut.GetPool("mordor").Items.Count);
    }

    [TestMethod]
    public void BuildPools_PrefixFallback_UsedWhenAttributeMissing()
    {
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("attribute_item", "gondor", null),
            new("prefix_only_item", null, "mordor"),
        });
        var sut = NewSut();

        sut.BuildPools();

        Assert.AreEqual(1, sut.GetPool("gondor").Items.Count);
        Assert.AreEqual(1, sut.GetPool("mordor").Items.Count);
    }

    [TestMethod]
    public void BuildPools_ItemWithNoCultureSignal_Unresolved()
    {
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("orphan_item", null, null),
        });
        var sut = NewSut();

        sut.BuildPools();

        Assert.AreEqual(0, sut.CultureCount);
        Assert.AreEqual(0, sut.TotalItemCount);
    }

    [TestMethod]
    public void BuildPools_BlacklistedItem_NotInPool()
    {
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("anduril", "gondor", null),
            new("imrahil_armor", "gondor", null),
        });
        _config.GetOverridesByCulture().Returns(new Dictionary<string, MarketplaceConfigOverride>
        {
            ["gondor"] = new("gondor",
                new HashSet<string> { "anduril" },
                new Dictionary<string, float>())
        });
        var sut = NewSut();

        sut.BuildPools();

        var gondor = sut.GetPool("gondor");
        Assert.AreEqual(1, gondor.Items.Count);
        Assert.AreEqual("imrahil_armor", gondor.Items[0].ItemId);
    }

    [TestMethod]
    public void BuildPools_BoostedItem_AppliesWeight()
    {
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("boosted", "gondor", null),
            new("normal", "gondor", null),
        });
        _config.GetOverridesByCulture().Returns(new Dictionary<string, MarketplaceConfigOverride>
        {
            ["gondor"] = new("gondor",
                new HashSet<string>(),
                new Dictionary<string, float> { ["boosted"] = 4f })
        });
        var sut = NewSut();

        sut.BuildPools();

        var gondor = sut.GetPool("gondor");
        Assert.AreEqual(4f, gondor.Items.First(e => e.ItemId == "boosted").Weight);
        Assert.AreEqual(1f, gondor.Items.First(e => e.ItemId == "normal").Weight);
        Assert.AreEqual(5f, gondor.TotalWeight, 0.0001f);
    }

    [TestMethod]
    public void BuildPools_CalledTwice_NoOpSecondCall()
    {
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("a1", "gondor", null),
        });
        var sut = NewSut();

        sut.BuildPools();
        sut.BuildPools();

        // Adapter only consulted once.
        _adapter.Received(1).GetAllItems();
    }

    [TestMethod]
    public void GetPool_UnknownCulture_ReturnsNull()
    {
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("a1", "gondor", null),
        });
        var sut = NewSut();
        sut.BuildPools();

        Assert.IsNull(sut.GetPool("undefined_culture"));
        Assert.IsNull(sut.GetPool(null));
        Assert.IsNull(sut.GetPool(""));
    }

    [TestMethod]
    [ExpectedException(typeof(System.InvalidOperationException))]
    public void GetPool_BeforeBuild_Throws()
    {
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>());
        var sut = NewSut();

        sut.GetPool("gondor");
    }

    [TestMethod]
    public void BuildPools_AttributeAndPrefixDisagree_AttributeWins()
    {
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("rohan_special_item", "vlandia", "vlandia"),
        });
        var sut = NewSut();

        sut.BuildPools();

        Assert.AreEqual(1, sut.GetPool("vlandia").Items.Count);
        Assert.AreEqual(0, sut.CultureCount > 1 ? sut.GetPool("rohan")?.Items.Count ?? 0 : 0);
    }

    // Codex review 2026-05-20 (C2): LOTRAOM_horses.xml has items tagged Culture.rohan but
    // rohan is not a valid TAOM culture ID — Rohan towns use vlandia. Alias normalization
    // routes these items into the vlandia pool.
    [TestMethod]
    public void BuildPools_RohanCultureAttribute_NormalizesToVlandia()
    {
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("rohan_horse_armor_scalemail", "rohan", "vlandia"),
            new("rohan_horse_armor_other", "rohan", null),
        });
        var sut = NewSut();

        sut.BuildPools();

        Assert.IsNull(sut.GetPool("rohan"), "no 'rohan' pool should exist; items must be aliased");
        var vlandia = sut.GetPool("vlandia");
        Assert.IsNotNull(vlandia, "vlandia pool must receive aliased Rohan items");
        Assert.AreEqual(2, vlandia.Items.Count);
    }

    [TestMethod]
    public void BuildPools_RohanAlias_IsCaseInsensitive()
    {
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("rohan_item_a", "Rohan", null),
            new("rohan_item_b", "ROHAN", null),
        });
        var sut = NewSut();

        sut.BuildPools();

        Assert.IsNull(sut.GetPool("rohan"));
        Assert.AreEqual(2, sut.GetPool("vlandia").Items.Count);
    }

    // User finding C5 (2026-05-20): wargs should appear in 4 evil-culture markets, not
    // just Isengard. The CultureRouting XML mechanism overrides attribute + prefix and
    // adds the item to every listed culture's pool.
    [TestMethod]
    public void BuildPools_RoutedItem_AppearsInAllListedCulturesNotAttributeCulture()
    {
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("warg_brown", "isengard", null),  // attribute says isengard
        });
        _config.GetItemRouting().Returns(new Dictionary<string, IReadOnlyList<string>>
        {
            ["warg_brown"] = new List<string> { "isengard", "mordor", "gundabad", "dolguldur" },
        });
        var sut = NewSut();

        sut.BuildPools();

        Assert.AreEqual(1, sut.GetPool("isengard")?.Items.Count ?? 0);
        Assert.AreEqual(1, sut.GetPool("mordor")?.Items.Count ?? 0);
        Assert.AreEqual(1, sut.GetPool("gundabad")?.Items.Count ?? 0);
        Assert.AreEqual(1, sut.GetPool("dolguldur")?.Items.Count ?? 0);
    }

    [TestMethod]
    public void BuildPools_RoutedItem_DoesNotAppearInOtherCultures()
    {
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("warg_brown", "isengard", null),
            new("regular_isengard_item", "isengard", null),
        });
        _config.GetItemRouting().Returns(new Dictionary<string, IReadOnlyList<string>>
        {
            ["warg_brown"] = new List<string> { "isengard", "mordor" },
        });
        var sut = NewSut();

        sut.BuildPools();

        // warg_brown appears in isengard + mordor; regular item only in isengard.
        Assert.AreEqual(2, sut.GetPool("isengard").Items.Count);
        var mordor = sut.GetPool("mordor");
        Assert.AreEqual(1, mordor.Items.Count);
        Assert.AreEqual("warg_brown", mordor.Items[0].ItemId);
        // No other pool exists for this item.
        Assert.IsNull(sut.GetPool("gondor"));
        Assert.IsNull(sut.GetPool("vlandia"));
    }

    [TestMethod]
    public void BuildPools_RoutedItem_OverridesAttribute()
    {
        // Item attribute says "gondor" but routing sends it to mordor only.
        // The gondor pool must NOT contain this item.
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("misplaced_item", "gondor", null),
        });
        _config.GetItemRouting().Returns(new Dictionary<string, IReadOnlyList<string>>
        {
            ["misplaced_item"] = new List<string> { "mordor" },
        });
        var sut = NewSut();

        sut.BuildPools();

        Assert.IsNull(sut.GetPool("gondor"));
        Assert.AreEqual(1, sut.GetPool("mordor").Items.Count);
    }

    [TestMethod]
    public void BuildPools_RoutedItem_HonorsBlacklist()
    {
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("warg_brown", "isengard", null),
        });
        _config.GetItemRouting().Returns(new Dictionary<string, IReadOnlyList<string>>
        {
            ["warg_brown"] = new List<string> { "isengard", "mordor" },
        });
        _config.GetOverridesByCulture().Returns(new Dictionary<string, MarketplaceConfigOverride>
        {
            ["mordor"] = new("mordor",
                new HashSet<string> { "warg_brown" },
                new Dictionary<string, float>())
        });
        var sut = NewSut();

        sut.BuildPools();

        // Mordor blacklisted warg_brown, so warg only appears in isengard.
        Assert.AreEqual(1, sut.GetPool("isengard").Items.Count);
        Assert.IsNull(sut.GetPool("mordor"));
    }

    // Codex self-review 2026-05-20 (S2): post-alias dedup. Catches both author-typo
    // (mordor,mordor) and alias-collision (rohan,vlandia) cases.

    [TestMethod]
    public void BuildPools_RoutedItem_DuplicateCultureExact_DedupsToOneEntry()
    {
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("dup_item", "isengard", null),
        });
        _config.GetItemRouting().Returns(new Dictionary<string, IReadOnlyList<string>>
        {
            ["dup_item"] = new List<string> { "mordor", "mordor", "mordor" },
        });
        var sut = NewSut();

        sut.BuildPools();

        var mordor = sut.GetPool("mordor");
        Assert.AreEqual(1, mordor.Items.Count, "duplicate routing entries must collapse to one pool entry");
    }

    [TestMethod]
    public void BuildPools_RoutedItem_AliasCollision_DedupsToOneEntry()
    {
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("collision_item", "isengard", null),
        });
        // 'rohan' aliases to 'vlandia', so this routing is effectively {vlandia, vlandia, mordor}.
        _config.GetItemRouting().Returns(new Dictionary<string, IReadOnlyList<string>>
        {
            ["collision_item"] = new List<string> { "rohan", "vlandia", "mordor" },
        });
        var sut = NewSut();

        sut.BuildPools();

        Assert.AreEqual(1, sut.GetPool("vlandia").Items.Count, "alias-collision must collapse to one pool entry");
        Assert.AreEqual(1, sut.GetPool("mordor").Items.Count);
        Assert.IsNull(sut.GetPool("rohan"));
    }

    [TestMethod]
    public void BuildPools_RoutedItem_RouteCultureAliasNormalized()
    {
        // If a routing target lists an invalid alias like "rohan", normalization
        // should redirect it to "vlandia" before pool grouping.
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("test_item", "isengard", null),
        });
        _config.GetItemRouting().Returns(new Dictionary<string, IReadOnlyList<string>>
        {
            ["test_item"] = new List<string> { "rohan", "mordor" },
        });
        var sut = NewSut();

        sut.BuildPools();

        Assert.IsNull(sut.GetPool("rohan"));
        Assert.AreEqual(1, sut.GetPool("vlandia").Items.Count);
        Assert.AreEqual(1, sut.GetPool("mordor").Items.Count);
    }
}
