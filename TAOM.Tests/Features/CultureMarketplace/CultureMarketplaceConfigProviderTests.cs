using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CultureMarketplace;

namespace TAOM.Tests.Features.CultureMarketplace;

[TestClass]
public class CultureMarketplaceConfigProviderTests
{
    private string _tempDir;
    private string _configDir;
    private IPathService _pathService;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "taom_marketplace_cfg_" + Guid.NewGuid().ToString("N"));
        _configDir = Path.Combine(_tempDir, "culture_marketplace");
        Directory.CreateDirectory(_configDir);

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

    private void WriteConfig(string xml)
    {
        File.WriteAllText(Path.Combine(_configDir, "culture_marketplace_config.xml"), xml);
    }

    [TestMethod]
    public void GetOverridesByCulture_MissingFile_ReturnsEmptyDict()
    {
        var provider = new CultureMarketplaceConfigProvider(_pathService, _logger);

        var result = provider.GetOverridesByCulture();

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void GetOverridesByCulture_MalformedXml_ReturnsEmptyAndLogsError()
    {
        WriteConfig("<<<not xml>>>");
        var provider = new CultureMarketplaceConfigProvider(_pathService, _logger);

        var result = provider.GetOverridesByCulture();

        Assert.AreEqual(0, result.Count);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetOverridesByCulture_BlacklistEntry_RetainedInOverride()
    {
        WriteConfig(@"<CultureMarketplaceConfig>
  <Culture id=""gondor"">
    <Blacklist><Item id=""anduril"" /><Item id=""theoden_sword"" /></Blacklist>
  </Culture>
</CultureMarketplaceConfig>");
        var provider = new CultureMarketplaceConfigProvider(_pathService, _logger);

        var result = provider.GetOverridesByCulture();

        Assert.IsTrue(result.ContainsKey("gondor"));
        var gondor = result["gondor"];
        Assert.AreEqual(2, gondor.Blacklist.Count);
        Assert.IsTrue(gondor.Blacklist.Contains("anduril"));
        Assert.IsTrue(gondor.Blacklist.Contains("theoden_sword"));
    }

    [TestMethod]
    public void GetOverridesByCulture_BoostEntry_RetainsWeight()
    {
        WriteConfig(@"<CultureMarketplaceConfig>
  <Culture id=""mordor"">
    <Boost><Item id=""sm_mordor_shield_tower_a"" weight=""3.5"" /></Boost>
  </Culture>
</CultureMarketplaceConfig>");
        var provider = new CultureMarketplaceConfigProvider(_pathService, _logger);

        var gondor = provider.GetOverridesByCulture()["mordor"];

        Assert.AreEqual(3.5f, gondor.WeightBoosts["sm_mordor_shield_tower_a"], 0.0001f);
    }

    [TestMethod]
    public void GetOverridesByCulture_NaNWeight_RevertsToOneAndWarns()
    {
        WriteConfig(@"<CultureMarketplaceConfig>
  <Culture id=""gondor"">
    <Boost><Item id=""x"" weight=""NaN"" /></Boost>
  </Culture>
</CultureMarketplaceConfig>");
        var provider = new CultureMarketplaceConfigProvider(_pathService, _logger);

        var gondor = provider.GetOverridesByCulture()["gondor"];

        Assert.AreEqual(1f, gondor.WeightBoosts["x"]);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("non-finite") || s.Contains("outside")));
    }

    [TestMethod]
    public void GetOverridesByCulture_InfinityWeight_RevertsToOneAndWarns()
    {
        WriteConfig(@"<CultureMarketplaceConfig>
  <Culture id=""gondor"">
    <Boost><Item id=""x"" weight=""Infinity"" /></Boost>
  </Culture>
</CultureMarketplaceConfig>");
        var provider = new CultureMarketplaceConfigProvider(_pathService, _logger);

        var gondor = provider.GetOverridesByCulture()["gondor"];

        Assert.AreEqual(1f, gondor.WeightBoosts["x"]);
    }

    [TestMethod]
    public void GetOverridesByCulture_NegativeWeight_RevertsToOne()
    {
        WriteConfig(@"<CultureMarketplaceConfig>
  <Culture id=""gondor"">
    <Boost><Item id=""x"" weight=""-1.0"" /></Boost>
  </Culture>
</CultureMarketplaceConfig>");
        var provider = new CultureMarketplaceConfigProvider(_pathService, _logger);

        var gondor = provider.GetOverridesByCulture()["gondor"];

        Assert.AreEqual(1f, gondor.WeightBoosts["x"]);
    }

    [TestMethod]
    public void GetOverridesByCulture_WeightOverMax_RevertsToOne()
    {
        WriteConfig(@"<CultureMarketplaceConfig>
  <Culture id=""gondor"">
    <Boost><Item id=""x"" weight=""1000000"" /></Boost>
  </Culture>
</CultureMarketplaceConfig>");
        var provider = new CultureMarketplaceConfigProvider(_pathService, _logger);

        var gondor = provider.GetOverridesByCulture()["gondor"];

        Assert.AreEqual(1f, gondor.WeightBoosts["x"]);
    }

    [TestMethod]
    public void GetOverridesByCulture_MissingWeightAttribute_RevertsToOne()
    {
        WriteConfig(@"<CultureMarketplaceConfig>
  <Culture id=""gondor"">
    <Boost><Item id=""x"" /></Boost>
  </Culture>
</CultureMarketplaceConfig>");
        var provider = new CultureMarketplaceConfigProvider(_pathService, _logger);

        var gondor = provider.GetOverridesByCulture()["gondor"];

        Assert.AreEqual(1f, gondor.WeightBoosts["x"]);
    }

    [TestMethod]
    public void GetOverridesByCulture_CultureWithoutId_Skipped()
    {
        WriteConfig(@"<CultureMarketplaceConfig>
  <Culture>
    <Blacklist><Item id=""x"" /></Blacklist>
  </Culture>
  <Culture id=""mordor"">
    <Blacklist><Item id=""y"" /></Blacklist>
  </Culture>
</CultureMarketplaceConfig>");
        var provider = new CultureMarketplaceConfigProvider(_pathService, _logger);

        var result = provider.GetOverridesByCulture();

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.ContainsKey("mordor"));
    }

    [TestMethod]
    public void GetOverridesByCulture_EmptyDocument_ReturnsEmptyDict()
    {
        WriteConfig(@"<CultureMarketplaceConfig></CultureMarketplaceConfig>");
        var provider = new CultureMarketplaceConfigProvider(_pathService, _logger);

        var result = provider.GetOverridesByCulture();

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void GetOverridesByCulture_CalledTwice_DoesNotReparse()
    {
        WriteConfig(@"<CultureMarketplaceConfig>
  <Culture id=""gondor""><Blacklist><Item id=""anduril"" /></Blacklist></Culture>
</CultureMarketplaceConfig>");
        var provider = new CultureMarketplaceConfigProvider(_pathService, _logger);

        var first = provider.GetOverridesByCulture();
        var second = provider.GetOverridesByCulture();

        Assert.AreSame(first, second);
    }

    // User finding C5 (2026-05-20): <Routing> XML section for cross-culture item routing.

    [TestMethod]
    public void GetItemRouting_MissingFile_ReturnsEmptyDict()
    {
        var provider = new CultureMarketplaceConfigProvider(_pathService, _logger);

        var result = provider.GetItemRouting();

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void GetItemRouting_RoutingBlockWithFourWargs_ParsesCorrectly()
    {
        WriteConfig(@"<CultureMarketplaceConfig>
  <Routing>
    <Item id=""warg_brown""  cultures=""isengard,mordor,gundabad,dolguldur"" />
    <Item id=""warg_dark""   cultures=""isengard,mordor,gundabad,dolguldur"" />
    <Item id=""warg_albino"" cultures=""isengard,mordor,gundabad,dolguldur"" />
    <Item id=""warg_saddle"" cultures=""isengard,mordor,gundabad,dolguldur"" />
  </Routing>
</CultureMarketplaceConfig>");
        var provider = new CultureMarketplaceConfigProvider(_pathService, _logger);

        var result = provider.GetItemRouting();

        Assert.AreEqual(4, result.Count);
        Assert.IsTrue(result.ContainsKey("warg_brown"));
        var wargBrown = result["warg_brown"];
        Assert.AreEqual(4, wargBrown.Count);
        Assert.IsTrue(wargBrown.Contains("isengard"));
        Assert.IsTrue(wargBrown.Contains("mordor"));
        Assert.IsTrue(wargBrown.Contains("gundabad"));
        Assert.IsTrue(wargBrown.Contains("dolguldur"));
    }

    [TestMethod]
    public void GetItemRouting_RoutingItemMissingId_SkippedAndWarns()
    {
        WriteConfig(@"<CultureMarketplaceConfig>
  <Routing>
    <Item cultures=""mordor"" />
    <Item id=""valid"" cultures=""mordor"" />
  </Routing>
</CultureMarketplaceConfig>");
        var provider = new CultureMarketplaceConfigProvider(_pathService, _logger);

        var result = provider.GetItemRouting();

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.ContainsKey("valid"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("missing id")));
    }

    [TestMethod]
    public void GetItemRouting_RoutingItemMissingCultures_SkippedAndWarns()
    {
        WriteConfig(@"<CultureMarketplaceConfig>
  <Routing><Item id=""x"" /></Routing>
</CultureMarketplaceConfig>");
        var provider = new CultureMarketplaceConfigProvider(_pathService, _logger);

        var result = provider.GetItemRouting();

        Assert.AreEqual(0, result.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("missing cultures")));
    }

    [TestMethod]
    public void GetItemRouting_RoutingItemEmptyCulturesString_Skipped()
    {
        WriteConfig(@"<CultureMarketplaceConfig>
  <Routing><Item id=""x"" cultures="" , , "" /></Routing>
</CultureMarketplaceConfig>");
        var provider = new CultureMarketplaceConfigProvider(_pathService, _logger);

        var result = provider.GetItemRouting();

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void GetItemRouting_CulturesWithWhitespace_AreTrimmed()
    {
        WriteConfig(@"<CultureMarketplaceConfig>
  <Routing><Item id=""x"" cultures="" isengard , mordor "" /></Routing>
</CultureMarketplaceConfig>");
        var provider = new CultureMarketplaceConfigProvider(_pathService, _logger);

        var x = provider.GetItemRouting()["x"];

        Assert.AreEqual(2, x.Count);
        Assert.IsTrue(x.Contains("isengard"));
        Assert.IsTrue(x.Contains("mordor"));
    }
}
