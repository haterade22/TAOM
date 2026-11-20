using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Stamina;
using TAOM.Features.Stamina.Domain;

namespace TAOM.Tests.Features.Stamina;

[TestClass]
public class StaminaPeoplesTests
{
    private const float Delta = 0.0001f;
    private static readonly StaminaEconomy E = StaminaEconomy.Default;

    private static string RepoRoot => Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static StaminaPeoples Peoples(Dictionary<string, StaminaPeople>? races = null, Dictionary<string, StaminaPeople>? cultures = null) =>
        new StaminaPeoples(
            new Dictionary<string, StaminaPeople>(races ?? new Dictionary<string, StaminaPeople>(), StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, StaminaPeople>(cultures ?? new Dictionary<string, StaminaPeople>(), StringComparer.OrdinalIgnoreCase),
            0.7f, 1.6f, 10f, 60f, 1.35f);

    private static StaminaProfile Profile(StaminaPeoples peoples, string? race = null, string? culture = null, float armourKg = 0f,
        int tier = 3, bool hero = false, int heroLevel = 0) =>
        StaminaService.Profile(E, peoples, tier, hero, heroLevel, race, culture, armourKg);

    [TestMethod]
    public void Profile_NoPeopleRow_IsThePlainPoolWithNoFactor()
    {
        var profile = Profile(Peoples(), race: "human", culture: "vlandia");

        Assert.AreEqual(130f, profile.Pool, Delta);
        Assert.AreEqual(1f, profile.RegenFactor, Delta);
        Assert.AreEqual(1f, profile.CostFactor, Delta);
        Assert.IsFalse(profile.Exempt);
    }

    [TestMethod]
    public void Profile_Race_ScalesThePoolAndRegeneration()
    {
        var peoples = Peoples(races: new Dictionary<string, StaminaPeople> { ["elf"] = new StaminaPeople(1.3f, 1.4f, false) });

        var profile = Profile(peoples, race: "elf");

        Assert.AreEqual(130f * 1.3f, profile.Pool, Delta);
        Assert.AreEqual(1.4f, profile.RegenFactor, Delta);
    }

    [TestMethod]
    public void Profile_RaceNameInAnotherCase_StillMatches()
    {
        var peoples = Peoples(races: new Dictionary<string, StaminaPeople> { ["dwarf"] = new StaminaPeople(1.35f, 0.9f, false) });

        Assert.AreEqual(130f * 1.35f, Profile(peoples, race: "Dwarf").Pool, Delta);
    }

    [TestMethod]
    public void Profile_RaceAndCulture_Multiply()
    {
        var peoples = Peoples(
            races: new Dictionary<string, StaminaPeople> { ["orc"] = new StaminaPeople(0.9f, 1f, false) },
            cultures: new Dictionary<string, StaminaPeople> { ["gondor"] = new StaminaPeople(1.1f, 1.2f, false) });

        var profile = Profile(peoples, race: "orc", culture: "gondor");

        Assert.AreEqual(130f * 0.9f * 1.1f, profile.Pool, Delta);
        Assert.AreEqual(1.2f, profile.RegenFactor, Delta);
    }

    [TestMethod]
    public void Profile_ProductAboveTheMaximum_StopsAtTheMaximum()
    {
        var peoples = Peoples(
            races: new Dictionary<string, StaminaPeople> { ["cave_troll"] = new StaminaPeople(1.5f, 1.5f, false) },
            cultures: new Dictionary<string, StaminaPeople> { ["gondor"] = new StaminaPeople(1.5f, 1.5f, false) });

        var profile = Profile(peoples, race: "cave_troll", culture: "gondor");

        Assert.AreEqual(130f * 1.6f, profile.Pool, Delta);
        Assert.AreEqual(1.6f, profile.RegenFactor, Delta);
    }

    [TestMethod]
    public void Profile_ProductBelowTheMinimum_StopsAtTheMinimum()
    {
        var peoples = Peoples(
            races: new Dictionary<string, StaminaPeople> { ["goblin"] = new StaminaPeople(0.5f, 0.5f, false) },
            cultures: new Dictionary<string, StaminaPeople> { ["sturgia"] = new StaminaPeople(0.5f, 0.5f, false) });

        var profile = Profile(peoples, race: "goblin", culture: "sturgia");

        Assert.AreEqual(130f * 0.7f, profile.Pool, Delta);
        Assert.AreEqual(0.7f, profile.RegenFactor, Delta);
    }

    [TestMethod]
    public void Profile_ExemptRace_HasNoStamina()
    {
        var peoples = Peoples(races: new Dictionary<string, StaminaPeople> { ["nazghul"] = new StaminaPeople(1f, 1f, true) });

        Assert.IsTrue(Profile(peoples, race: "nazghul").Exempt);
        Assert.IsFalse(Profile(peoples, race: "human").Exempt);
    }

    [TestMethod]
    public void Profile_ExemptCulture_HasNoStamina()
    {
        var peoples = Peoples(cultures: new Dictionary<string, StaminaPeople> { ["wraiths"] = new StaminaPeople(1f, 1f, true) });

        Assert.IsTrue(Profile(peoples, race: "human", culture: "wraiths").Exempt);
    }

    [TestMethod]
    public void Profile_NoRaceAndNoCulture_IsThePlainPool()
    {
        // An agent with no character, or a race id the registry does not know: no name reaches the table.
        var peoples = Peoples(races: new Dictionary<string, StaminaPeople> { ["elf"] = new StaminaPeople(1.3f, 1.4f, false) });

        Assert.AreEqual(130f, Profile(peoples, race: null, culture: null).Pool, Delta);
        Assert.AreEqual(130f, Profile(peoples, race: "", culture: "").Pool, Delta);
    }

    [DataTestMethod]
    [DataRow(0f, 1f)]
    [DataRow(10f, 1f, DisplayName = "the free weight")]
    [DataRow(22f, 1.2f, DisplayName = "12 kg over the free weight")]
    [DataRow(31f, 1.35f, DisplayName = "the maximum is reached at 31 kg")]
    [DataRow(80f, 1.35f)]
    [DataRow(float.NaN, 1f)]
    [DataRow(float.PositiveInfinity, 1f)]
    [DataRow(-5f, 1f)]
    public void ArmourFactor_ByWeight(float kg, float expected)
    {
        Assert.AreEqual(expected, StaminaService.ArmourFactor(StaminaPeoples.None, kg), Delta);
        Assert.AreEqual(expected, Profile(StaminaPeoples.None, armourKg: kg).CostFactor, Delta);
    }

    [DataTestMethod]
    [DataRow(0, 200f, DisplayName = "a new companion")]
    [DataRow(25, 225f)]
    [DataRow(40, 240f)]
    [DataRow(62, 240f, DisplayName = "the level counts up to the cap")]
    [DataRow(-4, 200f)]
    public void MaxPool_HeroLevel_AddsToTheHeroPool(int level, float expected)
    {
        Assert.AreEqual(expected, StaminaService.MaxPool(E, tier: 7, hero: true, heroLevel: level), Delta);
    }

    [TestMethod]
    public void MaxPool_LevelOfASoldierWhoIsNoHero_AddsNothing()
    {
        Assert.AreEqual(150f, StaminaService.MaxPool(E, tier: 5, hero: false, heroLevel: 30), Delta);
    }

    // ── The file ────────────────────────────────────────────────────────────────────────────────

    private string _tempDir = null!;
    private IModLogger _logger = null!;
    private StaminaConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_StaminaPeoples_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "configs"));
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new StaminaConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) => File.WriteAllText(Path.Combine(_tempDir, "configs", "stamina.json"), json);

    [TestMethod]
    public void GetTuning_NoPeoplesSection_GivesNoModifierAndTheDefaultArmourRule()
    {
        WriteConfig("{}");

        var peoples = _sut.GetTuning().Peoples;

        Assert.AreEqual(0, peoples.Races.Count);
        Assert.AreEqual(0, peoples.Cultures.Count);
        Assert.AreEqual(StaminaPeoples.DefaultMultiplierMin, peoples.MultiplierMin, Delta);
        Assert.AreEqual(StaminaPeoples.DefaultMultiplierMax, peoples.MultiplierMax, Delta);
        Assert.AreEqual(StaminaPeoples.DefaultArmourFreeKg, peoples.ArmourFreeKg, Delta);
        Assert.AreEqual(StaminaPeoples.DefaultArmourKgPerStep, peoples.ArmourKgPerStep, Delta);
        Assert.AreEqual(StaminaPeoples.DefaultArmourFactorMax, peoples.ArmourFactorMax, Delta);
    }

    [TestMethod]
    public void GetTuning_PeoplesRows_AreRead_AndAMissingValueIsOne()
    {
        WriteConfig("{ \"peoples\": { \"races\": { \"elf\": { \"pool\": 1.3, \"regen\": 1.4 }, \"orc\": { \"pool\": 0.9 }, \"nazghul\": { \"exempt\": true } },"
                    + " \"cultures\": { \"gondor\": { \"pool\": 1.1 } }, \"multiplierMin\": 0.8, \"multiplierMax\": 1.5 },"
                    + " \"armour\": { \"freeKg\": 12, \"kgPerStep\": 40, \"factorMax\": 1.5 } }");

        var peoples = _sut.GetTuning().Peoples;

        Assert.AreEqual(1.3f, peoples.Races["elf"].Pool, Delta);
        Assert.AreEqual(1.4f, peoples.Races["elf"].Regen, Delta);
        Assert.AreEqual(0.9f, peoples.Races["ORC"].Pool, Delta, "a name is matched without regard to case");
        Assert.AreEqual(1f, peoples.Races["orc"].Regen, Delta);
        Assert.IsTrue(peoples.Races["nazghul"].Exempt);
        Assert.IsFalse(peoples.Races["elf"].Exempt);
        Assert.AreEqual(1.1f, peoples.Cultures["gondor"].Pool, Delta);
        Assert.AreEqual(0.8f, peoples.MultiplierMin, Delta);
        Assert.AreEqual(1.5f, peoples.MultiplierMax, Delta);
        Assert.AreEqual(12f, peoples.ArmourFreeKg, Delta);
        Assert.AreEqual(40f, peoples.ArmourKgPerStep, Delta);
        Assert.AreEqual(1.5f, peoples.ArmourFactorMax, Delta);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("pool", "0", DisplayName = "a pool multiplier of 0 would exhaust the people at once")]
    [DataRow("pool", "9")]
    [DataRow("pool", "NaN")]
    [DataRow("regen", "-1")]
    [DataRow("regen", "Infinity")]
    public void GetTuning_PeopleValueOutOfRange_ReadsAsOneAndWarns(string field, string value)
    {
        WriteConfig("{ \"peoples\": { \"races\": { \"elf\": { \"" + field + "\": " + value + " } } } }");

        var elf = _sut.GetTuning().Peoples.Races["elf"];

        Assert.AreEqual(1f, elf.Pool, Delta);
        Assert.AreEqual(1f, elf.Regen, Delta);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("peoples.races.elf." + field)));
    }

    [TestMethod]
    public void GetTuning_SameRaceTwiceInDifferentCase_KeepsTheFirstAndWarns()
    {
        WriteConfig("{ \"peoples\": { \"races\": { \"elf\": { \"pool\": 1.3 }, \"ELF\": { \"pool\": 0.8 } } } }");

        var peoples = _sut.GetTuning().Peoples;

        Assert.AreEqual(1, peoples.Races.Count);
        Assert.AreEqual(1.3f, peoples.Races["elf"].Pool, Delta);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("listed twice")));
    }

    [TestMethod]
    public void GetTuning_RowWithNoValues_IsSkippedAndWarns()
    {
        WriteConfig("{ \"peoples\": { \"cultures\": { \"gondor\": null, \"  \": { \"pool\": 1.2 } } } }");

        var peoples = _sut.GetTuning().Peoples;

        Assert.AreEqual(0, peoples.Cultures.Count);
        _logger.Received(2).LogWarning(Arg.Is<string>(m => m.Contains("no name or no values")));
    }

    [DataTestMethod]
    [DataRow("\"multiplierMin\": 1.4", "peoples.multiplierMin", DisplayName = "a minimum above 1 would raise every listed people")]
    [DataRow("\"multiplierMax\": 0.5", "peoples.multiplierMax", DisplayName = "a maximum below 1 would lower every listed people")]
    public void GetTuning_MultiplierLimitOnTheWrongSideOfOne_GivesItsDefault(string limit, string named)
    {
        WriteConfig("{ \"peoples\": { " + limit + " } }");

        var peoples = _sut.GetTuning().Peoples;

        Assert.AreEqual(StaminaPeoples.DefaultMultiplierMin, peoples.MultiplierMin, Delta);
        Assert.AreEqual(StaminaPeoples.DefaultMultiplierMax, peoples.MultiplierMax, Delta);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains(named)));
    }

    [DataTestMethod]
    [DataRow("freeKg", "-3")]
    [DataRow("kgPerStep", "0", DisplayName = "a step of 0 would divide by zero")]
    [DataRow("kgPerStep", "NaN")]
    [DataRow("factorMax", "0.5", DisplayName = "a maximum below 1 would make armour lower the costs")]
    public void GetTuning_ArmourValueOutOfRange_GivesItsDefaultAndWarns(string key, string value)
    {
        WriteConfig("{ \"armour\": { \"" + key + "\": " + value + " } }");

        var peoples = _sut.GetTuning().Peoples;

        Assert.AreEqual(StaminaPeoples.DefaultArmourFreeKg, peoples.ArmourFreeKg, Delta);
        Assert.AreEqual(StaminaPeoples.DefaultArmourKgPerStep, peoples.ArmourKgPerStep, Delta);
        Assert.AreEqual(StaminaPeoples.DefaultArmourFactorMax, peoples.ArmourFactorMax, Delta);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("armour." + key)));
    }

    [TestMethod]
    public void ShippedConfig_EveryRaceIsARaceOfTheMod_AndEveryCultureIsACultureId()
    {
        // The provider cannot ask the game's race registry at load, and a culture id is only a string: a mistyped
        // name would match nobody and the row would do nothing. The mod's own race list and id sheet are the check.
        var moduleData = Path.Combine(RepoRoot, @"Main\_Module\ModuleData");
        var shipped = JObject.Parse(File.ReadAllText(Path.Combine(moduleData, @"configs\stamina.json")));
        var races = ((JObject)JObject.Parse(File.ReadAllText(Path.Combine(moduleData, @"raceage\race_age_config.json")))["races"]!)
            .Properties().Select(p => p.Name).ToList();
        var idSheet = File.ReadAllText(Path.Combine(RepoRoot, @"docs\modding\id-cheatsheet.md"));

        var shippedRaces = ((JObject)shipped["peoples"]!["races"]!).Properties().Select(p => p.Name).ToList();
        var shippedCultures = ((JObject)shipped["peoples"]!["cultures"]!).Properties().Select(p => p.Name).ToList();

        Assert.IsTrue(shippedRaces.Count >= 10, "the race table is missing");
        foreach (var race in shippedRaces)
            CollectionAssert.Contains(races, race, "stamina.json race " + race + " is not in race_age_config.json");
        Assert.IsTrue(shippedCultures.Count >= 1, "the culture table is missing");
        foreach (var culture in shippedCultures)
            StringAssert.Contains(idSheet, "| `" + culture + "` |", "stamina.json culture " + culture + " is not a culture id in id-cheatsheet.md");
    }

    [TestMethod]
    public void ShippedConfig_PeoplesLoadWithoutAWarning_AndTheUndeadAreExempt()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(Path.Combine(RepoRoot, @"Main\_Module\ModuleData"));
        var logger = Substitute.For<IModLogger>();

        var peoples = new StaminaConfigProvider(pathService, logger).GetTuning().Peoples;

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        logger.DidNotReceive().LogError(Arg.Any<string>());
        Assert.IsTrue(peoples.Races["nazghul"].Exempt);
        Assert.IsTrue(peoples.Races["sauron"].Exempt);
        Assert.IsTrue(peoples.Races["saruman"].Exempt);
        Assert.IsFalse(peoples.Races["elf"].Exempt);
        // Every listed multiplier is inside the table's own limits, so none is silently clamped.
        foreach (var people in peoples.Races.Values.Concat(peoples.Cultures.Values))
        {
            Assert.IsTrue(people.Pool >= peoples.MultiplierMin && people.Pool <= peoples.MultiplierMax, "pool " + people.Pool);
            Assert.IsTrue(people.Regen >= peoples.MultiplierMin && people.Regen <= peoples.MultiplierMax, "regen " + people.Regen);
        }
    }
}
