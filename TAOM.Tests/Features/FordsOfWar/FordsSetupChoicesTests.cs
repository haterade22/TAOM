using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
public class FordsSetupChoicesTests
{
    private const string TwoMaps = @"{
  'defaults': { 'options': { 'laneCount': 3 } },
  'scenes': [
    { 'scene': 'scene_a', 'name': '{=aom_a}Scene A', 'layout': 'generated' },
    { 'scene': 'scene_b', 'name': '{=aom_b}Scene B', 'layout': 'generated', 'options': { 'laneCount': 4 } }
  ]
}";

    private static FordsConfigCatalog Catalog(string? maps = null) =>
        FordsConfigCatalog.FromTexts(null, FordsConfigFixtures.Factions, maps ?? FordsConfigFixtures.Maps, Substitute.For<IModLogger>());

    [TestMethod]
    public void Factions_ListsEveryFactionInFileOrderWithItsRawName()
    {
        var choices = FordsSetupChoices.Factions(Catalog());

        CollectionAssert.AreEqual(new[] { "gondor", "mordor" }, choices.Select(c => c.Id).ToArray());
        Assert.AreEqual("{=TAOM_FOW_Faction_Gondor}Gondor", choices[0].Label);
        Assert.AreEqual("{=TAOM_FOW_Faction_Mordor}Mordor", choices[1].Label);
    }

    [TestMethod]
    public void Maps_ListsEveryMapInFileOrderWithItsRawName()
    {
        var choices = FordsSetupChoices.Maps(Catalog(TwoMaps));

        CollectionAssert.AreEqual(new[] { "scene_a", "scene_b" }, choices.Select(c => c.Id).ToArray());
        Assert.AreEqual("{=aom_b}Scene B", choices[1].Label);
    }

    [TestMethod]
    public void Difficulties_PutsEasyNormalHardFirstThenTheRestAlphabetically()
    {
        var profiles = new Dictionary<string, FordsAiProfile>
        {
            ["zealot"] = new("zealot", 3f, 1.5f, 0f, 500),
            ["hard"] = new("hard", 5f, 1.2f, 0f, 400),
            ["brutal"] = new("brutal", 4f, 1.3f, 0f, 450),
            ["easy"] = new("easy", 12f, 0.8f, 0.25f, 200),
            ["normal"] = new("normal", 8f, 1f, 0.1f, 300),
        };

        var choices = FordsSetupChoices.Difficulties(profiles);

        CollectionAssert.AreEqual(new[] { "easy", "normal", "hard", "brutal", "zealot" }, choices.Select(c => c.Id).ToArray());
    }

    [TestMethod]
    public void Difficulties_ShippedNamesGetTheirOwnKeys_AndACustomNameIsPlainText()
    {
        var profiles = FordsMatchConfig.Default.AiProfiles.ToDictionary(p => p.Key, p => p.Value);
        profiles["zealot"] = new FordsAiProfile("zealot", 3f, 1.5f, 0f, 500);

        var labels = FordsSetupChoices.Difficulties(profiles).ToDictionary(c => c.Id, c => c.Label);

        Assert.AreEqual("{=TAOM_FOW_Difficulty_Easy}Easy", labels["easy"]);
        Assert.AreEqual("{=TAOM_FOW_Difficulty_Normal}Normal", labels["normal"]);
        Assert.AreEqual("{=TAOM_FOW_Difficulty_Hard}Hard", labels["hard"]);
        Assert.AreEqual("{=!}zealot", labels["zealot"]);
    }

    [TestMethod]
    public void TryResolve_NoArguments_UsesTheFirstFactionAnotherEastNormalAndTheFirstMap()
    {
        var ok = FordsSetupChoices.TryResolve(Catalog(), null, null, null, null, out var setup, out var error);

        Assert.IsTrue(ok, error);
        Assert.AreEqual(new FordsMatchSetup("taom_rohan_battle_001_forceatmo", "gondor", "mordor", "normal", 3), setup);
    }

    [TestMethod]
    public void TryResolve_NamedArguments_AreUsedAndMatchedCaseInsensitively()
    {
        var ok = FordsSetupChoices.TryResolve(Catalog(TwoMaps), "MORDOR", "Gondor", "Hard", "SCENE_B", out var setup, out var error);

        Assert.IsTrue(ok, error);
        Assert.AreEqual(new FordsMatchSetup("scene_b", "mordor", "gondor", "hard", 4), setup);
    }

    [TestMethod]
    public void TryResolve_OnlyTheWestNamed_PicksTheFirstOtherFactionForEast()
    {
        var ok = FordsSetupChoices.TryResolve(Catalog(), "mordor", null, null, null, out var setup, out _);

        Assert.IsTrue(ok);
        Assert.AreEqual("gondor", setup.EastFaction);
    }

    [TestMethod]
    public void TryResolve_UnknownFaction_FailsNamingTheValidIds()
    {
        var ok = FordsSetupChoices.TryResolve(Catalog(), "gondor", "narnia", null, null, out _, out var error);

        Assert.IsFalse(ok);
        StringAssert.Contains(error, "narnia");
        StringAssert.Contains(error, "gondor, mordor");
    }

    [TestMethod]
    public void TryResolve_UnknownProfile_FailsNamingTheValidIds()
    {
        var ok = FordsSetupChoices.TryResolve(Catalog(), null, null, "legendary", null, out _, out var error);

        Assert.IsFalse(ok);
        StringAssert.Contains(error, "legendary");
        StringAssert.Contains(error, "easy, normal, hard");
    }

    [TestMethod]
    public void TryResolve_UnknownScene_FailsNamingTheValidIds()
    {
        var ok = FordsSetupChoices.TryResolve(Catalog(TwoMaps), null, null, null, "scene_z", out _, out var error);

        Assert.IsFalse(ok);
        StringAssert.Contains(error, "scene_z");
        StringAssert.Contains(error, "scene_a, scene_b");
    }

    [TestMethod]
    public void TryResolve_NoFactionsOrNoMaps_FailsWithoutThrowing()
    {
        var empty = FordsConfigCatalog.FromTexts(null, null, null, Substitute.For<IModLogger>());

        Assert.IsFalse(FordsSetupChoices.TryResolve(empty, null, null, null, null, out _, out var error));
        StringAssert.Contains(error, "factions");
    }

    [TestMethod]
    public void TryResolve_BlankArguments_CountAsMissing()
    {
        var ok = FordsSetupChoices.TryResolve(Catalog(), " ", "", " ", "", out var setup, out var error);

        Assert.IsTrue(ok, error);
        Assert.AreEqual("normal", setup.AiProfile);
    }

    [TestMethod]
    public void Compose_TakesTheLaneCountFromTheMapsOptions()
    {
        var catalog = Catalog(TwoMaps);
        catalog.TryGetMap("scene_b", out var map);

        var setup = FordsSetupChoices.Compose(map, "gondor", "mordor", "easy");

        Assert.AreEqual(new FordsMatchSetup("scene_b", "gondor", "mordor", "easy", 4), setup);
    }

    [TestMethod]
    public void HasContent_NeedsAFactionAndAMap()
    {
        Assert.IsTrue(FordsSetupChoices.HasContent(Catalog()));
        Assert.IsFalse(FordsSetupChoices.HasContent(FordsConfigCatalog.FromTexts(null, null, FordsConfigFixtures.Maps, Substitute.For<IModLogger>())));
        Assert.IsFalse(FordsSetupChoices.HasContent(FordsConfigCatalog.FromTexts(null, FordsConfigFixtures.Factions, null, Substitute.For<IModLogger>())));
    }
}
