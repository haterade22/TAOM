using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.HeroAbilities;
using TAOM.Features.RaceAbilities;
using TAOM.Features.RaceAbilities.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.HeroAbilities;

/// <summary>
/// Pins hero_abilities.json as shipped (#1359). The provider is fail-soft, so a mistyped character id or a value out of
/// range would load quietly and a hero would never cast.
/// </summary>
[TestClass]
public class ShippedHeroAbilitiesConfigTests
{
    private static string ModuleDataPath => RepoPaths.RepoPath("Main", "_Module", "ModuleData");

    private IModLogger _logger = null!;
    private HeroAbilitiesConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        _logger = Substitute.For<IModLogger>();
        _sut = new HeroAbilitiesConfigProvider(pathService, _logger);
    }

    [TestMethod]
    public void ShippedConfig_LoadsWithoutWarningOrError()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled, "hero_abilities.json ships switched off");
        Assert.IsTrue(config.Heroes.Count > 0, "hero_abilities.json ships no kit");
        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ShippedConfig_EveryCharacterId_IsInTaomMissionHeroesXml()
    {
        var xml = File.ReadAllText(Path.Combine(ModuleDataPath, "characters", "taom_mission_heroes.xml"));
        var ids = Regex.Matches(xml, "<NPCCharacter\\b[^>]*?\\bid=\"([^\"]+)\"").Cast<Match>()
            .Select(m => m.Groups[1].Value).ToList();

        var missing = _sut.GetConfig().Heroes.Select(k => k.CharacterId).Where(id => !ids.Contains(id, StringComparer.Ordinal)).ToList();

        Assert.AreEqual(0, missing.Count, "kits with no mission hero: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void ShippedConfig_EveryAbilityId_HasANameRow()
    {
        // TextObject needs the engine, so the name rows are read from the source: each id has its own taom_ key.
        var source = RepoPaths.ReadSource("Main/Features/HeroAbilities/HeroAbilityNames.cs", stripComments: true);

        foreach (var ability in _sut.GetConfig().Heroes.SelectMany(k => k.Abilities))
            StringAssert.Contains(source, $"\"{ability.Id}\" => new TextObject(\"{{=taom_hero_ability_{ability.Id}}}");
    }

    // None of the three carries a shield (#1438): each takes 30 % less from a missile.
    [TestMethod]
    public void ShippedConfig_EveryHero_HasThePassiveMissileReduction()
    {
        foreach (var kit in _sut.GetConfig().Heroes)
            Assert.AreEqual(30f, kit.Passive.MissileDamageReductionPercent, kit.CharacterId);
    }

    [TestMethod]
    public void ShippedConfig_Kits_AreAragornGimliAndLegolas()
    {
        CollectionAssert.AreEquivalent(new[] { "taom_hero_aragorn", "taom_hero_gimli", "taom_hero_legolas" },
            _sut.GetConfig().Heroes.Select(k => k.CharacterId).ToArray());
    }

    // #1361: the kit as the plan pins it. Aragorn and Gimli carry no ranged block.
    [TestMethod]
    public void ShippedConfig_LegolasRow_HasTheRangedKit()
    {
        var heroes = _sut.GetConfig().Heroes;
        var legolas = heroes.Single(k => k.CharacterId == "taom_hero_legolas");
        var ranged = legolas.Ranged;

        Assert.IsNotNull(ranged);
        Assert.IsTrue(ranged!.ZeroAimError);
        Assert.IsTrue(ranged.PierceShields);
        Assert.AreEqual(1f, ranged.HeadshotKillChance);
        Assert.AreEqual(0.10f, ranged.BodyKillChance, 1e-6f);
        Assert.AreEqual("taom_legolas_arrow", ranged.ExtraArrowItem);
        Assert.AreEqual(10f, ranged.ExtraArrowSpreadMeters);
        Assert.AreEqual(120f, ranged.ExtraArrowRangeMeters);
        Assert.AreEqual(5f, ranged.KeepDistanceMeters, "#1415: an enemy within 5 m is a melee threat");
        Assert.AreEqual(8f, ranged.StepBackMeters);
        CollectionAssert.AreEqual(new[] { "legolas_rapid_fire", "legolas_many_arrows", "legolas_archers_eye" },
            legolas.Abilities.Select(a => a.Id).ToArray());
        Assert.AreEqual(2, legolas.Abilities.Single(a => a.Id == "legolas_many_arrows").ExtraArrows);
        Assert.IsTrue(legolas.Abilities.Single(a => a.Id == "legolas_archers_eye").AlliesRangedOnly);
        Assert.IsTrue(legolas.Abilities.All(a => a.EnemyShockMorale == 0f && a.EnemyShockRadius == 0f), "no Legolas effect on enemy morale (LongDefeatMorale.HoldsAttacker)");
        Assert.IsTrue(heroes.Where(k => k.CharacterId != "taom_hero_legolas").All(k => k.Ranged == null));
    }

    [TestMethod]
    public void ShippedConfig_ExtraArrowItem_IsInItemsXml()
    {
        var xml = File.ReadAllText(Path.Combine(ModuleDataPath, "hero_abilities", "hero_abilities_items.xml"));
        var ids = Regex.Matches(xml, "<Item\\b[^>]*?\\bid=\"([^\"]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToList();

        foreach (var kit in _sut.GetConfig().Heroes.Where(k => k.Ranged != null && k.Ranged.ExtraArrowItem.Length > 0))
            CollectionAssert.Contains(ids, kit.Ranged!.ExtraArrowItem, kit.CharacterId);
    }

    // The items file loads for a Custom Game (the Long Defeat) and for a campaign (the Helm's Deep story mission, #1366).
    [TestMethod]
    public void ShippedConfig_ItemsXml_LoadsForCampaignAndCustomGame()
    {
        var subModule = File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "SubModule.xml"));
        var node = Regex.Match(subModule,
            "<XmlNode>\\s*<XmlName id=\"Items\" path=\"hero_abilities/hero_abilities_items\"\\s*/>\\s*<IncludedGameTypes>\\s*<GameType value=\"Campaign\"\\s*/>\\s*<GameType value=\"CampaignStoryMode\"\\s*/>\\s*<GameType value=\"CustomGame\"\\s*/>\\s*</IncludedGameTypes>\\s*</XmlNode>");

        Assert.IsTrue(node.Success, "SubModule.xml needs the hero_abilities_items node for Campaign, CampaignStoryMode and CustomGame");
    }

    [TestMethod]
    public void ShippedConfig_ExtraArrowItem_IsNotMerchandise_AndCannotBePickedUp()
    {
        // In a campaign is_merchandise="false" (ItemObject.NotMerchandise) keeps the arrow out of workshops, loot and prizes,
        // and CannotBePickedUp keeps a stuck arrow off the field.
        var item = System.Xml.Linq.XDocument.Load(Path.Combine(ModuleDataPath, "hero_abilities", "hero_abilities_items.xml")).Root!
            .Elements("Item").Single(i => (string?)i.Attribute("id") == "taom_legolas_arrow");

        Assert.AreEqual("false", (string?)item.Attribute("is_merchandise"));
        Assert.AreEqual("true", (string?)item.Element("Flags")?.Attribute("CannotBePickedUp"));
    }

    [TestMethod]
    public void ShippedConfig_EveryKit_HasAnEnemyTriggeredAbility()
    {
        // A kit whose abilities all wait for low health never casts for a hero who is not hurt.
        foreach (var kit in _sut.GetConfig().Heroes)
            Assert.IsTrue(kit.Abilities.Any(a => a.AnyOf.Concat(a.Requires).Any(t => t.ParsedKind == RaceAbilityTriggerKind.EnemiesWithin)),
                kit.CharacterId + " has no ability an enemy close by can trigger");
    }

    [TestMethod]
    public void ShippedConfig_EveryKit_HasAGlowColour()
    {
        foreach (var kit in _sut.GetConfig().Heroes)
            Assert.IsTrue(kit.GlowColor.HasValue, kit.CharacterId + " has no outline colour");
    }

    [TestMethod]
    public void TriggerCheck_Limits_ArePinned()
    {
        // The shipped triggers sit inside these limits; a change to one would revert a shipped range without a word here.
        Assert.AreEqual(40f, RaceAbilityTriggerCheck.MaxRange);
        Assert.AreEqual(15f, RaceAbilityTriggerCheck.MaxKinRadius);
        Assert.AreEqual(30f, RaceAbilityTriggerCheck.MaxEventSeconds);
    }
}
