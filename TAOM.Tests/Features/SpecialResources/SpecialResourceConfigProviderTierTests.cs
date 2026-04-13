using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.SpecialResources;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// Tests for tier XML parsing in SpecialResourceConfigProvider.
/// Uses temp files to avoid coupling to real module data path.
/// </summary>
[TestClass]
public class SpecialResourceConfigProviderTierTests
{
    private string _tempDir;
    private string _resourceDir;
    private IPathService _pathService;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "taom_tier_tests_" + System.Guid.NewGuid().ToString("N"));
        _resourceDir = Path.Combine(_tempDir, "special_resources");
        Directory.CreateDirectory(_resourceDir);

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

    private void WriteResourceXml(string xml)
    {
        File.WriteAllText(
            Path.Combine(_resourceDir, "special_resources_config.xml"),
            xml);
    }

    // ── Tier parsing — happy path ──

    [TestMethod]
    public void GetByKingdomId_ResourceWithTiers_ParsesTierCount()
    {
        WriteResourceXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<SpecialResources>
  <Resource id=""gems"" display_name=""Gems"" icon_sprite=""taom_gems_icon""
    cap=""600"" starting_amount=""40"" daily_per_town=""1.0""
    per_battle_victory_base=""5"" per_raid=""3"" per_siege_victory=""10""
    per_prisoner=""0"" per_tournament_win=""8"" per_hideout_clear=""4"">
    <Kingdom id=""erebor"" />
    <Culture id=""erebor"" />
    <Tiers>
      <Tier level=""1"" name=""Apprentice Miner"" threshold=""100""
            description=""Dwarven mining efficiency improves."" />
      <Tier level=""2"" name=""Journeyman Smith"" threshold=""250""
            description=""Erebor's forges burn bright."" />
      <Tier level=""3"" name=""Master of the Treasury"" threshold=""400""
            description=""The wealth of Erebor flows."" />
    </Tiers>
  </Resource>
</SpecialResources>");

        var provider = new SpecialResourceConfigProvider(_pathService, _logger);
        var resource = provider.GetByKingdomId("erebor");

        Assert.IsNotNull(resource);
        Assert.AreEqual(3, resource.TierThresholds.Count);
    }

    [TestMethod]
    public void GetByKingdomId_ResourceWithTiers_ParsesTierOneCorrectly()
    {
        WriteResourceXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<SpecialResources>
  <Resource id=""gems"" display_name=""Gems"" icon_sprite=""icon""
    cap=""600"" starting_amount=""0"" daily_per_town=""0""
    per_battle_victory_base=""0"" per_raid=""0"" per_siege_victory=""0"" per_prisoner=""0"">
    <Kingdom id=""erebor"" />
    <Tiers>
      <Tier level=""1"" name=""Apprentice Miner"" threshold=""100""
            description=""Mining desc."" />
    </Tiers>
  </Resource>
</SpecialResources>");

        var provider = new SpecialResourceConfigProvider(_pathService, _logger);
        var resource = provider.GetByKingdomId("erebor");

        var tier = resource.TierThresholds[0];
        Assert.AreEqual(1, tier.Level);
        Assert.AreEqual("Apprentice Miner", tier.Name);
        Assert.AreEqual(100f, tier.Threshold);
        Assert.AreEqual("Mining desc.", tier.Description);
    }

    [TestMethod]
    public void GetByKingdomId_ResourceWithTiers_TiersOrderedByLevel()
    {
        // XML has tiers in reverse order — provider must sort them
        WriteResourceXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<SpecialResources>
  <Resource id=""gems"" display_name=""Gems"" icon_sprite=""icon""
    cap=""600"" starting_amount=""0"" daily_per_town=""0""
    per_battle_victory_base=""0"" per_raid=""0"" per_siege_victory=""0"" per_prisoner=""0"">
    <Kingdom id=""erebor"" />
    <Tiers>
      <Tier level=""3"" name=""Master"" threshold=""400"" description="""" />
      <Tier level=""1"" name=""Apprentice"" threshold=""100"" description="""" />
      <Tier level=""2"" name=""Journeyman"" threshold=""250"" description="""" />
    </Tiers>
  </Resource>
</SpecialResources>");

        var provider = new SpecialResourceConfigProvider(_pathService, _logger);
        var resource = provider.GetByKingdomId("erebor");

        Assert.AreEqual(1, resource.TierThresholds[0].Level);
        Assert.AreEqual(2, resource.TierThresholds[1].Level);
        Assert.AreEqual(3, resource.TierThresholds[2].Level);
    }

    // ── Tier parsing — no tiers element ──

    [TestMethod]
    public void GetByKingdomId_ResourceWithoutTiersElement_ReturnsEmptyTierList()
    {
        WriteResourceXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<SpecialResources>
  <Resource id=""war_spoils"" display_name=""War Spoils"" icon_sprite=""icon""
    cap=""500"" starting_amount=""30"" daily_per_town=""0.2""
    per_battle_victory_base=""14"" per_raid=""12"" per_siege_victory=""20"" per_prisoner=""2"">
    <Kingdom id=""empire_s"" />
  </Resource>
</SpecialResources>");

        var provider = new SpecialResourceConfigProvider(_pathService, _logger);
        var resource = provider.GetByKingdomId("empire_s");

        Assert.IsNotNull(resource);
        Assert.AreEqual(0, resource.TierThresholds.Count);
    }

    [TestMethod]
    public void GetByKingdomId_ResourceWithEmptyTiersElement_ReturnsEmptyTierList()
    {
        WriteResourceXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<SpecialResources>
  <Resource id=""gems"" display_name=""Gems"" icon_sprite=""icon""
    cap=""600"" starting_amount=""0"" daily_per_town=""0""
    per_battle_victory_base=""0"" per_raid=""0"" per_siege_victory=""0"" per_prisoner=""0"">
    <Kingdom id=""erebor"" />
    <Tiers />
  </Resource>
</SpecialResources>");

        var provider = new SpecialResourceConfigProvider(_pathService, _logger);
        var resource = provider.GetByKingdomId("erebor");

        Assert.AreEqual(0, resource.TierThresholds.Count);
    }

    // ── Multi-resource — only gems gets tiers ──

    [TestMethod]
    public void GetAllResources_MixedResources_OnlyGemsHasTiers()
    {
        WriteResourceXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<SpecialResources>
  <Resource id=""gems"" display_name=""Gems"" icon_sprite=""icon""
    cap=""600"" starting_amount=""0"" daily_per_town=""0""
    per_battle_victory_base=""0"" per_raid=""0"" per_siege_victory=""0"" per_prisoner=""0"">
    <Kingdom id=""erebor"" />
    <Tiers>
      <Tier level=""1"" name=""Apprentice"" threshold=""100"" description="""" />
    </Tiers>
  </Resource>
  <Resource id=""war_spoils"" display_name=""War Spoils"" icon_sprite=""icon""
    cap=""500"" starting_amount=""0"" daily_per_town=""0""
    per_battle_victory_base=""0"" per_raid=""0"" per_siege_victory=""0"" per_prisoner=""0"">
    <Kingdom id=""empire_s"" />
  </Resource>
</SpecialResources>");

        var provider = new SpecialResourceConfigProvider(_pathService, _logger);
        var all = provider.GetAllResources();

        Assert.AreEqual(2, all.Count);
        var gems = provider.GetByKingdomId("erebor");
        var warSpoils = provider.GetByKingdomId("empire_s");

        Assert.AreEqual(1, gems.TierThresholds.Count);
        Assert.AreEqual(0, warSpoils.TierThresholds.Count);
    }
}
