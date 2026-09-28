using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// armour_acquisition_config.xml is hand-edited, so every field is range-checked, NaN-checked and
/// order-checked at load, and a bad field reverts to the compiled default with a warning plus one
/// summary warning (csharp-architecture.md, "Config Providers MUST Validate"). One test per rule.
/// </summary>
[TestClass]
public class ArmourAcquisitionConfigProviderTests
{
    private string _tempDir = null!;
    private string _configDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "taom_armour_cfg_" + Guid.NewGuid().ToString("N"));
        _configDir = Path.Combine(_tempDir, "armour_acquisition");
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

    private ArmourAcquisitionConfig Load(string? body)
    {
        if (body != null)
            File.WriteAllText(Path.Combine(_configDir, "armour_acquisition_config.xml"), body);
        return new ArmourAcquisitionConfigProvider(_pathService, _logger).GetConfig();
    }

    private static string Wrap(string inner, string enabled = "true") =>
        $"<?xml version=\"1.0\" encoding=\"utf-8\"?><ArmourAcquisition enabled=\"{enabled}\">{inner}</ArmourAcquisition>";

    private void AssertWarned(string fragment) =>
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(fragment)));

    [TestMethod]
    public void GetConfig_NoFile_ReturnsDefaults()
    {
        var config = Load(null);

        Assert.AreSame(ArmourAcquisitionConfig.Default, config);
    }

    [TestMethod]
    public void GetConfig_MalformedXml_ReturnsDefaultsAndLogsError()
    {
        var config = Load("<ArmourAcquisition><Gate");

        Assert.AreSame(ArmourAcquisitionConfig.Default, config);
        _logger.Received().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_ValidFile_ReadsEverySection()
    {
        var config = Load(Wrap(
            "<Gate heavy=\"0\" elite=\"1\" lord=\"2\" />" +
            "<Upgrades><Upgrade target=\"heavy\" gold=\"700\" value_share=\"0.2\" special_resource=\"3\">" +
            "<Material item=\"ironIngot4\" count=\"2\" /><Material item=\"ironIngot3\" count=\"5\" /></Upgrade></Upgrades>" +
            "<NamedWeapons><Item id=\"glamdring\" /></NamedWeapons>" +
            "<LordEvent chance=\"0.5\" cooldown_days=\"10\" leave_relation=\"2\" />" +
            "<VisitingArmourer chance_per_day=\"0.1\" duration_days=\"3\" level_bonus=\"2\" />" +
            "<LordHarness offer_cooldown_days=\"12\" />", enabled: "false"));

        Assert.IsFalse(config.Enabled);
        Assert.AreEqual((0, 1, 2), (config.HeavyLevel, config.EliteLevel, config.LordLevel));
        var heavy = config.Recipes[ArmourClass.Heavy];
        Assert.AreEqual((700, 0.2f, 3f), (heavy.Gold, heavy.ValueShare, heavy.SpecialResource));
        CollectionAssert.AreEqual(new[] { "ironIngot4:2", "ironIngot3:5" },
            heavy.Materials.Select(m => m.ItemId + ":" + m.Count).ToArray());
        Assert.AreEqual(1, config.Recipes.Count, "a present <Upgrades> lists every recipe");
        CollectionAssert.AreEquivalent(new[] { "glamdring" }, config.NamedWeapons.ToArray());
        Assert.AreEqual((0.5f, 10, 2), (config.LordEventChance, config.LordEventCooldownDays, config.LordEventLeaveRelation));
        Assert.AreEqual((0.1f, 3, 2), (config.VisitChancePerDay, config.VisitDurationDays, config.VisitLevelBonus));
        Assert.AreEqual(12, config.HarnessOfferCooldownDays);
    }

    [TestMethod]
    public void GetConfig_EnabledNotABool_RevertsToDefault()
    {
        var config = Load(Wrap("", enabled: "maybe"));

        Assert.IsTrue(config.Enabled);
        AssertWarned("enabled");
    }

    [TestMethod]
    public void GetConfig_GateLevelOutOfRange_RevertsGateToDefaults()
    {
        var config = Load(Wrap("<Gate heavy=\"7\" elite=\"2\" lord=\"3\" />"));

        Assert.AreEqual((1, 2, 3), (config.HeavyLevel, config.EliteLevel, config.LordLevel));
        AssertWarned("heavy");
    }

    [TestMethod]
    public void GetConfig_GateLevelsOutOfOrder_RevertGateToDefaults()
    {
        var config = Load(Wrap("<Gate heavy=\"3\" elite=\"1\" lord=\"2\" />"));

        Assert.AreEqual((1, 2, 3), (config.HeavyLevel, config.EliteLevel, config.LordLevel));
        AssertWarned("order");
    }

    [TestMethod]
    public void GetConfig_UpgradeTargetUnknown_IsSkipped()
    {
        var config = Load(Wrap("<Upgrades><Upgrade target=\"banana\" gold=\"1\" /></Upgrades>"));

        Assert.AreEqual(0, config.Recipes.Count);
        AssertWarned("banana");
    }

    [TestMethod]
    public void GetConfig_UpgradeTargetLight_IsSkipped()
    {
        // Nothing upgrades INTO light; a light recipe is an authoring slip, not a recipe.
        var config = Load(Wrap("<Upgrades><Upgrade target=\"light\" gold=\"1\" /></Upgrades>"));

        Assert.AreEqual(0, config.Recipes.Count);
        AssertWarned("light");
    }

    [TestMethod]
    public void GetConfig_DuplicateUpgradeTarget_KeepsTheFirst()
    {
        var config = Load(Wrap(
            "<Upgrades><Upgrade target=\"elite\" gold=\"11\" /><Upgrade target=\"elite\" gold=\"22\" /></Upgrades>"));

        Assert.AreEqual(11, config.Recipes[ArmourClass.Elite].Gold);
        AssertWarned("duplicate");
    }

    [TestMethod]
    public void GetConfig_NegativeGold_RevertsToTheDefaultRecipeGold()
    {
        var config = Load(Wrap("<Upgrades><Upgrade target=\"elite\" gold=\"-5\" /></Upgrades>"));

        Assert.AreEqual(ArmourAcquisitionConfig.Default.Recipes[ArmourClass.Elite].Gold, config.Recipes[ArmourClass.Elite].Gold);
        AssertWarned("gold");
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("1.5")]
    [DataRow("-0.1")]
    public void GetConfig_ValueShareNotAFiniteFraction_Reverts(string raw)
    {
        var config = Load(Wrap($"<Upgrades><Upgrade target=\"heavy\" value_share=\"{raw}\" /></Upgrades>"));

        Assert.AreEqual(ArmourAcquisitionConfig.Default.Recipes[ArmourClass.Heavy].ValueShare, config.Recipes[ArmourClass.Heavy].ValueShare);
        AssertWarned("value_share");
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("-1")]
    [DataRow("20000")]
    public void GetConfig_SpecialResourceOutOfRange_Reverts(string raw)
    {
        var config = Load(Wrap($"<Upgrades><Upgrade target=\"lord\" special_resource=\"{raw}\" /></Upgrades>"));

        Assert.AreEqual(ArmourAcquisitionConfig.Default.Recipes[ArmourClass.Lord].SpecialResource, config.Recipes[ArmourClass.Lord].SpecialResource);
        AssertWarned("special_resource");
    }

    [TestMethod]
    public void GetConfig_MaterialWithoutItem_IsSkipped()
    {
        var config = Load(Wrap("<Upgrades><Upgrade target=\"heavy\"><Material count=\"2\" /></Upgrade></Upgrades>"));

        Assert.AreEqual(0, config.Recipes[ArmourClass.Heavy].Materials.Count);
        AssertWarned("item");
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("1000")]
    [DataRow("two")]
    public void GetConfig_MaterialCountOutOfRange_IsSkipped(string raw)
    {
        var config = Load(Wrap($"<Upgrades><Upgrade target=\"heavy\"><Material item=\"ironIngot4\" count=\"{raw}\" /></Upgrade></Upgrades>"));

        Assert.AreEqual(0, config.Recipes[ArmourClass.Heavy].Materials.Count);
        AssertWarned("count");
    }

    [TestMethod]
    public void GetConfig_DuplicateMaterial_KeepsTheFirst()
    {
        var config = Load(Wrap(
            "<Upgrades><Upgrade target=\"heavy\"><Material item=\"ironIngot4\" count=\"2\" />" +
            "<Material item=\"ironIngot4\" count=\"9\" /></Upgrade></Upgrades>"));

        var materials = config.Recipes[ArmourClass.Heavy].Materials;
        Assert.AreEqual(1, materials.Count);
        Assert.AreEqual(2, materials[0].Count);
        AssertWarned("ironIngot4");
    }

    [TestMethod]
    public void GetConfig_NamedWeaponsAbsent_KeepsTheDefaultList()
    {
        var config = Load(Wrap(""));

        CollectionAssert.AreEquivalent(ArmourAcquisitionConfig.Default.NamedWeapons.ToArray(), config.NamedWeapons.ToArray());
    }

    [TestMethod]
    public void GetConfig_NamedWeaponWithoutId_IsIgnored()
    {
        var config = Load(Wrap("<NamedWeapons><Item /><Item id=\"sting\" /></NamedWeapons>"));

        CollectionAssert.AreEquivalent(new[] { "sting" }, config.NamedWeapons.ToArray());
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("2")]
    [DataRow("-0.5")]
    public void GetConfig_LordEventChanceNotAProbability_Reverts(string raw)
    {
        var config = Load(Wrap($"<LordEvent chance=\"{raw}\" />"));

        Assert.AreEqual(ArmourAcquisitionConfig.Default.LordEventChance, config.LordEventChance);
        AssertWarned("chance");
    }

    [TestMethod]
    public void GetConfig_LordEventCooldownNegative_Reverts()
    {
        var config = Load(Wrap("<LordEvent cooldown_days=\"-3\" />"));

        Assert.AreEqual(ArmourAcquisitionConfig.Default.LordEventCooldownDays, config.LordEventCooldownDays);
        AssertWarned("cooldown_days");
    }

    [TestMethod]
    public void GetConfig_LordEventLeaveRelationOutOfRange_Reverts()
    {
        var config = Load(Wrap("<LordEvent leave_relation=\"500\" />"));

        Assert.AreEqual(ArmourAcquisitionConfig.Default.LordEventLeaveRelation, config.LordEventLeaveRelation);
        AssertWarned("leave_relation");
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("1.01")]
    public void GetConfig_VisitChanceNotAProbability_Reverts(string raw)
    {
        var config = Load(Wrap($"<VisitingArmourer chance_per_day=\"{raw}\" />"));

        Assert.AreEqual(ArmourAcquisitionConfig.Default.VisitChancePerDay, config.VisitChancePerDay);
        AssertWarned("chance_per_day");
    }

    [TestMethod]
    public void GetConfig_VisitDurationZero_Reverts()
    {
        var config = Load(Wrap("<VisitingArmourer duration_days=\"0\" />"));

        Assert.AreEqual(ArmourAcquisitionConfig.Default.VisitDurationDays, config.VisitDurationDays);
        AssertWarned("duration_days");
    }

    [TestMethod]
    public void GetConfig_VisitLevelBonusAboveTheLevelCap_Reverts()
    {
        var config = Load(Wrap("<VisitingArmourer level_bonus=\"4\" />"));

        Assert.AreEqual(ArmourAcquisitionConfig.Default.VisitLevelBonus, config.VisitLevelBonus);
        AssertWarned("level_bonus");
    }

    [TestMethod]
    public void GetConfig_HarnessOfferCooldownNegative_Reverts()
    {
        var config = Load(Wrap("<LordHarness offer_cooldown_days=\"-1\" />"));

        Assert.AreEqual(ArmourAcquisitionConfig.Default.HarnessOfferCooldownDays, config.HarnessOfferCooldownDays);
        AssertWarned("offer_cooldown_days");
    }

    [TestMethod]
    public void GetConfig_AnyReversion_EmitsOneSummaryWarning()
    {
        Load(Wrap("<Gate heavy=\"9\" /><LordEvent chance=\"NaN\" />"));

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("reverted")));
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ParsesOnce()
    {
        File.WriteAllText(Path.Combine(_configDir, "armour_acquisition_config.xml"), Wrap(""));
        var provider = new ArmourAcquisitionConfigProvider(_pathService, _logger);

        Assert.AreSame(provider.GetConfig(), provider.GetConfig());
    }
}
