using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace TAOM.Tests.Features;

[TestClass]
public class TaomSettingsTests
{
    private string _tempDir;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "taom_settings_test_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public void LoadFrom_FileNotFound_ReturnsDefaults()
    {
        var path = Path.Combine(_tempDir, "nonexistent.json");

        var settings = TAOM.Features.TaomSettings.LoadFrom(path);

        Assert.IsNotNull(settings);
        Assert.IsTrue(settings.WarOfTheRingEnabled);
        Assert.AreEqual(30, settings.Phase1TriggerDay);
        Assert.AreEqual(45, settings.Phase2TriggerDay);
        Assert.IsFalse(settings.TestMode);
        Assert.IsTrue(settings.EnableCustomTroopPower);
        Assert.AreEqual(2.91f, settings.Tier7Power, 0.001f);
        Assert.AreEqual(4, settings.FastForwardMultiplier);
        Assert.AreEqual(0.15f, settings.ArmyBorderProximityFloor, 0.001f);
    }

    [TestMethod]
    public void LoadFrom_ValidJson_ReturnsParsedValues()
    {
        var path = Path.Combine(_tempDir, "settings.json");
        var json = JsonConvert.SerializeObject(new
        {
            WarOfTheRingEnabled = false,
            Phase1TriggerDay = 60,
            Tier7Power = 5.0f,
            FastForwardMultiplier = 16,
            ArmyBorderProximityFloor = 0.5f,
            EnableSiegeDefenseEvents = false
        });
        File.WriteAllText(path, json);

        var settings = TAOM.Features.TaomSettings.LoadFrom(path);

        Assert.IsFalse(settings.WarOfTheRingEnabled);
        Assert.AreEqual(60, settings.Phase1TriggerDay);
        Assert.AreEqual(5.0f, settings.Tier7Power, 0.001f);
        Assert.AreEqual(16, settings.FastForwardMultiplier);
        Assert.AreEqual(0.5f, settings.ArmyBorderProximityFloor, 0.001f);
        Assert.IsFalse(settings.EnableSiegeDefenseEvents);
    }

    [TestMethod]
    public void LoadFrom_PartialJson_MergesWithDefaults()
    {
        var path = Path.Combine(_tempDir, "partial.json");
        File.WriteAllText(path, "{ \"WarOfTheRingEnabled\": false }");

        var settings = TAOM.Features.TaomSettings.LoadFrom(path);

        Assert.IsFalse(settings.WarOfTheRingEnabled);
        Assert.AreEqual(30, settings.Phase1TriggerDay);
        Assert.IsTrue(settings.EnableCustomTroopPower);
        Assert.AreEqual(2.91f, settings.Tier7Power, 0.001f);
    }

    [TestMethod]
    public void LoadFrom_MalformedJson_ReturnsDefaults()
    {
        var path = Path.Combine(_tempDir, "bad.json");
        File.WriteAllText(path, "{ this is not valid json }}}");

        var settings = TAOM.Features.TaomSettings.LoadFrom(path);

        Assert.IsNotNull(settings);
        Assert.IsTrue(settings.WarOfTheRingEnabled);
        Assert.AreEqual(30, settings.Phase1TriggerDay);
    }

    [TestMethod]
    public void LoadFrom_EmptyFile_ReturnsDefaults()
    {
        var path = Path.Combine(_tempDir, "empty.json");
        File.WriteAllText(path, "");

        var settings = TAOM.Features.TaomSettings.LoadFrom(path);

        Assert.IsNotNull(settings);
        Assert.IsTrue(settings.WarOfTheRingEnabled);
    }

    [TestMethod]
    public void SaveTo_ThenLoadFrom_RoundTrips()
    {
        var path = Path.Combine(_tempDir, "roundtrip.json");
        var original = new TAOM.Features.TaomSettings
        {
            WarOfTheRingEnabled = false,
            Phase1TriggerDay = 99,
            Tier7Power = 4.44f,
            FastForwardMultiplier = 32,
            EnableSiegeDefenseEvents = false,
            ArmyBorderProximityFloor = 0.75f
        };

        TAOM.Features.TaomSettings.SaveTo(original, path);
        var loaded = TAOM.Features.TaomSettings.LoadFrom(path);

        Assert.AreEqual(original.WarOfTheRingEnabled, loaded.WarOfTheRingEnabled);
        Assert.AreEqual(original.Phase1TriggerDay, loaded.Phase1TriggerDay);
        Assert.AreEqual(original.Tier7Power, loaded.Tier7Power, 0.001f);
        Assert.AreEqual(original.FastForwardMultiplier, loaded.FastForwardMultiplier);
        Assert.AreEqual(original.EnableSiegeDefenseEvents, loaded.EnableSiegeDefenseEvents);
        Assert.AreEqual(original.ArmyBorderProximityFloor, loaded.ArmyBorderProximityFloor, 0.001f);
    }

    [TestMethod]
    public void AllDefaults_MatchExpectedValues()
    {
        var settings = new TAOM.Features.TaomSettings();

        Assert.IsTrue(settings.ShowAllEncyclopediaCharacters);
        Assert.IsTrue(settings.EnableTroopWeight);
        Assert.IsTrue(settings.WarOfTheRingEnabled);
        Assert.AreEqual(30, settings.Phase1TriggerDay);
        Assert.AreEqual(45, settings.Phase2TriggerDay);
        Assert.IsFalse(settings.TestMode);
        Assert.IsTrue(settings.EnableCustomTroopPower);
        Assert.IsFalse(settings.OverrideVanillaTierPower);
        Assert.AreEqual(2.91f, settings.Tier7Power, 0.001f);
        Assert.AreEqual(3.26f, settings.Tier8Power, 0.001f);
        Assert.AreEqual(3.61f, settings.Tier9Power, 0.001f);
        Assert.AreEqual(3.96f, settings.Tier10Power, 0.001f);
        Assert.AreEqual(1.5f, settings.HeroMultiplier, 0.001f);
        Assert.AreEqual(1.2f, settings.MountedMultiplier, 0.001f);
        Assert.IsTrue(settings.EnableCustomCasualtyRatios);
        Assert.AreEqual(0.30f, settings.PlayerBluntDamageChance, 0.001f);
        Assert.AreEqual(0.10f, settings.AIBluntDamageChance, 0.001f);
        Assert.IsTrue(settings.EnableCulturalSurvivalBonuses);
        Assert.IsTrue(settings.EnableSiegeDefenseEvents);
        Assert.AreEqual(3, settings.SiegeDefenseResponseDays);
        Assert.IsTrue(settings.EnableArmyStrategicIntelligence);
        Assert.AreEqual(4.0f, settings.ArmyCommitmentMultiplier, 0.001f);
        Assert.AreEqual(3.0f, settings.ArmyPriorityBoost, 0.001f);
        Assert.AreEqual(1.0f, settings.EvilFactionAggressionScale, 0.001f);
        Assert.AreEqual(1.0f, settings.LongRangePriorityBoostScale, 0.001f);
        Assert.AreEqual(0.15f, settings.ArmyBorderProximityFloor, 0.001f);
        Assert.AreEqual(4, settings.FastForwardMultiplier);
        Assert.AreEqual(8, settings.ExtraFastForwardMultiplier);
        Assert.AreEqual(16, settings.CtrlSpaceMultiplier);
    }
}
