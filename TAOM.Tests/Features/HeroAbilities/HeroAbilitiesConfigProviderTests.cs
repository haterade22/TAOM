using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.HeroAbilities;
using TAOM.Features.HeroAbilities.Domain;

namespace TAOM.Tests.Features.HeroAbilities;

[TestClass]
public class HeroAbilitiesConfigProviderTests
{
    private const string Trigger = "{\"kind\":\"EnemiesWithin\",\"range\":10,\"count\":3}";

    private string _tempDir = null!;
    private string _featureDir = null!;
    private IModLogger _logger = null!;
    private HeroAbilitiesConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_HeroAbilities_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "hero_abilities");
        Directory.CreateDirectory(_featureDir);
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new HeroAbilitiesConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) => File.WriteAllText(Path.Combine(_featureDir, "hero_abilities.json"), json);

    // One kit with one ability; extra holds raw JSON members appended to the ability.
    private static string Ability(string id = "a_one", string extra = "", string trigger = Trigger, string cooldown = "90", string duration = "12") =>
        "{\"id\":\"" + id + "\",\"cooldownSeconds\":" + cooldown + ",\"durationSeconds\":" + duration
        + ",\"anyOf\":[" + trigger + "]" + (extra.Length > 0 ? "," + extra : "") + "}";

    private static string Kit(string characterId, string abilities, string extra = "") =>
        "{\"characterId\":\"" + characterId + "\"" + (extra.Length > 0 ? "," + extra : "") + ",\"abilities\":[" + abilities + "]}";

    private HeroAbilitiesConfig LoadKits(params string[] kits)
    {
        WriteConfig("{\"heroes\":[" + string.Join(",", kits) + "]}");
        return _sut.GetConfig();
    }

    private HeroAbilityConfig LoadOne(string extra = "", string trigger = Trigger, string cooldown = "90", string duration = "12") =>
        LoadKits(Kit("hero_x", Ability(extra: extra, trigger: trigger, cooldown: cooldown, duration: duration))).Heroes.Single().Abilities.Single();

    private void AssertWarned(string text) =>
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains(text)));

    [TestMethod]
    public void Load_MissingFile_NoKitsAndWarning()
    {
        var config = _sut.GetConfig();

        Assert.AreEqual(0, config.Heroes.Count);
        AssertWarned("not found");
    }

    [TestMethod]
    public void Load_ParseFailure_NoKitsAndError()
    {
        WriteConfig("{ not json");

        Assert.AreEqual(0, _sut.GetConfig().Heroes.Count);
        _logger.Received().LogError(Arg.Is<string>(m => m.Contains("failed to parse")));
    }

    [TestMethod]
    public void Load_Valid_KeepsEveryField()
    {
        var config = LoadKits(Kit("hero_x",
            Ability(extra: "\"radius\":12,\"allyEffects\":{\"meleeDamagePercent\":15},\"selfEffects\":{\"shrugOffBlows\":true},"
                + "\"allyMorale\":25,\"enemyShockRadius\":10,\"enemyShockMorale\":10,\"warCry\":\"charge\""),
            "\"glow\":\"#e8d27a\",\"passive\":{\"knockdownResistancePercent\":1000}"));

        var kit = config.Heroes.Single();
        var ability = kit.Abilities.Single();
        Assert.IsTrue(config.Enabled);
        Assert.AreEqual(0xFFE8D27Au, kit.GlowColor);
        Assert.AreEqual(1000f, kit.Passive.KnockdownResistancePercent);
        Assert.AreEqual(12f, ability.Radius);
        Assert.AreEqual(15f, ability.AllyEffects.MeleeDamagePercent);
        Assert.IsTrue(ability.SelfEffects.ShrugOffBlows);
        Assert.AreEqual(25f, ability.AllyMorale);
        Assert.AreEqual(10f, ability.EnemyShockRadius);
        Assert.AreEqual(10f, ability.EnemyShockMorale);
        Assert.AreEqual("Charge", ability.WarCry);
        Assert.AreEqual(1, ability.AnyOf.Count);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Load_EnabledFalse_Kept()
    {
        WriteConfig("{\"enabled\":false,\"heroes\":[]}");

        Assert.IsFalse(_sut.GetConfig().Enabled);
    }

    [DataTestMethod]
    [DataRow("59")]
    [DataRow("121")]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    public void Load_CooldownOutside60To120_DropsTheAbility(string cooldown)
    {
        var config = LoadKits(Kit("hero_x", Ability(cooldown: cooldown, duration: "10")));

        Assert.AreEqual(0, config.Heroes.Single().Abilities.Count);
        AssertWarned("cooldownSeconds");
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("91")]
    [DataRow("NaN")]
    public void Load_DurationOutsideZeroToCooldown_DropsTheAbility(string duration)
    {
        var config = LoadKits(Kit("hero_x", Ability(duration: duration)));

        Assert.AreEqual(0, config.Heroes.Single().Abilities.Count);
        AssertWarned("durationSeconds");
    }

    [TestMethod]
    public void Load_DurationEqualToCooldown_Kept() =>
        Assert.AreEqual(90f, LoadOne(duration: "90").DurationSeconds);

    [TestMethod]
    public void Load_NoTriggerLeft_DropsTheAbility()
    {
        var config = LoadKits(Kit("hero_x", Ability(trigger: "{\"kind\":\"Nope\"}")));

        Assert.AreEqual(0, config.Heroes.Single().Abilities.Count);
        AssertWarned("no valid trigger");
    }

    [TestMethod]
    public void Load_BadTriggerRange_SkipsOnlyThatTrigger()
    {
        var config = LoadKits(Kit("hero_x", Ability(trigger: Trigger + ",{\"kind\":\"EnemyWithin\",\"range\":99}")));

        Assert.AreEqual(1, config.Heroes.Single().Abilities.Single().AnyOf.Count);
        AssertWarned("range=99");
    }

    [DataTestMethod]
    [DataRow("{\"kind\":\"KinWithin\",\"range\":5,\"count\":2}")]
    [DataRow("{\"kind\":\"KinFell\",\"range\":5,\"seconds\":5}")]
    [DataRow("{\"kind\":\"LandedKill\",\"seconds\":5}")]
    public void Load_KinTriggers_Skipped(string kin)
    {
        var ability = LoadOne(trigger: Trigger + "," + kin);

        Assert.AreEqual(1, ability.AnyOf.Count);
        AssertWarned("kin");
    }

    [DataTestMethod]
    [DataRow("\"radius\":16", "radius")]
    [DataRow("\"radius\":-1", "radius")]
    [DataRow("\"enemyShockRadius\":16", "enemyShockRadius")]
    [DataRow("\"enemyShockRadius\":NaN", "enemyShockRadius")]
    public void Load_RadiusOutside0To15_RevertsToZeroWithWarning(string extra, string name)
    {
        var ability = LoadOne(extra: extra);

        Assert.AreEqual(0f, ability.Radius);
        Assert.AreEqual(0f, ability.EnemyShockRadius);
        AssertWarned(name);
    }

    [DataTestMethod]
    [DataRow("\"allyMorale\":51", "allyMorale")]
    [DataRow("\"allyMorale\":-1", "allyMorale")]
    [DataRow("\"enemyShockMorale\":51", "enemyShockMorale")]
    [DataRow("\"enemyShockMorale\":NaN", "enemyShockMorale")]
    public void Load_MoraleOutside0To50_RevertsToZeroWithWarning(string extra, string name)
    {
        var ability = LoadOne(extra: extra);

        Assert.AreEqual(0f, ability.AllyMorale);
        Assert.AreEqual(0f, ability.EnemyShockMorale);
        AssertWarned(name);
    }

    [TestMethod]
    public void Load_RadiusAndMoraleAt15And50_Kept()
    {
        var ability = LoadOne(extra: "\"radius\":15,\"enemyShockRadius\":15,\"allyMorale\":50,\"enemyShockMorale\":50");

        Assert.AreEqual(15f, ability.Radius);
        Assert.AreEqual(15f, ability.EnemyShockRadius);
        Assert.AreEqual(50f, ability.AllyMorale);
        Assert.AreEqual(50f, ability.EnemyShockMorale);
    }

    [TestMethod]
    public void Load_HarmfulEffect_ZeroedWithWarning()
    {
        var ability = LoadOne(extra: "\"allyEffects\":{\"meleeDamagePercent\":-20,\"swingSpeedPercent\":10}");

        Assert.AreEqual(0f, ability.AllyEffects.MeleeDamagePercent);
        Assert.AreEqual(10f, ability.AllyEffects.SwingSpeedPercent);
        AssertWarned("meleeDamagePercent");
    }

    [TestMethod]
    public void Load_AbilityOnlyEffect_ZeroedWithWarning()
    {
        var ability = LoadOne(extra: "\"selfEffects\":{\"healPerKill\":5}");

        Assert.AreEqual(0f, ability.SelfEffects.HealPerKill);
        AssertWarned("healPerKill");
    }

    [TestMethod]
    public void Load_DamageReductionAbove90_ZeroedWithWarning()
    {
        var ability = LoadOne(extra: "\"selfEffects\":{\"damageReductionPercent\":91}");

        Assert.AreEqual(0f, ability.SelfEffects.DamageReductionPercent);
        AssertWarned("damageReductionPercent");
        _sut = NewProvider();
        Assert.AreEqual(90f, LoadOne(extra: "\"selfEffects\":{\"damageReductionPercent\":90}").SelfEffects.DamageReductionPercent);
    }

    [TestMethod]
    public void Load_ResistanceAbove1000_ZeroedWithWarning()
    {
        var ability = LoadOne(extra: "\"allyEffects\":{\"knockdownResistancePercent\":1001,\"knockbackResistancePercent\":1000}");

        Assert.AreEqual(0f, ability.AllyEffects.KnockdownResistancePercent);
        Assert.AreEqual(1000f, ability.AllyEffects.KnockbackResistancePercent);
        AssertWarned("knockdownResistancePercent");
    }

    [TestMethod]
    public void Load_OtherPercentAbove300_ZeroedWithWarning()
    {
        var ability = LoadOne(extra: "\"selfEffects\":{\"meleeDamagePercent\":301,\"swingSpeedPercent\":300}");

        Assert.AreEqual(0f, ability.SelfEffects.MeleeDamagePercent);
        Assert.AreEqual(300f, ability.SelfEffects.SwingSpeedPercent);
        AssertWarned("meleeDamagePercent");
    }

    [TestMethod]
    public void Load_MoraleFloorAbove100_CappedAt100()
    {
        Assert.AreEqual(100f, LoadOne(extra: "\"allyEffects\":{\"moraleFloor\":150}").AllyEffects.MoraleFloor);
        _sut = NewProvider();
        Assert.AreEqual(60f, LoadOne(extra: "\"allyEffects\":{\"moraleFloor\":60}").AllyEffects.MoraleFloor);
    }

    [TestMethod]
    public void Load_PassiveEffectsAreChecked()
    {
        var config = LoadKits(Kit("hero_x", Ability(), "\"passive\":{\"knockdownResistancePercent\":1500,\"meleeDamagePercent\":-5}"));

        var passive = config.Heroes.Single().Passive;
        Assert.AreEqual(0f, passive.KnockdownResistancePercent);
        Assert.AreEqual(0f, passive.MeleeDamagePercent);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("Has Space")]
    [DataRow("UPPER")]
    [DataRow("a-b")]
    public void Load_AbilityIdBadShape_DropsTheAbility(string id)
    {
        var config = LoadKits(Kit("hero_x", Ability(id: id)));

        Assert.AreEqual(0, config.Heroes.Single().Abilities.Count);
        AssertWarned("ability id");
    }

    [TestMethod]
    public void Load_AbilityId63Chars_KeptAnd64Dropped()
    {
        Assert.AreEqual(1, LoadKits(Kit("hero_x", Ability(id: new string('a', 63)))).Heroes.Single().Abilities.Count);
        _sut = NewProvider();

        Assert.AreEqual(0, LoadKits(Kit("hero_x", Ability(id: new string('a', 64)))).Heroes.Single().Abilities.Count);
    }

    private HeroAbilitiesConfigProvider NewProvider()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        return new HeroAbilitiesConfigProvider(pathService, _logger);
    }

    [TestMethod]
    public void Load_DuplicateAbilityIdAcrossKits_LaterDropped()
    {
        var config = LoadKits(Kit("hero_a", Ability(id: "same")), Kit("hero_b", Ability(id: "same")));

        Assert.AreEqual(1, config.Heroes[0].Abilities.Count);
        Assert.AreEqual(0, config.Heroes[1].Abilities.Count);
        AssertWarned("repeats");
    }

    [TestMethod]
    public void Load_EmptyCharacterId_KitDropped()
    {
        var config = LoadKits(Kit("  ", Ability()), Kit("hero_b", Ability(id: "b_one")));

        Assert.AreEqual("hero_b", config.Heroes.Single().CharacterId);
        AssertWarned("characterId");
    }

    [TestMethod]
    public void Load_DuplicateCharacterIdCaseInsensitive_LaterDropped()
    {
        var config = LoadKits(Kit("Hero_A", Ability(id: "one")), Kit("hero_a", Ability(id: "two")));

        Assert.AreEqual("Hero_A", config.Heroes.Single().CharacterId);
        AssertWarned("repeats");
    }

    [TestMethod]
    public void Load_BadGlow_NoOutlineWithWarning()
    {
        var kit = LoadKits(Kit("hero_x", Ability(), "\"glow\":\"gold\"")).Heroes.Single();

        Assert.IsNull(kit.GlowColor);
        Assert.AreEqual("", kit.Glow);
        AssertWarned("glow");
    }

    [TestMethod]
    public void Load_BadWarCry_Silenced()
    {
        Assert.AreEqual("", LoadOne(extra: "\"warCry\":\"Scream\"").WarCry);
        AssertWarned("warCry");
    }

    [TestMethod]
    public void Load_AnythingRejected_OneSummaryWarning()
    {
        LoadOne(extra: "\"radius\":99,\"allyMorale\":99");

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }

    [TestMethod]
    public void Load_CleanFile_NoSummaryWarning()
    {
        LoadOne();

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_LoadsOnce()
    {
        WriteConfig("{\"heroes\":[]}");

        Assert.AreSame(_sut.GetConfig(), _sut.GetConfig());
    }
}
