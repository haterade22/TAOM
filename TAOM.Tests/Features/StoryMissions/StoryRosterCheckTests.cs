using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.HeroAbilities;
using TAOM.Features.HeroAbilities.Domain;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.StoryMissions;
using TAOM.Features.StoryMissions.Config;
using TAOM.Tests.Features.LongDefeat;

namespace TAOM.Tests.Features.StoryMissions;

/// <summary>The presence check written when a campaign session starts (#1366): every troop a story mission spawns and every hero kit's extra arrow item must resolve.</summary>
[TestClass]
public class StoryRosterCheckTests
{
    private const string OneMission =
        "{\"missions\":[{\"id\":\"m1\",\"scene\":\"scene_a\",\"side\":\"Defend\",\"defenderCulture\":\"vlandia\",\"attackerCulture\":\"isengard\","
        + "\"garrison\":[{\"troop\":\"g1\",\"class\":\"Infantry\",\"count\":10}],"
        + "\"heroes\":[{\"id\":\"aragorn\",\"troop\":\"hero_a\"}],"
        + "\"roster\":{\"waves\":[{\"count\":110,\"mix\":[{\"troop\":\"a1\",\"class\":\"Infantry\",\"weight\":1}]}]}}]}";

    private readonly RecordingOutcomeLog _log = new();
    private readonly IObjectManagerAdapter _objects = Substitute.For<IObjectManagerAdapter>();
    private readonly IHeroAbilitiesConfigProvider _config = Substitute.For<IHeroAbilitiesConfigProvider>();
    private readonly IModLogger _logger = Substitute.For<IModLogger>();

    private StoryRosterService Service(string? json = OneMission, string[]? arrows = null, Func<string, bool>? itemExists = null, Func<string, bool>? characterExists = null)
    {
        var kits = new HeroAbilitiesConfig();
        foreach (var arrow in arrows ?? new[] { "arrow_x" })
            kits.Heroes.Add(new HeroKitConfig { CharacterId = "hero_a", Ranged = new HeroRangedConfig { ExtraArrowItem = arrow } });
        kits.Heroes.Add(new HeroKitConfig { CharacterId = "plain" });
        _config.GetConfig().Returns(kits);
        _objects.ItemExists(Arg.Any<string>()).Returns(c => itemExists?.Invoke((string)c[0]) ?? true);
        _objects.GetBasicCharacter(Arg.Any<string>()).Returns(c => (characterExists?.Invoke((string)c[0]) ?? true) ? new BasicCharacterObject() : null);
        return new StoryRosterService(StoryMissionCatalog.FromText(json, _logger), _config, _objects, _log, _logger);
    }

    [TestMethod]
    public void Judge_NothingMissing_Passes_WithTheExactDetail()
    {
        var verdict = StoryRosterCheck.Judge(2, 7, Array.Empty<string>(), 1, Array.Empty<string>());

        Assert.AreEqual(CheckResult.Pass, verdict.Result);
        Assert.AreEqual("missions=2 troops=7 missing=- items=1 items_missing=-", verdict.Detail);
    }

    [TestMethod]
    public void Judge_AMissingTroop_Fails_AndNamesIt()
    {
        var verdict = StoryRosterCheck.Judge(1, 3, new[] { "taom_hero_gimli", "a1" }, 1, Array.Empty<string>());

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        Assert.AreEqual("missions=1 troops=3 missing=taom_hero_gimli,a1 items=1 items_missing=-", verdict.Detail);
    }

    [TestMethod]
    public void Judge_AMissingItem_Fails_AndNamesIt()
    {
        var verdict = StoryRosterCheck.Judge(1, 3, Array.Empty<string>(), 1, new[] { "taom_legolas_arrow" });

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        Assert.AreEqual("missions=1 troops=3 missing=- items=1 items_missing=taom_legolas_arrow", verdict.Detail);
    }

    [TestMethod]
    public void Judge_NoMissionLoaded_Fails_SoALostCatalogIsNeverAPass()
    {
        var verdict = StoryRosterCheck.Judge(0, 0, Array.Empty<string>(), 0, Array.Empty<string>());

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
    }

    [TestCategory("RequiresGame")]   // constructs BasicCharacterObject, and the service catches the throw
    [TestMethod]
    public void Report_EveryIdResolves_WritesOnePass_CountingTheFreeHeroTroop()
    {
        Service().Report();

        CollectionAssert.AreEqual(new[] { "pass taom.story-missions.roster  missions=1 troops=3 missing=- items=1 items_missing=-" }, _log.Lines);
    }

    [TestCategory("RequiresGame")]   // constructs BasicCharacterObject, and the service catches the throw
    [TestMethod]
    public void Report_AMissingHeroTroop_Fails_NamingTheTroop()
    {
        Service(characterExists: id => id != "hero_a").Report();

        CollectionAssert.AreEqual(new[] { "fail taom.story-missions.roster  missions=1 troops=3 missing=hero_a items=1 items_missing=-" }, _log.Lines);
    }

    [TestCategory("RequiresGame")]   // constructs BasicCharacterObject, and the service catches the throw
    [TestMethod]
    public void Report_AMissingExtraArrowItem_Fails_NamingTheItem()
    {
        Service(itemExists: id => id != "arrow_x").Report();

        CollectionAssert.AreEqual(new[] { "fail taom.story-missions.roster  missions=1 troops=3 missing=- items=1 items_missing=arrow_x" }, _log.Lines);
    }

    [TestCategory("RequiresGame")]   // constructs BasicCharacterObject, and the service catches the throw
    [TestMethod]
    public void Report_TwoKitsWithTheSameArrow_CountOneItem_AndAKitWithNoRangedBlockCountsNone()
    {
        Service(arrows: new[] { "arrow_x", "arrow_x", "" }).Report();

        StringAssert.Contains(_log.Lines[0], "items=1 ");
    }

    [TestMethod]
    public void Report_NoMissionLoaded_Fails()
    {
        Service(json: null).Report();

        Assert.IsTrue(_log.Has("fail", StoryRosterCheck.Id));
    }

    [TestCategory("RequiresGame")]   // constructs BasicCharacterObject
    [TestMethod]
    public void Report_AnAdapterThatThrows_IsLogged_AndNeverEscapes()
    {
        var service = Service();
        _objects.ItemExists(Arg.Any<string>()).Returns(_ => throw new InvalidOperationException("boom"));

        service.Report();

        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("boom")));
        Assert.AreEqual(0, _log.Lines.Count);
    }
}
