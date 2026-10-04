using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.RaceAbilities;
using TAOM.Features.RaceAbilities.Domain;

// The loader for race_abilities.json: a missing or unparseable file gives the compiled profiles, and a
// parseable but invalid value reverts (or the bad trigger or profile is skipped) with a warning. One test
// per validation rule (csharp-architecture.md "Config Providers MUST Validate").

namespace TAOM.Tests.Features.RaceAbilities;

[TestClass]
public class RaceAbilitiesConfigProviderTests
{
    private string _tempDir = null!;
    private string _configDir = null!;
    private IModLogger _logger = null!;
    private RaceAbilitiesConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_RaceAbilities_" + Path.GetRandomFileName());
        _configDir = Path.Combine(_tempDir, "race_abilities");
        Directory.CreateDirectory(_configDir);

        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new RaceAbilitiesConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_configDir, "race_abilities.json"), json);

    // One race with one valid trigger; the test overrides the field it is about.
    private static string OneRace(string profileBody) =>
        "{ \"races\": { \"dwarf\": { " + profileBody + " } } }";

    private const string ValidCore =
        "\"abilityId\": \"stand_fast\", \"cooldownSeconds\": 30, \"durationSeconds\": 10, " +
        "\"anyOf\": [ { \"kind\": \"EnemyWithin\", \"range\": 5 } ]";

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsTheCompiledProfilesAndWarns()
    {
        var config = _sut.GetConfig();

        CollectionAssert.AreEquivalent(
            new[] { "berserker", "uruk_hai", "dwarf", "elf", "orc", "goblin", "uruk", "pale_uruk", "dg_uruk" }, config.Races.Keys);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MissingFile_ParsesTheDefaultTriggerKinds()
    {
        var berserk = _sut.GetConfig().Races["berserker"];

        Assert.AreEqual(RaceAbilityTriggerKind.EnemyWithin, berserk.Requires[0].ParsedKind);
        Assert.AreEqual(RaceAbilityTriggerKind.KinFell, berserk.AnyOf[2].ParsedKind);
    }

    [TestMethod]
    public void GetConfig_Unparseable_ReturnsTheCompiledProfilesAndLogsError()
    {
        WriteConfig("{ not json");

        var config = _sut.GetConfig();

        Assert.AreEqual(9, config.Races.Count);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_FileRacesReplaceTheDefaults()
    {
        WriteConfig(OneRace(ValidCore));

        var config = _sut.GetConfig();

        Assert.AreEqual(1, config.Races.Count);
        Assert.AreEqual("stand_fast", config.Races["dwarf"].AbilityId);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_TriggerKind_IsMatchedCaseInsensitively()
    {
        WriteConfig(OneRace("\"abilityId\": \"x\", \"cooldownSeconds\": 30, \"durationSeconds\": 10, " +
            "\"anyOf\": [ { \"kind\": \"cavalryclosing\", \"range\": 20 } ]"));

        var trigger = _sut.GetConfig().Races["dwarf"].AnyOf[0];

        Assert.AreEqual(RaceAbilityTriggerKind.CavalryClosing, trigger.ParsedKind);
    }

    [TestMethod]
    public void GetConfig_UnknownTriggerKind_IsSkippedWithAWarning()
    {
        WriteConfig(OneRace("\"abilityId\": \"x\", \"cooldownSeconds\": 30, \"durationSeconds\": 10, " +
            "\"anyOf\": [ { \"kind\": \"EnemyWithn\", \"range\": 5 }, { \"kind\": \"Always\" } ]"));

        var profile = _sut.GetConfig().Races["dwarf"];

        Assert.AreEqual(1, profile.AnyOf.Count);
        Assert.AreEqual(RaceAbilityTriggerKind.Always, profile.AnyOf[0].ParsedKind);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("EnemyWithn")));
    }

    [TestMethod]
    public void GetConfig_ProfileWithNoValidTrigger_IsSkippedWithAWarning()
    {
        WriteConfig(OneRace("\"abilityId\": \"x\", \"cooldownSeconds\": 30, \"durationSeconds\": 10, " +
            "\"anyOf\": [ { \"kind\": \"Nonsense\" } ]"));

        var config = _sut.GetConfig();

        Assert.IsFalse(config.Races.ContainsKey("dwarf"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("no valid trigger")));
    }

    [TestMethod]
    public void GetConfig_NullProfile_IsSkippedWithAWarning()
    {
        WriteConfig("{ \"races\": { \"dwarf\": null } }");

        Assert.AreEqual(0, _sut.GetConfig().Races.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("dwarf")));
    }

    [TestMethod]
    public void GetConfig_NullRaces_RevertsToTheCompiledProfiles()
    {
        WriteConfig("{ \"races\": null }");

        Assert.AreEqual(9, _sut.GetConfig().Races.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("races")));
    }

    [TestMethod]
    public void GetConfig_EmptyAbilityId_TakesTheRaceName()
    {
        WriteConfig(OneRace("\"cooldownSeconds\": 30, \"durationSeconds\": 10, \"anyOf\": [ { \"kind\": \"Always\" } ]"));

        Assert.AreEqual("dwarf", _sut.GetConfig().Races["dwarf"].AbilityId);
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("0")]
    [DataRow("121")]
    public void GetConfig_BadDuration_SkipsTheProfile(string duration)
    {
        WriteConfig(OneRace("\"abilityId\": \"x\", \"cooldownSeconds\": 30, \"durationSeconds\": " + duration +
            ", \"anyOf\": [ { \"kind\": \"Always\" } ]"));

        Assert.IsFalse(_sut.GetConfig().Races.ContainsKey("dwarf"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("durationSeconds")));
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("0.5")]
    [DataRow("601")]
    public void GetConfig_BadCooldown_SkipsTheProfile(string cooldown)
    {
        WriteConfig(OneRace("\"abilityId\": \"x\", \"cooldownSeconds\": " + cooldown +
            ", \"durationSeconds\": 10, \"anyOf\": [ { \"kind\": \"Always\" } ]"));

        Assert.IsFalse(_sut.GetConfig().Races.ContainsKey("dwarf"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("cooldownSeconds")));
    }

    [TestMethod]
    public void GetConfig_CooldownShorterThanTheLongestWindow_IsRaisedToIt()
    {
        // 8 s plus kills up to 14 s, then 3 s spent: it may not fire again inside 17 s.
        WriteConfig(OneRace("\"abilityId\": \"x\", \"cooldownSeconds\": 10, \"durationSeconds\": 8, " +
            "\"killExtensionSeconds\": 2, \"maxDurationSeconds\": 14, \"spentSeconds\": 3, " +
            "\"anyOf\": [ { \"kind\": \"Always\" } ]"));

        Assert.AreEqual(17f, _sut.GetConfig().Races["dwarf"].CooldownSeconds, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("cooldownSeconds")));
    }

    [TestMethod]
    public void GetConfig_MaxDurationZero_IsTheDuration()
    {
        WriteConfig(OneRace(ValidCore));

        Assert.AreEqual(10f, _sut.GetConfig().Races["dwarf"].MaxDurationSeconds, 0.0001f);
    }

    [TestMethod]
    public void GetConfig_MaxDurationBelowTheDuration_IsRaisedToIt()
    {
        WriteConfig(OneRace(ValidCore + ", \"maxDurationSeconds\": 4"));

        Assert.AreEqual(10f, _sut.GetConfig().Races["dwarf"].MaxDurationSeconds, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("maxDurationSeconds")));
    }

    [DataTestMethod]
    [DataRow("spentSeconds", "-1", 0f)]
    [DataRow("spentSeconds", "NaN", 0f)]
    [DataRow("killExtensionSeconds", "31", 0f)]
    [DataRow("rallyRadius", "Infinity", 0f)]
    [DataRow("rallyRadius", "31", 0f)]
    public void GetConfig_BadOptionalTiming_RevertsToZero(string field, string value, float expected)
    {
        WriteConfig(OneRace(ValidCore + ", \"" + field + "\": " + value));

        var profile = _sut.GetConfig().Races["dwarf"];
        var actual = field switch
        {
            "spentSeconds" => profile.SpentSeconds,
            "killExtensionSeconds" => profile.KillExtensionSeconds,
            _ => profile.RallyRadius,
        };
        Assert.AreEqual(expected, actual, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(field)));
    }

    [TestMethod]
    public void GetConfig_UnknownWarCry_IsSilencedWithAWarning()
    {
        WriteConfig(OneRace(ValidCore + ", \"warCry\": \"Scream\""));

        Assert.AreEqual("", _sut.GetConfig().Races["dwarf"].WarCry);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("warCry")));
    }

    [TestMethod]
    public void GetConfig_WarCry_IsCanonicalised()
    {
        WriteConfig(OneRace(ValidCore + ", \"warCry\": \"yell\""));

        Assert.AreEqual("Yell", _sut.GetConfig().Races["dwarf"].WarCry);
    }

    // Each kind validates only the parameters it reads, so each case names a kind that reads its field.
    [DataTestMethod]
    [DataRow("{ \"kind\": \"EnemiesWithin\", \"range\": 0, \"count\": 3 }", "range")]
    [DataRow("{ \"kind\": \"EnemiesWithin\", \"range\": 41, \"count\": 3 }", "range")]
    [DataRow("{ \"kind\": \"EnemyWithin\", \"range\": NaN }", "range")]
    [DataRow("{ \"kind\": \"CavalryClosing\", \"range\": -1 }", "range")]
    [DataRow("{ \"kind\": \"KinFell\", \"range\": 10, \"seconds\": 31 }", "seconds")]
    [DataRow("{ \"kind\": \"LandedKill\", \"seconds\": 0 }", "seconds")]
    [DataRow("{ \"kind\": \"HealthBelow\", \"fraction\": 1.5 }", "fraction")]
    [DataRow("{ \"kind\": \"WoundedEnemyWithin\", \"range\": 3, \"fraction\": 0 }", "fraction")]
    [DataRow("{ \"kind\": \"EnemiesWithin\", \"range\": 5, \"count\": 0 }", "count")]
    [DataRow("{ \"kind\": \"KinWithin\", \"range\": 5, \"count\": 51 }", "count")]
    public void GetConfig_BadTriggerParameter_SkipsTheTrigger(string triggerJson, string field)
    {
        WriteConfig(OneRace("\"abilityId\": \"x\", \"cooldownSeconds\": 30, \"durationSeconds\": 10, " +
            "\"anyOf\": [ " + triggerJson + ", { \"kind\": \"Always\" } ]"));

        var profile = _sut.GetConfig().Races["dwarf"];

        Assert.AreEqual(1, profile.AnyOf.Count);
        Assert.AreEqual(RaceAbilityTriggerKind.Always, profile.AnyOf[0].ParsedKind);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(field)));
    }

    [TestMethod]
    public void GetConfig_ParameterAKindDoesNotRead_IsIgnored()
    {
        // TookDamage reads nothing, so a stray range is not a reason to drop it.
        WriteConfig(OneRace("\"abilityId\": \"x\", \"cooldownSeconds\": 30, \"durationSeconds\": 10, " +
            "\"anyOf\": [ { \"kind\": \"TookDamage\", \"range\": 999 } ]"));

        Assert.AreEqual(1, _sut.GetConfig().Races["dwarf"].AnyOf.Count);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_NullTrigger_IsSkipped()
    {
        WriteConfig(OneRace("\"abilityId\": \"x\", \"cooldownSeconds\": 30, \"durationSeconds\": 10, " +
            "\"requires\": [ null ], \"anyOf\": [ { \"kind\": \"Always\" } ]"));

        Assert.AreEqual(0, _sut.GetConfig().Races["dwarf"].Requires.Count);
    }

    // One bad value for every numeric effect field (a second for the fields with two bounds worth pinning).
    // BadEffectValues_CoverEveryNumericEffect fails when a new field arrives without a row.
    public static IEnumerable<object[]> BadEffectValues => new[]
    {
        new object[] { "moveSpeedPercent", "NaN" },
        new object[] { "moveSpeedPercent", "-96" },
        new object[] { "accelerationPercent", "301" },
        new object[] { "swingSpeedPercent", "301" },
        new object[] { "drawSpeedPercent", "-96" },
        new object[] { "reloadSpeedPercent", "Infinity" },
        new object[] { "missileSpeedPercent", "301" },
        new object[] { "mountSpeedPercent", "301" },
        new object[] { "meleeDamagePercent", "Infinity" },
        new object[] { "rangedDamagePercent", "-96" },
        new object[] { "damageReductionPercent", "-1" },
        new object[] { "damageReductionPercent", "91" },
        new object[] { "knockdownResistancePercent", "1001" },
        new object[] { "knockbackResistancePercent", "-96" },
        new object[] { "dismountResistancePercent", "1001" },
        new object[] { "blockAbilityPercent", "-96" },
        new object[] { "parryAbilityPercent", "301" },
        new object[] { "attackEagernessPercent", "NaN" },
        new object[] { "aimErrorPercent", "-96" },
        new object[] { "moraleFloor", "101" },
        new object[] { "moraleOnEnd", "-101" },
        new object[] { "moraleOnEnd", "NaN" },
        new object[] { "healPerKill", "-1" },
        new object[] { "fearOnKillRadius", "31" },
        new object[] { "fearOnKillMorale", "101" },
        new object[] { "fearAuraRadius", "31" },
        new object[] { "fearAuraMoralePerSecond", "51" },
    };

    [DataTestMethod]
    [DynamicData(nameof(BadEffectValues))]
    public void GetConfig_BadEffect_RevertsToNoEffect(string field, string value)
    {
        WriteConfig(OneRace(ValidCore + ", \"effects\": { \"" + field + "\": " + value + " }"));

        var effects = _sut.GetConfig().Races["dwarf"].Effects;

        Assert.AreEqual(0f, EffectValue(effects, field), 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(field)));
    }

    [TestMethod]
    public void BadEffectValues_CoverEveryNumericEffect()
    {
        var covered = new HashSet<string>(BadEffectValues.Select(row => (string)row[0]));
        var missing = typeof(RaceAbilityEffects).GetProperties()
            .Where(p => p.PropertyType == typeof(float))
            .Select(p => char.ToLowerInvariant(p.Name[0]) + p.Name.Substring(1))
            .Where(field => !covered.Contains(field))
            .ToList();

        Assert.AreEqual(0, missing.Count, "numeric effect fields with no bad-value row: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void GetConfig_BadSpentEffect_RevertsToNoEffect()
    {
        WriteConfig(OneRace(ValidCore + ", \"spentSeconds\": 3, \"spent\": { \"moveSpeedPercent\": -200 }"));

        Assert.AreEqual(0f, _sut.GetConfig().Races["dwarf"].Spent.MoveSpeedPercent, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("spent.moveSpeedPercent")));
    }

    [TestMethod]
    public void GetConfig_NullEffects_AreNoEffect()
    {
        WriteConfig(OneRace(ValidCore + ", \"effects\": null, \"spent\": null"));

        var profile = _sut.GetConfig().Races["dwarf"];

        Assert.IsNotNull(profile.Effects);
        Assert.IsNotNull(profile.Spent);
        Assert.IsFalse(profile.Effects.ForceCrushThrough);
    }

    [TestMethod]
    public void GetConfig_ValidEffects_PassThrough()
    {
        WriteConfig(OneRace(ValidCore + ", \"effects\": { \"meleeDamagePercent\": 20, \"blockAbilityPercent\": -60, " +
            "\"forceCrushThrough\": true, \"moraleFloor\": 30 }"));

        var effects = _sut.GetConfig().Races["dwarf"].Effects;

        Assert.AreEqual(20f, effects.MeleeDamagePercent, 0.0001f);
        Assert.AreEqual(-60f, effects.BlockAbilityPercent, 0.0001f);
        Assert.IsTrue(effects.ForceCrushThrough);
        Assert.AreEqual(30f, effects.MoraleFloor, 0.0001f);
    }

    [DataTestMethod]
    [DataRow("baseTier", "-1")]
    [DataRow("baseTier", "11")]
    [DataRow("percentPerTier", "NaN")]
    [DataRow("percentPerTier", "51")]
    [DataRow("minFactor", "0")]
    [DataRow("maxFactor", "0.9")]
    [DataRow("heroFactor", "0")]
    [DataRow("heroFactor", "3.1")]
    public void GetConfig_BadTierScaling_RevertsTheField(string field, string value)
    {
        WriteConfig("{ \"tierScaling\": { \"" + field + "\": " + value + " } }");

        var scaling = _sut.GetConfig().TierScaling;
        var defaults = new RaceAbilityTierScaling();

        Assert.AreEqual(defaults.BaseTier, scaling.BaseTier);
        Assert.AreEqual(defaults.PercentPerTier, scaling.PercentPerTier, 0.0001f);
        Assert.AreEqual(defaults.MinFactor, scaling.MinFactor, 0.0001f);
        Assert.AreEqual(defaults.MaxFactor, scaling.MaxFactor, 0.0001f);
        Assert.AreEqual(defaults.HeroFactor, scaling.HeroFactor, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(field)));
    }

    [TestMethod]
    public void GetConfig_NullTierScaling_RevertsToDefaults()
    {
        WriteConfig("{ \"tierScaling\": null }");

        Assert.AreEqual(new RaceAbilityTierScaling().HeroFactor, _sut.GetConfig().TierScaling.HeroFactor, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("tierScaling")));
    }

    [TestMethod]
    public void GetConfig_AnyRejection_EmitsOneSummaryWarning()
    {
        WriteConfig(OneRace(ValidCore + ", \"warCry\": \"Scream\""));

        _sut.GetConfig();

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("reverted")));
    }

    [TestMethod]
    public void GetConfig_IsLoadedOnce()
    {
        WriteConfig(OneRace(ValidCore));

        var first = _sut.GetConfig();
        WriteConfig("{ not json");

        Assert.AreSame(first, _sut.GetConfig());
    }

    // The JSON field's property, by name: a field with no property fails the test instead of reading another.
    private static float EffectValue(RaceAbilityEffects effects, string field)
    {
        var property = typeof(RaceAbilityEffects).GetProperty(char.ToUpperInvariant(field[0]) + field.Substring(1));
        Assert.IsNotNull(property, $"RaceAbilityEffects has no property for '{field}'");
        return (float)property!.GetValue(effects)!;
    }

    // --- cultures, aliases, kin ---

    [TestMethod]
    public void GetConfig_MissingFile_HasTheCultureProfiles()
    {
        var cultures = _sut.GetConfig().Cultures;

        Assert.AreEqual("forth_eorlingas", cultures["vlandia"].AbilityId);
        Assert.AreEqual("citadel_guard", cultures["gondor"].AbilityId);
    }

    [TestMethod]
    public void GetConfig_CommaSeparatedKey_SharesOneProfileObject()
    {
        WriteConfig("{ \"cultures\": { \"aserai, harad_raiders ,shaghana\": { " + ValidCore + " } } }");

        var cultures = _sut.GetConfig().Cultures;

        Assert.AreEqual(3, cultures.Count);
        Assert.AreSame(cultures["aserai"], cultures["harad_raiders"]);
        Assert.AreSame(cultures["aserai"], cultures["shaghana"]);
    }

    [TestMethod]
    public void GetConfig_EmptyKeyPart_IsIgnored()
    {
        WriteConfig("{ \"cultures\": { \"gondor,,\": { " + ValidCore + " } } }");

        Assert.AreEqual(1, _sut.GetConfig().Cultures.Count);
    }

    [TestMethod]
    public void GetConfig_AnIdNamedTwice_TheLaterEntryWinsWithAWarning()
    {
        WriteConfig("{ \"cultures\": { \"gondor\": { " + ValidCore + " }, \"gondor,rohan\": { " +
            "\"abilityId\": \"second\", \"cooldownSeconds\": 30, \"durationSeconds\": 10, \"anyOf\": [ { \"kind\": \"Always\" } ] } } }");

        Assert.AreEqual("second", _sut.GetConfig().Cultures["gondor"].AbilityId);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("'gondor'") && s.Contains("more than one")));
    }

    [TestMethod]
    public void GetConfig_NullCultures_RevertsToTheCompiledProfiles()
    {
        WriteConfig("{ \"cultures\": null }");

        Assert.IsTrue(_sut.GetConfig().Cultures.ContainsKey("vlandia"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("cultures")));
    }

    [TestMethod]
    public void GetConfig_KinRaces_AreCleanedAndSplit()
    {
        WriteConfig(OneRace(ValidCore + ", \"kinRaces\": [ \" goblin \", \"\", \"orc,goblin\" ]"));

        CollectionAssert.AreEqual(new[] { "goblin", "orc" }, _sut.GetConfig().Races["dwarf"].KinRaces);
    }

    [TestMethod]
    public void GetConfig_ValidKinBonus_PassesThrough()
    {
        WriteConfig(OneRace(ValidCore + ", \"kinBonus\": { \"radius\": 6, \"perKinPercent\": 3, \"maxKin\": 5 }"));

        var bonus = _sut.GetConfig().Races["dwarf"].KinBonus!;
        Assert.AreEqual(6f, bonus.Radius, 0.0001f);
        Assert.AreEqual(5, bonus.MaxKin);
    }

    [DataTestMethod]
    [DataRow("{ \"radius\": 0, \"perKinPercent\": 3, \"maxKin\": 5 }")]
    [DataRow("{ \"radius\": 31, \"perKinPercent\": 3, \"maxKin\": 5 }")]
    [DataRow("{ \"radius\": 6, \"perKinPercent\": NaN, \"maxKin\": 5 }")]
    [DataRow("{ \"radius\": 6, \"perKinPercent\": 51, \"maxKin\": 5 }")]
    [DataRow("{ \"radius\": 6, \"perKinPercent\": 3, \"maxKin\": 0 }")]
    public void GetConfig_BadKinBonus_IsDroppedWithAWarning(string bonusJson)
    {
        WriteConfig(OneRace(ValidCore + ", \"kinBonus\": " + bonusJson));

        Assert.IsNull(_sut.GetConfig().Races["dwarf"].KinBonus);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("kinBonus")));
    }

    // --- new trigger kinds and effects ---

    [DataTestMethod]
    [DataRow("{ \"kind\": \"MoraleBelow\", \"fraction\": 0 }", "fraction")]
    [DataRow("{ \"kind\": \"MoraleBelow\", \"fraction\": 1.1 }", "fraction")]
    [DataRow("{ \"kind\": \"NoEnemyWithin\", \"range\": 0 }", "range")]
    public void GetConfig_BadNewTriggerParameter_SkipsTheTrigger(string triggerJson, string field)
    {
        WriteConfig(OneRace("\"abilityId\": \"x\", \"cooldownSeconds\": 30, \"durationSeconds\": 10, " +
            "\"anyOf\": [ " + triggerJson + ", { \"kind\": \"Always\" } ]"));

        Assert.AreEqual(1, _sut.GetConfig().Races["dwarf"].AnyOf.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(field)));
    }

    [TestMethod]
    public void GetConfig_Mounted_ReadsNoParameter()
    {
        WriteConfig(OneRace("\"abilityId\": \"x\", \"cooldownSeconds\": 30, \"durationSeconds\": 10, " +
            "\"requires\": [ { \"kind\": \"Mounted\", \"range\": 999 } ]"));

        Assert.AreEqual(RaceAbilityTriggerKind.Mounted, _sut.GetConfig().Races["dwarf"].Requires[0].ParsedKind);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    // --- trigger kinds are names, not numbers or lists ---

    [DataTestMethod]
    [DataRow("EnemyWithin,CavalryClosing")]   // Enum.TryParse would OR these into RangedTargetWithin
    [DataRow("1")]
    [DataRow("RangedTargetWithin, Mounted")]
    [DataRow("")]
    public void GetConfig_KindThatIsNotOneName_IsSkippedWithAWarning(string kind)
    {
        WriteConfig(OneRace("\"abilityId\": \"x\", \"cooldownSeconds\": 30, \"durationSeconds\": 10, " +
            "\"anyOf\": [ { \"kind\": \"" + kind + "\", \"range\": 5 }, { \"kind\": \"Always\" } ]"));

        var profile = _sut.GetConfig().Races["dwarf"];

        Assert.AreEqual(1, profile.AnyOf.Count);
        Assert.AreEqual(RaceAbilityTriggerKind.Always, profile.AnyOf[0].ParsedKind);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not a known trigger kind")));
    }

    [TestMethod]
    public void GetConfig_KindWithSpaces_IsMatched()
    {
        WriteConfig(OneRace("\"abilityId\": \"x\", \"cooldownSeconds\": 30, \"durationSeconds\": 10, " +
            "\"anyOf\": [ { \"kind\": \" EnemyWithin \", \"range\": 5 } ]"));

        Assert.AreEqual(RaceAbilityTriggerKind.EnemyWithin, _sut.GetConfig().Races["dwarf"].AnyOf[0].ParsedKind);
    }

    [TestMethod]
    public void GetConfig_KeyOfOnlyCommas_IsSkippedWithAWarning()
    {
        WriteConfig("{ \"cultures\": { \" , \": { " + ValidCore + " } } }");

        Assert.AreEqual(0, _sut.GetConfig().Cultures.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("empty name")));
    }

    [DataTestMethod]
    [DataRow("{ \"kind\": \"WoundedEnemyWithin\", \"range\": 41, \"fraction\": 0.5 }", "range")]
    [DataRow("{ \"kind\": \"RangedTargetWithin\", \"range\": 0 }", "range")]
    [DataRow("{ \"kind\": \"KinWithin\", \"range\": NaN, \"count\": 3 }", "range")]
    [DataRow("{ \"kind\": \"KinFell\", \"range\": 41, \"seconds\": 5 }", "range")]
    public void GetConfig_BadRangeOnEveryRangedKind_SkipsTheTrigger(string triggerJson, string field)
    {
        WriteConfig(OneRace("\"abilityId\": \"x\", \"cooldownSeconds\": 30, \"durationSeconds\": 10, " +
            "\"anyOf\": [ " + triggerJson + ", { \"kind\": \"Always\" } ]"));

        Assert.AreEqual(1, _sut.GetConfig().Races["dwarf"].AnyOf.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(field)));
    }

    // --- accepted but never read ---

    [TestMethod]
    public void GetConfig_KinRacesNothingReads_Warns()
    {
        WriteConfig(OneRace(ValidCore + ", \"kinRaces\": [ \"orc\" ]"));

        _sut.GetConfig();

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("kinRaces is set but")));
    }

    [DataTestMethod]
    [DataRow("\"requires\": [ { \"kind\": \"KinWithin\", \"range\": 6, \"count\": 3 } ]")]
    [DataRow("\"requires\": [ { \"kind\": \"KinFell\", \"range\": 6, \"seconds\": 5 } ]")]
    [DataRow("\"anyOf\": [ { \"kind\": \"Always\" } ], \"kinBonus\": { \"radius\": 6, \"perKinPercent\": 3, \"maxKin\": 5 }")]
    public void GetConfig_KinRacesSomethingReads_DoesNotWarn(string reader)
    {
        WriteConfig(OneRace("\"abilityId\": \"x\", \"cooldownSeconds\": 30, \"durationSeconds\": 10, \"kinRaces\": [ \"orc\" ], " + reader));

        _sut.GetConfig();

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_KinBonusOfZeroPercent_Warns()
    {
        WriteConfig(OneRace(ValidCore + ", \"kinBonus\": { \"radius\": 6, \"perKinPercent\": 0, \"maxKin\": 5 }"));

        _sut.GetConfig();

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("perKinPercent is 0")));
    }

    [TestMethod]
    public void GetConfig_SpentMoraleOnEnd_Warns()
    {
        WriteConfig(OneRace(ValidCore + ", \"spentSeconds\": 3, \"spent\": { \"moraleOnEnd\": -5 }"));

        _sut.GetConfig();

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("spent.moraleOnEnd is never read")));
    }

    [DataTestMethod]
    [DataRow("effects", "\"fearOnKillRadius\": 6", "fearOnKillRadius and fearOnKillMorale")]
    [DataRow("effects", "\"fearOnKillMorale\": 4", "fearOnKillRadius and fearOnKillMorale")]
    [DataRow("effects", "\"fearAuraRadius\": 8", "fearAuraRadius and fearAuraMoralePerSecond")]
    [DataRow("spent", "\"fearAuraMoralePerSecond\": 2", "fearAuraRadius and fearAuraMoralePerSecond")]
    public void GetConfig_HalfAFearPair_Warns(string block, string field, string message)
    {
        WriteConfig(OneRace(ValidCore + ", \"spentSeconds\": 3, \"" + block + "\": { " + field + " }"));

        _sut.GetConfig();

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(block + ": " + message)));
    }

    [TestMethod]
    public void GetConfig_WholeFearPairs_DoNotWarn()
    {
        WriteConfig(OneRace(ValidCore + ", \"effects\": { \"fearOnKillRadius\": 6, \"fearOnKillMorale\": 4, " +
            "\"fearAuraRadius\": 8, \"fearAuraMoralePerSecond\": 2 }"));

        _sut.GetConfig();

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_KillExtensionWithoutRoomToExtend_Warns()
    {
        WriteConfig(OneRace(ValidCore + ", \"killExtensionSeconds\": 2"));

        _sut.GetConfig();

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("kills cannot extend")));
    }

    [TestMethod]
    public void GetConfig_SpentEffectsWithoutASpentPhase_Warns()
    {
        WriteConfig(OneRace(ValidCore + ", \"spent\": { \"moveSpeedPercent\": -10 }"));

        _sut.GetConfig();

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("never apply")));
    }

    [TestMethod]
    public void GetConfig_CompiledProfiles_RaiseNoWarningBeyondTheMissingFile()
    {
        _sut.GetConfig();

        _logger.Received(1).LogWarning(Arg.Any<string>());
    }
}
