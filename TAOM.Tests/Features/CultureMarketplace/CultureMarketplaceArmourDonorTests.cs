using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CultureMarketplace;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Tests.Features.CultureMarketplace;

/// <summary>
/// A culture with no armour of its own draws on another's (<c>&lt;Culture id="lindon" armour_from="rivendell" /&gt;</c>,
/// Mike, 2026-09-27: the Armourer's Commission mapping fills both the markets and the lord kit). Its pool
/// gains the donor's character armour, never the donor's weapons, under its own blacklist; the config
/// refuses a culture that names itself or draws on a culture that draws on a third.
/// </summary>
[TestClass]
public class CultureMarketplaceArmourDonorTests
{
    private IItemPoolAdapter _adapter = null!;
    private ICultureMarketplaceConfigProvider _config = null!;
    private Dictionary<string, MarketplaceConfigOverride> _overrides = null!;
    private Dictionary<string, RoutedItem> _routing = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<IItemPoolAdapter>();
        _config = Substitute.For<ICultureMarketplaceConfigProvider>();
        _overrides = new Dictionary<string, MarketplaceConfigOverride>(StringComparer.OrdinalIgnoreCase);
        _routing = new Dictionary<string, RoutedItem>(StringComparer.Ordinal);
        _config.GetOverridesByCulture().Returns(_overrides);
        _config.GetItemRouting().Returns(_routing);
        _adapter.GetAllItems().Returns(new List<ItemPoolItem>
        {
            new("riv_helm", "rivendell", null, isCharacterArmour: true),
            new("riv_chest", "rivendell", null, isCharacterArmour: true),
            new("riv_sword", "rivendell", null),
            new("lin_bow", "lindon", null),
        });
    }

    private void DrawsOn(string cultureId, string donor, params string[] blacklist) =>
        _overrides[cultureId] = new MarketplaceConfigOverride(cultureId, new HashSet<string>(blacklist),
            new Dictionary<string, float>(), donor);

    private IReadOnlyList<string> Pool(string cultureId)
    {
        var sut = new CultureItemPoolService(_adapter, _config, Substitute.For<IModLogger>());
        sut.BuildPools();
        return sut.GetPool(cultureId)?.Items.Select(e => e.ItemId).ToList() ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    [TestMethod]
    public void BuildPools_ArmourFrom_TheDonorsArmourJoinsThePoolButNotItsWeapons()
    {
        DrawsOn("lindon", "rivendell");

        CollectionAssert.AreEquivalent(new[] { "lin_bow", "riv_helm", "riv_chest" }, Pool("lindon").ToList());
    }

    [TestMethod]
    public void BuildPools_ArmourFrom_LeavesTheDonorsOwnPoolAsItWas()
    {
        DrawsOn("lindon", "rivendell");

        CollectionAssert.AreEquivalent(new[] { "riv_helm", "riv_chest", "riv_sword" }, Pool("rivendell").ToList());
    }

    [TestMethod]
    public void BuildPools_ArmourFrom_ACultureWithNoItemsOfItsOwnGetsAPool()
    {
        DrawsOn("lothlorien", "rivendell");

        CollectionAssert.AreEquivalent(new[] { "riv_helm", "riv_chest" }, Pool("lothlorien").ToList());
    }

    [TestMethod]
    public void BuildPools_ArmourFrom_HonoursTheReceiversBlacklist()
    {
        DrawsOn("lindon", "rivendell", "riv_helm");

        CollectionAssert.DoesNotContain(Pool("lindon").ToList(), "riv_helm");
    }

    [TestMethod]
    public void BuildPools_ArmourFrom_APieceAlreadyThereIsNotAddedTwice()
    {
        _routing["riv_helm"] = new RoutedItem("riv_helm", new[] { "rivendell", "lindon" }, 0);
        DrawsOn("lindon", "rivendell");

        Assert.AreEqual(1, Pool("lindon").Count(id => id == "riv_helm"));
    }

    [TestMethod]
    public void BuildPools_NoArmourFrom_PoolsAreUnchanged()
    {
        CollectionAssert.AreEquivalent(new[] { "lin_bow" }, Pool("lindon").ToList());
    }

    // --- Config: armour_from ---

    private string _tempDir = null!;

    private ICultureMarketplaceConfigProvider ProviderFor(string xml)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "taom_armour_donor_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "culture_marketplace"));
        File.WriteAllText(Path.Combine(_tempDir, "culture_marketplace", "culture_marketplace_config.xml"), xml);
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(_tempDir);
        return new CultureMarketplaceConfigProvider(paths, Substitute.For<IModLogger>());
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_tempDir != null && Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void GetArmourDonor_ReadsArmourFrom()
    {
        var provider = ProviderFor(@"<CultureMarketplaceConfig><Culture id=""lindon"" armour_from=""rivendell"" /></CultureMarketplaceConfig>");

        Assert.AreEqual("rivendell", provider.GetArmourDonor("lindon"));
        Assert.AreEqual("rivendell", provider.GetArmourDonor("LINDON"), "culture ids compare case-insensitively");
        Assert.IsNull(provider.GetArmourDonor("rivendell"));
        Assert.IsNull(provider.GetArmourDonor(null!));
    }

    [TestMethod]
    public void GetArmourDonor_ACultureNamingItself_IsIgnored()
    {
        var provider = ProviderFor(@"<CultureMarketplaceConfig><Culture id=""lindon"" armour_from=""lindon"" /></CultureMarketplaceConfig>");

        Assert.IsNull(provider.GetArmourDonor("lindon"));
    }

    [TestMethod]
    public void GetArmourDonor_AChainIsRefused_TheMiddleLinkKept()
    {
        var provider = ProviderFor(@"<CultureMarketplaceConfig>
  <Culture id=""lindon"" armour_from=""lothlorien"" />
  <Culture id=""lothlorien"" armour_from=""rivendell"" />
</CultureMarketplaceConfig>");

        Assert.IsNull(provider.GetArmourDonor("lindon"), "lothlorien has no armour of its own to hand on");
        Assert.AreEqual("rivendell", provider.GetArmourDonor("lothlorien"));
    }
}
