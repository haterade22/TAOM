using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// The optional <c>emissary_price_multiplier</c> attribute of a <c>&lt;Resource&gt;</c>: 1 when absent, the
/// value when it is a whole number from 1 to 1000, and 1 with one warning for anything else.
/// </summary>
[TestClass]
public class SpecialResourceConfigProviderEmissaryPriceTests
{
    private string _tempDir;
    private IPathService _pathService;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "taom_emissary_price_tests_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "special_resources"));

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private SpecialResource Load(string extraAttributes)
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "special_resources", "special_resources_config.xml"),
            $@"<?xml version=""1.0"" encoding=""utf-8""?>
<SpecialResources>
  <Resource id=""gems"" display_name=""Gems"" icon_sprite=""i"" cap=""10000"" {extraAttributes}>
    <Kingdom id=""erebor"" />
  </Resource>
</SpecialResources>");
        return new SpecialResourceConfigProvider(_pathService, _logger).GetByKingdomId("erebor");
    }

    [TestMethod]
    public void Load_NoMultiplier_IsOne()
    {
        var resource = Load("");

        Assert.AreEqual(1, resource.EmissaryPriceMultiplier);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("emissary_price_multiplier")));
    }

    [DataTestMethod]
    [DataRow("20", 20)]
    [DataRow("1", 1)]
    [DataRow("1000", 1000)]
    public void Load_ValidMultiplier_IsUsed(string text, int expected)
    {
        var resource = Load($@"emissary_price_multiplier=""{text}""");

        Assert.AreEqual(expected, resource.EmissaryPriceMultiplier);
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("-5")]
    [DataRow("1001")]
    [DataRow("2.5")]
    [DataRow("many")]
    [DataRow("NaN")]
    public void Load_InvalidMultiplier_FallsBackToOneAndWarnsOnce(string text)
    {
        var resource = Load($@"emissary_price_multiplier=""{text}""");

        Assert.AreEqual(1, resource.EmissaryPriceMultiplier);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("emissary_price_multiplier")));
    }

    // ── The price each troop row carries ──

    private TroopResourceCostEntry LoadTroop(string troopRows, string troopId)
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "special_resources", "special_resources_config.xml"),
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<SpecialResources>
  <Resource id=""gems"" display_name=""Gems"" icon_sprite=""i"" cap=""10000"" emissary_price_multiplier=""20"">
    <Kingdom id=""erebor"" />
  </Resource>
  <Resource id=""war_drums"" display_name=""War Drums"" icon_sprite=""i"" cap=""10000"">
    <Kingdom id=""aserai"" />
  </Resource>
</SpecialResources>");
        File.WriteAllText(
            Path.Combine(_tempDir, "special_resources", "troop_resource_costs.xml"),
            $@"<?xml version=""1.0"" encoding=""utf-8""?>
<TroopResourceCosts>
{troopRows}
</TroopResourceCosts>");
        return new SpecialResourceConfigProvider(_pathService, _logger).GetTroopCost(troopId);
    }

    [TestMethod]
    public void GetTroopCost_RowOfAMultipliedResource_CarriesMerchantCostTimesItsMultiplier()
    {
        var entry = LoadTroop(@"<Troop id=""t1"" resource_id=""gems"" merchant_cost=""28"" />", "t1");

        Assert.AreEqual(28, entry.MerchantCost);
        Assert.AreEqual(560, entry.EmissaryPrice);
    }

    [TestMethod]
    public void GetTroopCost_RowOfAnUnmultipliedResource_CarriesTheRawPrice()
    {
        var entry = LoadTroop(@"<Troop id=""t1"" resource_id=""war_drums"" merchant_cost=""2800"" />", "t1");

        Assert.AreEqual(2800, entry.EmissaryPrice);
    }

    [TestMethod]
    public void GetTroopCost_RowNamingAnUnknownResource_KeepsTheRawPrice()
    {
        var entry = LoadTroop(@"<Troop id=""t1"" resource_id=""no_such_resource"" merchant_cost=""28"" />", "t1");

        Assert.AreEqual(28, entry.EmissaryPrice);
    }

    [TestMethod]
    public void GetTroopCost_RowWithoutAMerchantCost_HasNoEmissaryPrice()
    {
        var entry = LoadTroop(@"<Troop id=""t1"" resource_id=""gems"" upgrade_cost=""3"" />", "t1");

        Assert.AreEqual(0, entry.EmissaryPrice);
    }

    [TestMethod]
    public void GetTroopCost_HugePrice_SaturatesInsteadOfOverflowing()
    {
        var entry = LoadTroop(@"<Troop id=""t1"" resource_id=""gems"" merchant_cost=""2000000000"" />", "t1");

        Assert.AreEqual(int.MaxValue, entry.EmissaryPrice);
    }

    [TestMethod]
    public void GetTroopCost_GoldOnlyOption_KeepsTheRawMerchantCost()
    {
        var settings = Substitute.For<ISpecialResourceSettingsProvider>();
        settings.EliteTroopsCostGoldOnly.Returns(true);
        File.WriteAllText(
            Path.Combine(_tempDir, "special_resources", "special_resources_config.xml"),
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<SpecialResources>
  <Resource id=""gems"" display_name=""Gems"" icon_sprite=""i"" cap=""10000"" emissary_price_multiplier=""20"">
    <Kingdom id=""erebor"" />
  </Resource>
</SpecialResources>");
        File.WriteAllText(
            Path.Combine(_tempDir, "special_resources", "troop_resource_costs.xml"),
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<TroopResourceCosts>
<Troop id=""t1"" resource_id=""gems"" upgrade_cost=""3"" merchant_cost=""28"" />
</TroopResourceCosts>");

        var entry = new SpecialResourceConfigProvider(_pathService, _logger, settings).GetTroopCost("t1");

        Assert.AreEqual(28, entry.MerchantCost); // the gold price reads this, times the MCM rate
        Assert.AreEqual(0, entry.UpgradeCost);
    }
}
