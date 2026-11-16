using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.HeroAbilities;
using TAOM.Features.HeroAbilities.Domain;
using TAOM.Features.RaceAbilities;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Tests.Features.HeroAbilities;

[TestClass]
public class HeroAbilityServiceTests
{
    private HeroAbilitiesConfig _config = null!;
    private HeroAbilityService _sut = null!;

    private static HeroAbilityConfig Ability(string id, float cooldown = 90f, params RaceAbilityTrigger[] anyOf) =>
        new HeroAbilityConfig { Id = id, CooldownSeconds = cooldown, DurationSeconds = 10f, AnyOf = new List<RaceAbilityTrigger>(anyOf) };

    private static RaceAbilityTrigger Enemies(float range, int count) =>
        new RaceAbilityTrigger { Kind = "EnemiesWithin", ParsedKind = RaceAbilityTriggerKind.EnemiesWithin, Range = range, Count = count };

    [TestInitialize]
    public void Setup()
    {
        _config = new HeroAbilitiesConfig();
        var provider = Substitute.For<IHeroAbilitiesConfigProvider>();
        provider.GetConfig().Returns(_ => _config);
        _sut = new HeroAbilityService(provider, new RaceAbilityService());
    }

    [TestMethod]
    public void ConfigEnabledAndKitCount_ReadTheConfig()
    {
        _config.Enabled = false;
        _config.Heroes.Add(new HeroKitConfig { CharacterId = "a" });

        Assert.IsFalse(_sut.ConfigEnabled);
        Assert.AreEqual(1, _sut.KitCount);
    }

    [TestMethod]
    public void KitFor_MatchesIgnoringCase_NullAndUnknownGiveNull()
    {
        var kit = new HeroKitConfig { CharacterId = "taom_hero_gimli" };
        _config.Heroes.Add(kit);

        Assert.AreSame(kit, _sut.KitFor("TAOM_Hero_Gimli"));
        Assert.IsNull(_sut.KitFor("taom_hero_legolas"));
        Assert.IsNull(_sut.KitFor(null));
        Assert.IsNull(_sut.KitFor(""));
    }

    [TestMethod]
    public void ProfileOf_IsCachedAndCarriesTheTriggers()
    {
        var ability = Ability("a", 90f, Enemies(10f, 3));

        var profile = _sut.ProfileOf(ability);

        Assert.AreSame(profile, _sut.ProfileOf(ability));
        Assert.AreEqual("a", profile.AbilityId);
        Assert.AreEqual(1, profile.AnyOf.Count);
    }

    [TestMethod]
    public void Fires_FollowsTheTriggers()
    {
        var profile = _sut.ProfileOf(Ability("a", 90f, Enemies(10f, 2)));
        var senses = new RaceAbilitySenses();
        senses.Enemies.Add(new EnemySense(5f, 1f, false));

        Assert.IsFalse(_sut.Fires(profile, senses));
        senses.Enemies.Add(new EnemySense(9f, 1f, false));
        Assert.IsTrue(_sut.Fires(profile, senses));
        senses.Enemies.Clear();
        senses.Enemies.Add(new EnemySense(11f, 1f, false));
        senses.Enemies.Add(new EnemySense(12f, 1f, false));
        Assert.IsFalse(_sut.Fires(profile, senses));
    }

    [TestMethod]
    public void PickAbility_FirstOffCooldownThatFires()
    {
        var abilities = new[] { Ability("a", 90f), Ability("b", 90f), Ability("c", 90f) };
        var last = new float?[] { null, null, null };

        Assert.AreEqual(1, _sut.PickAbility(abilities, last, 100f, i => i >= 1));
        Assert.AreEqual(-1, _sut.PickAbility(abilities, last, 100f, i => false));
    }

    [TestMethod]
    public void PickAbility_OnCooldownIsSkippedWithoutAskingFires()
    {
        var abilities = new[] { Ability("a", 90f), Ability("b", 90f) };
        var last = new float?[] { 50f, null };
        var asked = new List<int>();

        var picked = _sut.PickAbility(abilities, last, 100f, i => { asked.Add(i); return true; });

        Assert.AreEqual(1, picked);
        CollectionAssert.AreEqual(new[] { 1 }, asked);
    }

    [TestMethod]
    public void PickAbility_CooldownElapsed_Picks()
    {
        var abilities = new[] { Ability("a", 90f) };

        Assert.AreEqual(0, _sut.PickAbility(abilities, new float?[] { 10f }, 100f, _ => true));
        Assert.AreEqual(-1, _sut.PickAbility(abilities, new float?[] { 11f }, 100f, _ => true));
    }

    [TestMethod]
    public void PickAbility_NaNNow_ReturnsMinusOneWithoutCallingFires()
    {
        var called = false;

        var picked = _sut.PickAbility(new[] { Ability("a") }, new float?[] { null }, float.NaN, _ => { called = true; return true; });

        Assert.AreEqual(-1, picked);
        Assert.IsFalse(called);
    }

    [TestMethod]
    public void PickAbility_NoAbilities_MinusOne() =>
        Assert.AreEqual(-1, _sut.PickAbility(new HeroAbilityConfig[0], new float?[0], 5f, _ => true));

    [DataTestMethod]
    [DataRow(true, false, false, false, true)]
    [DataRow(false, false, false, false, false)]
    [DataRow(true, true, false, false, false)]
    [DataRow(true, false, true, false, false)]
    [DataRow(true, false, false, true, false)]
    public void ShouldDecide_OnlyAnAiHeroNotRetreating(bool ai, bool player, bool main, bool retreating, bool expected) =>
        Assert.AreEqual(expected, _sut.ShouldDecide(ai, player, main, retreating));

    [DataTestMethod]
    [DataRow(50f, 25f, 0f, 25f)]
    [DataRow(90f, 25f, 0f, 10f)]
    [DataRow(100f, 25f, 0f, 0f)]
    [DataRow(30f, 0f, 60f, 30f)]
    [DataRow(30f, 10f, 60f, 30f)]
    [DataRow(70f, 10f, 60f, 10f)]
    [DataRow(-1f, 25f, 60f, 0f)]
    [DataRow(float.NaN, 25f, 60f, 0f)]
    [DataRow(50f, float.NaN, 0f, 0f)]
    [DataRow(50f, 10f, float.NaN, 10f)]
    [DataRow(50f, -5f, 0f, 0f)]
    public void AllyMoraleGain_TopsUpToFloorNeverPast100(float morale, float gain, float floor, float expected) =>
        Assert.AreEqual(expected, _sut.AllyMoraleGain(morale, gain, floor), 0.001f);

    [TestMethod]
    public void SelfEffects_CombinesSelfAndAllyStrongerPerChannel()
    {
        var ability = new HeroAbilityConfig
        {
            SelfEffects = new RaceAbilityEffects { DamageReductionPercent = 40f, ShrugOffBlows = true },
            AllyEffects = new RaceAbilityEffects { DamageReductionPercent = 20f, BlockAbilityPercent = 20f },
        };

        var effects = _sut.SelfEffects(ability);

        Assert.AreEqual(40f, effects.DamageReductionPercent);
        Assert.AreEqual(20f, effects.BlockAbilityPercent);
        Assert.IsTrue(effects.ShrugOffBlows);
    }

    [TestMethod]
    public void IsActive_WithinDurationOnly()
    {
        Assert.IsFalse(_sut.IsActive(null, 10f, 12f));
        Assert.IsTrue(_sut.IsActive(10f, 21.9f, 12f));
        Assert.IsFalse(_sut.IsActive(10f, 22f, 12f));
        Assert.IsFalse(_sut.IsActive(10f, 5f, 12f));
    }

    [TestMethod]
    public void IsActive_NaNNow_False() =>
        Assert.IsFalse(_sut.IsActive(10f, float.NaN, 12f));

    [TestMethod]
    public void Constants_ArePinned()
    {
        Assert.AreEqual(1f, HeroAbilityService.PassSeconds);
        Assert.AreEqual(20f, HeroAbilityService.ContactSecondsToJudge);
    }

    private static HeroKitConfig KitOf(params HeroAbilityConfig[] abilities) =>
        new HeroKitConfig { CharacterId = "k", Abilities = new List<HeroAbilityConfig>(abilities) };

    private static RaceAbilityTrigger Other() =>
        new RaceAbilityTrigger { Kind = "HealthBelow", ParsedKind = RaceAbilityTriggerKind.HealthBelow, Range = 2f, Count = 1 };

    [TestMethod]
    public void TryContactRule_SmallestCountWins()
    {
        var kit = KitOf(Ability("a", 90f, Enemies(10f, 3)), Ability("b", 90f, Enemies(6f, 2)));

        Assert.IsTrue(_sut.TryContactRule(kit, out var count, out var range));
        Assert.AreEqual(2, count);
        Assert.AreEqual(6f, range);
    }

    [TestMethod]
    public void TryContactRule_TieTakesTheLargerRange()
    {
        var kit = KitOf(Ability("a", 90f, Enemies(6f, 3)), Ability("b", 90f, Enemies(8f, 3)));

        Assert.IsTrue(_sut.TryContactRule(kit, out var count, out var range));
        Assert.AreEqual(3, count);
        Assert.AreEqual(8f, range);
    }

    [TestMethod]
    public void TryContactRule_ReadsRequiresAndAnyOf()
    {
        var requires = Ability("a", 90f);
        requires.Requires = new List<RaceAbilityTrigger> { Enemies(9f, 4) };
        var anyOf = Ability("b", 90f, Enemies(7f, 1));

        Assert.IsTrue(_sut.TryContactRule(KitOf(requires), out var c1, out var r1));
        Assert.AreEqual(4, c1);
        Assert.AreEqual(9f, r1);
        Assert.IsTrue(_sut.TryContactRule(KitOf(requires, anyOf), out var c2, out var r2));
        Assert.AreEqual(1, c2);
        Assert.AreEqual(7f, r2);
    }

    [TestMethod]
    public void TryContactRule_NoEnemyTrigger_NoRule()
    {
        Assert.IsFalse(_sut.TryContactRule(KitOf(), out _, out _));
        Assert.IsFalse(_sut.TryContactRule(KitOf(Ability("a", 90f, Other())), out var count, out var range));
        Assert.AreEqual(0, count);
        Assert.AreEqual(0f, range);
    }

    [TestMethod]
    public void TryContactRule_NonEnemyTriggerIgnored()
    {
        var kit = KitOf(Ability("a", 90f, Other(), Enemies(8f, 3)));

        Assert.IsTrue(_sut.TryContactRule(kit, out var count, out var range));
        Assert.AreEqual(3, count);
        Assert.AreEqual(8f, range);
    }
}
