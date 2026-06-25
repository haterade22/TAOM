using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.EliteEmissary;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Tests.Features.EliteEmissary;

[TestClass]
public class EliteEmissaryConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private ISpecialResourceConfigProvider _resourceConfig = null!;
    private EliteEmissaryConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_EliteEmissary_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "elite_emissary");
        Directory.CreateDirectory(_featureDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _resourceConfig = Substitute.For<ISpecialResourceConfigProvider>();

        // Priced troops (merchant_cost > 0)
        _resourceConfig.GetTroopCost("gondor_a").Returns(Priced("gondor_a", 30));
        _resourceConfig.GetTroopCost("gondor_b").Returns(Priced("gondor_b", 50));
        // Unpriced troop (no merchant_cost row)
        _resourceConfig.GetTroopCost("gondor_unpriced").Returns((TroopResourceCostEntry)null);
        // gondor maps to a special resource (Castar); goblin deliberately does NOT (substitute default null).
        _resourceConfig.GetByCultureId("gondor").Returns(
            new SpecialResource("caster", new[] { "empire_w" }, new[] { "gondor" }, "Castar", "icon",
                500f, 25f, 0.6f, 8f, 4f, 18f, 1f));

        _sut = new EliteEmissaryConfigProvider(_pathService, _logger, _resourceConfig);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private static TroopResourceCostEntry Priced(string id, int merchant) =>
        new(id, "caster", upgradeCost: 0, dailyUpkeep: 0f, recruitCost: 0, merchantCost: merchant);

    private void WriteConfig(string xml) =>
        File.WriteAllText(Path.Combine(_featureDir, "elite_emissary_config.xml"), xml);

    [TestMethod]
    public void GetConfig_ValidXml_LoadsKeySettlementsAndCultureOffers()
    {
        WriteConfig(@"<EliteEmissary enabled=""true"">
  <KeySettlements>
    <Settlement id=""town_EW1"" />
    <Settlement id=""town_ES1"" />
  </KeySettlements>
  <CultureOffers>
    <Culture id=""gondor"">
      <Troop id=""gondor_a"" />
      <Troop id=""gondor_b"" />
    </Culture>
  </CultureOffers>
</EliteEmissary>");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.IsTrue(config.KeySettlementIds.Contains("town_EW1"));
        Assert.IsTrue(config.KeySettlementIds.Contains("town_ES1"));
        Assert.AreEqual(2, config.KeySettlementIds.Count);
        Assert.IsTrue(config.CultureOffers.ContainsKey("gondor"));
        Assert.AreEqual(2, config.CultureOffers["gondor"].Count);
        Assert.AreEqual("gondor_a", config.CultureOffers["gondor"][0]);
    }

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsEmptyAndLogsWarning()
    {
        var config = _sut.GetConfig();

        Assert.IsFalse(config.Enabled);
        Assert.AreEqual(0, config.KeySettlementIds.Count);
        Assert.AreEqual(0, config.CultureOffers.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedXml_ReturnsEmptyAndLogsError()
    {
        WriteConfig("<EliteEmissary><not closed");

        var config = _sut.GetConfig();

        Assert.AreEqual(0, config.CultureOffers.Count);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("parse")));
    }

    [TestMethod]
    public void GetConfig_UnknownCultureId_DroppedWithWarning()
    {
        WriteConfig(@"<EliteEmissary enabled=""true"">
  <CultureOffers>
    <Culture id=""gondorr"">
      <Troop id=""gondor_a"" />
    </Culture>
  </CultureOffers>
</EliteEmissary>");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.CultureOffers.ContainsKey("gondorr"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("gondorr")));
    }

    [TestMethod]
    public void GetConfig_TroopWithoutMerchantCost_DroppedWithWarning()
    {
        WriteConfig(@"<EliteEmissary enabled=""true"">
  <CultureOffers>
    <Culture id=""gondor"">
      <Troop id=""gondor_a"" />
      <Troop id=""gondor_unpriced"" />
    </Culture>
  </CultureOffers>
</EliteEmissary>");

        var config = _sut.GetConfig();

        Assert.AreEqual(1, config.CultureOffers["gondor"].Count);
        Assert.AreEqual("gondor_a", config.CultureOffers["gondor"][0]);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("gondor_unpriced")));
    }

    [TestMethod]
    public void GetConfig_KnownCultureWithNoResourceMapping_DroppedWithWarning()
    {
        // goblin is a known culture id but maps to NO special resource -> its offers would be silently
        // dead at runtime (ResolveResource null + HideWhenNoResource). Drop+warn at load. Codex 2026-06-25 [MED]/S4.
        _resourceConfig.GetTroopCost("goblin_x").Returns(Priced("goblin_x", 12));
        // _resourceConfig.GetByCultureId("goblin") returns null (substitute default)

        WriteConfig(@"<EliteEmissary enabled=""true"">
  <CultureOffers>
    <Culture id=""goblin"">
      <Troop id=""goblin_x"" />
    </Culture>
  </CultureOffers>
</EliteEmissary>");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.CultureOffers.ContainsKey("goblin"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("goblin") && s.Contains("no special resource")));
    }

    [TestMethod]
    public void GetConfig_CultureWithNoValidTroops_NotRecorded()
    {
        WriteConfig(@"<EliteEmissary enabled=""true"">
  <CultureOffers>
    <Culture id=""gondor"">
      <Troop id=""gondor_unpriced"" />
    </Culture>
  </CultureOffers>
</EliteEmissary>");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.CultureOffers.ContainsKey("gondor"));
    }

    [TestMethod]
    public void GetConfig_EnabledFalse_Honored()
    {
        WriteConfig(@"<EliteEmissary enabled=""false""><KeySettlements /><CultureOffers /></EliteEmissary>");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.Enabled);
    }
}
