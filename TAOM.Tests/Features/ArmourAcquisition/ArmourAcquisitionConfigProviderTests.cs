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
            "<VisitingArmourer chance_per_day=\"0.1\" duration_days=\"3\" level_bonus=\"2\" />", enabled: "false"));

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

    // ── The lord's gear ladder (#693) ─────────────────────────────────────────

    [TestMethod]
    public void GetConfig_Ladder_ReadsTheRungsInTheirOrder()
    {
        var config = Load(Wrap(
            "<LordsLadder count_knockouts=\"false\">" +
            "<Step slot=\"head\" quest=\"q_head\" materials=\"7\" />" +
            "<Step slot=\"Hands\" quest=\"q_hands\" materials=\"3\" /></LordsLadder>"));

        var steps = config.Ladder.Steps;
        CollectionAssert.AreEqual(new[] { "Head:q_head:7", "Hands:q_hands:3" },
            steps.Select(s => $"{s.Slot}:{s.QuestId}:{s.Materials}").ToArray());
        Assert.IsFalse(config.Ladder.CountsKnockouts);
    }

    [TestMethod]
    public void GetConfig_LadderRungWithAnUnknownSlot_IsSkipped()
    {
        var config = Load(Wrap(
            "<LordsLadder><Step slot=\"feet\" quest=\"q_feet\" materials=\"3\" />" +
            "<Step slot=\"head\" quest=\"q_head\" materials=\"3\" /></LordsLadder>"));

        CollectionAssert.AreEqual(new[] { LadderSlot.Head }, config.Ladder.Steps.Select(s => s.Slot).ToArray());
        AssertWarned("feet");
    }

    [TestMethod]
    public void GetConfig_LadderRepeatsASlot_KeepsTheFirstRung()
    {
        var config = Load(Wrap(
            "<LordsLadder><Step slot=\"head\" quest=\"q_a\" materials=\"3\" />" +
            "<Step slot=\"head\" quest=\"q_b\" materials=\"3\" /></LordsLadder>"));

        CollectionAssert.AreEqual(new[] { "q_a" }, config.Ladder.Steps.Select(s => s.QuestId).ToArray());
        AssertWarned("head");
    }

    [TestMethod]
    public void GetConfig_LadderRepeatsAQuest_KeepsTheFirstRung()
    {
        // Two rungs on one quest id would share its progress and both complete at once.
        var config = Load(Wrap(
            "<LordsLadder><Step slot=\"head\" quest=\"q_a\" materials=\"3\" />" +
            "<Step slot=\"body\" quest=\"q_a\" materials=\"3\" /></LordsLadder>"));

        CollectionAssert.AreEqual(new[] { LadderSlot.Head }, config.Ladder.Steps.Select(s => s.Slot).ToArray());
        AssertWarned("q_a");
    }

    [TestMethod]
    public void GetConfig_LadderRungWithoutAQuest_IsSkipped()
    {
        var config = Load(Wrap(
            "<LordsLadder><Step slot=\"head\" materials=\"3\" /><Step slot=\"body\" quest=\"q_b\" materials=\"3\" /></LordsLadder>"));

        CollectionAssert.AreEqual(new[] { LadderSlot.Body }, config.Ladder.Steps.Select(s => s.Slot).ToArray());
        AssertWarned("quest");
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("1000")]
    [DataRow("many")]
    public void GetConfig_LadderRungMaterialsOutOfRange_IsSkipped(string materials)
    {
        var config = Load(Wrap(
            $"<LordsLadder><Step slot=\"head\" quest=\"q_a\" materials=\"{materials}\" />" +
            "<Step slot=\"body\" quest=\"q_b\" materials=\"3\" /></LordsLadder>"));

        CollectionAssert.AreEqual(new[] { LadderSlot.Body }, config.Ladder.Steps.Select(s => s.Slot).ToArray());
        AssertWarned("materials");
    }

    [TestMethod]
    public void GetConfig_LadderWithNoValidRung_KeepsTheDefaultRungs()
    {
        var config = Load(Wrap("<LordsLadder><Step slot=\"feet\" quest=\"q\" materials=\"3\" /></LordsLadder>"));

        Assert.AreSame(ArmourAcquisitionConfig.Default.Ladder.Steps, config.Ladder.Steps);
        AssertWarned("no valid <Step>");
    }

    [TestMethod]
    public void GetConfig_LadderCountKnockoutsNotABool_Reverts()
    {
        var config = Load(Wrap("<LordsLadder count_knockouts=\"sometimes\" />"));

        Assert.AreEqual(ArmourAcquisitionConfig.Default.Ladder.CountsKnockouts, config.Ladder.CountsKnockouts);
        AssertWarned("count_knockouts");
    }

    [TestMethod]
    public void GetConfig_Materials_ReadsTheCulturesAndTheDropCurve()
    {
        var config = Load(Wrap(
            "<LordsMaterials base_chance=\"0.2\" chance_per_ten_kills=\"0.05\" max_chance=\"0.9\" min_units=\"2\" max_units=\"4\">" +
            "<Material culture=\"gondor\" item=\"m_gondor\" /><Material culture=\"mordor\" item=\"m_mordor\" /></LordsMaterials>"));

        var ladder = config.Ladder;
        CollectionAssert.AreEquivalent(new[] { "gondor=m_gondor", "mordor=m_mordor" },
            ladder.Materials.Select(p => p.Key + "=" + p.Value).ToArray());
        Assert.AreEqual((0.2f, 0.05f, 0.9f, 2, 4),
            (ladder.Drop.BaseChance, ladder.Drop.ChancePerTenKills, ladder.Drop.MaxChance, ladder.Drop.MinUnits, ladder.Drop.MaxUnits));
    }

    [TestMethod]
    [DataRow("base_chance", "NaN")]
    [DataRow("chance_per_ten_kills", "-0.1")]
    [DataRow("max_chance", "1.5")]
    public void GetConfig_MaterialDropChanceOutOfRange_Reverts(string attr, string value)
    {
        var config = Load(Wrap($"<LordsMaterials {attr}=\"{value}\" />"));

        var d = ArmourAcquisitionConfig.Default.Ladder.Drop;
        var drop = config.Ladder.Drop;
        Assert.AreEqual((d.BaseChance, d.ChancePerTenKills, d.MaxChance), (drop.BaseChance, drop.ChancePerTenKills, drop.MaxChance));
        AssertWarned(attr);
    }

    [TestMethod]
    public void GetConfig_MaterialUnitsInverted_RevertsBoth()
    {
        var config = Load(Wrap("<LordsMaterials min_units=\"5\" max_units=\"2\" />"));

        var d = ArmourAcquisitionConfig.Default.Ladder.Drop;
        Assert.AreEqual((d.MinUnits, d.MaxUnits), (config.Ladder.Drop.MinUnits, config.Ladder.Drop.MaxUnits));
        AssertWarned("min_units");
    }

    [TestMethod]
    public void GetConfig_MaterialBaseChanceAboveTheMax_RevertsBoth()
    {
        // base 0.5 under a max of 0.2 would be a flat 0.2 whatever the kills (RCA 2026-09-28 row 8).
        var config = Load(Wrap("<LordsMaterials base_chance=\"0.5\" max_chance=\"0.2\" />"));

        var d = ArmourAcquisitionConfig.Default.Ladder.Drop;
        Assert.AreEqual((d.BaseChance, d.MaxChance), (config.Ladder.Drop.BaseChance, config.Ladder.Drop.MaxChance));
        AssertWarned("base_chance");
    }

    [TestMethod]
    public void GetConfig_LordsMaterialsWithNoValidRow_KeepsTheDefaults()
    {
        var config = Load(Wrap("<LordsMaterials><Material culture=\"gondor\" /></LordsMaterials>"));

        Assert.AreSame(ArmourAcquisitionConfig.Default.Ladder.Materials, config.Ladder.Materials);
        AssertWarned("no valid <Material>");
    }

    [TestMethod]
    public void GetConfig_LadderWeaponsWithNoValidRow_KeepsTheDefaults()
    {
        // An empty weapon list would leave every weapon rung unclaimable (RCA 2026-09-28 row 9).
        var empty = Load(Wrap("<LadderWeapons />"));
        Assert.AreSame(ArmourAcquisitionConfig.Default.Ladder.Weapons, empty.Ladder.Weapons);

        var invalid = Load(Wrap("<LadderWeapons><Weapon item=\"w_a\" /></LadderWeapons>"));
        Assert.AreSame(ArmourAcquisitionConfig.Default.Ladder.Weapons, invalid.Ladder.Weapons);
        AssertWarned("no valid <Weapon>");
    }

    [TestMethod]
    public void GetConfig_MaterialRepeatsACulture_KeepsTheFirst()
    {
        var config = Load(Wrap(
            "<LordsMaterials><Material culture=\"gondor\" item=\"m_a\" /><Material culture=\"gondor\" item=\"m_b\" /></LordsMaterials>"));

        Assert.AreEqual("m_a", config.Ladder.Materials["gondor"]);
        AssertWarned("gondor");
    }

    [TestMethod]
    public void GetConfig_MaterialWithoutAnItem_IsSkipped()
    {
        var config = Load(Wrap(
            "<LordsMaterials><Material culture=\"gondor\" /><Material culture=\"mordor\" item=\"m_mordor\" /></LordsMaterials>"));

        CollectionAssert.AreEquivalent(new[] { "mordor" }, config.Ladder.Materials.Keys.ToArray());
        AssertWarned("item");
    }

    [TestMethod]
    public void GetConfig_Weapons_GroupByCultureInTheirOrder()
    {
        var config = Load(Wrap(
            "<LadderWeapons><Weapon culture=\"gondor\" item=\"w_b\" /><Weapon culture=\"mordor\" item=\"w_m\" />" +
            "<Weapon culture=\"gondor\" item=\"w_a\" /><Weapon culture=\"gondor\" item=\"w_b\" /></LadderWeapons>"));

        CollectionAssert.AreEqual(new[] { "w_b", "w_a" }, config.Ladder.Weapons["gondor"].ToArray(), "a repeated weapon is kept once");
        CollectionAssert.AreEqual(new[] { "w_m" }, config.Ladder.Weapons["mordor"].ToArray());
        CollectionAssert.AreEquivalent(new[] { "gondor", "mordor" }, config.Ladder.Weapons.Keys.ToArray(),
            "a present <LadderWeapons> lists every culture's picks");
    }

    [TestMethod]
    public void GetConfig_WeaponWithoutACulture_IsSkipped()
    {
        var config = Load(Wrap("<LadderWeapons><Weapon item=\"w_a\" /><Weapon culture=\"mordor\" item=\"w_m\" /></LadderWeapons>"));

        CollectionAssert.AreEquivalent(new[] { "mordor" }, config.Ladder.Weapons.Keys.ToArray());
        AssertWarned("culture");
    }

    [TestMethod]
    public void GetConfig_NoLadderSections_UsesTheDefaults()
    {
        var config = Load(Wrap(""));

        var d = ArmourAcquisitionConfig.Default.Ladder;
        Assert.AreSame(d.Steps, config.Ladder.Steps);
        Assert.AreSame(d.Materials, config.Ladder.Materials);
        Assert.AreSame(d.Weapons, config.Ladder.Weapons);
        Assert.AreEqual(d.CountsKnockouts, config.Ladder.CountsKnockouts);
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
