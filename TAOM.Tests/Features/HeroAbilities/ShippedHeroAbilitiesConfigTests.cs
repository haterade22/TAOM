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

    [TestMethod]
    public void ShippedConfig_Kits_AreAragornAndGimliOnly()
    {
        // Legolas's ranged kit is Phase 5.
        CollectionAssert.AreEquivalent(new[] { "taom_hero_aragorn", "taom_hero_gimli" },
            _sut.GetConfig().Heroes.Select(k => k.CharacterId).ToArray());
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
