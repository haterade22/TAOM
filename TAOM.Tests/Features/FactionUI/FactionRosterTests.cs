using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FactionUI;
using TAOM.Features.FactionUI.FactionScreen;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704. The faction screen's rules, kept off the engine: which troop the 3D viewport shows,
/// which lords the browse lists offer, when the Leader tab is shown, and which faction a minimap click
/// lands on.
/// </summary>
[TestClass]
public class FactionRosterTests
{
    private const string Root = @"C:\Game\Modules\TAOM";

    private IFactionRosterAdapter _adapter = null!;
    private FakeFrontEndResourceAdapter _files = null!;
    private FactionUIPaths _paths = null!;
    private FactionRoster _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleRootPath.Returns(Root);
        pathService.ModuleDataPath.Returns(Path.Combine(Root, "ModuleData"));
        _paths = new FactionUIPaths(pathService);
        _files = new FakeFrontEndResourceAdapter();
        _adapter = Substitute.For<IFactionRosterAdapter>();
        _adapter.RaceCount.Returns(10);
        _sut = new FactionRoster(_adapter, new FactionScreenConfigProvider(_files, _paths, Substitute.For<IModLogger>()));
    }

    private static RosterEntry Troop(string id, int tier, int level = 10, bool infantry = true, bool ranged = false) =>
        new(new object(), id, id, isHero: false, tier: tier, level: level, isInfantry: infantry, isRanged: ranged);

    private static RosterEntry Lord(string id, bool alive = true) => new(new object(), id, id, isHero: true, isAlive: alive);

    private void WriteConfig(string file, string json) => _files.AddFile(Path.Combine(_paths.ConfigDirectory, file), json);

    // ── Elite infantry ────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void EliteInfantry_PicksTheHighestTierFootSoldier()
    {
        _adapter.CultureTroopTree("vlandia").Returns(new[] { Troop("recruit", 1), Troop("knight", 6, infantry: false), Troop("sergeant", 5), Troop("footman", 4) });

        Assert.AreEqual("sergeant", _sut.EliteInfantry("vlandia")!.Id);
    }

    [TestMethod]
    public void EliteInfantry_IgnoresInfantryThatShoots()
    {
        _adapter.CultureTroopTree("vlandia").Returns(new[] { Troop("crossbowman", 6, ranged: true), Troop("sergeant", 5) });

        Assert.AreEqual("sergeant", _sut.EliteInfantry("vlandia")!.Id);
    }

    [TestMethod]
    public void EliteInfantry_ATierTie_GoesToTheHigherLevelThenTheFirstId()
    {
        _adapter.CultureTroopTree("vlandia").Returns(new[] { Troop("b_guard", 5, level: 26), Troop("a_guard", 5, level: 26), Troop("c_guard", 5, level: 21) });

        Assert.AreEqual("a_guard", _sut.EliteInfantry("vlandia")!.Id);
    }

    [TestMethod]
    public void EliteInfantry_NoFootSoldierAtAll_FallsBackToTheCultureTroop()
    {
        var elite = Troop("elite", 3);
        _adapter.CultureTroopTree("khuzait").Returns(new[] { Troop("horse_archer", 5, infantry: false, ranged: true) });
        _adapter.CultureTroop("khuzait").Returns(elite);

        Assert.AreSame(elite, _sut.EliteInfantry("khuzait"));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void EliteInfantry_NoCulture_IsNull(string? cultureId)
    {
        Assert.IsNull(_sut.EliteInfantry(cultureId));
    }

    // ── Rulers and lords ──────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Ruler_ADeadRuler_IsOnlyReturnedWhenTheCallerAcceptsTheDead()
    {
        _adapter.Ruler("empire").Returns(Lord("old_king", alive: false));

        Assert.IsNull(_sut.Ruler("empire", aliveOnly: true));
        Assert.AreEqual("old_king", _sut.Ruler("empire", aliveOnly: false)!.Id);
    }

    [TestMethod]
    public void Lords_ListsOnlyTheLivingClanLeaders()
    {
        _adapter.OtherClanLeaders("empire").Returns(new[] { Lord("alive_1"), Lord("dead", alive: false), Lord("alive_2") });

        CollectionAssert.AreEqual(new[] { "alive_1", "alive_2" }, _sut.Lords("empire").Select(l => l.Id).ToList());
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Lords_NoKingdomMapped_IsEmptyWithoutAskingTheEngine(string? kingdomId)
    {
        Assert.AreEqual(0, _sut.Lords(kingdomId).Count);
        _adapter.DidNotReceiveWithAnyArgs().OtherClanLeaders(default!);
    }

    // ── The Leader tab ────────────────────────────────────────────────────────────────────────

    private static FactionInfo Faction(string key, string kingdomId = "kingdom") => new() { Key = key, KingdomId = kingdomId, CultureId = "culture" };

    [TestMethod]
    public void ShowLeaderCategory_TheRulerAlreadyHasANamedCard_HidesTheTab()
    {
        _adapter.Ruler("vlandia").Returns(Lord("lord_4_1"));

        Assert.IsFalse(_sut.ShowLeaderCategory(Faction("kingdom_of_rohan", "vlandia")), "Theoden's card already offers him");
    }

    [TestMethod]
    public void ShowLeaderCategory_ARulerWithoutACard_ShowsTheTab()
    {
        _adapter.Ruler("vlandia").Returns(Lord("lord_someone_else"));

        Assert.IsTrue(_sut.ShowLeaderCategory(Faction("kingdom_of_rohan", "vlandia")));
    }

    [TestMethod]
    public void ShowLeaderCategory_AFactionWithNoCardsOrNoRuler_ShowsTheTab()
    {
        _adapter.Ruler("aserai").Returns(Lord("anyone"));

        Assert.IsTrue(_sut.ShowLeaderCategory(Faction("taskralan_of_harwan", "aserai")), "no named cards");
        Assert.IsTrue(_sut.ShowLeaderCategory(Faction("kingdom_of_rohan", "no_such_kingdom")), "no ruler");
    }

    // ── Named cards ───────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void CardCharacter_AMissingCharacter_FallsBackToTheCultureTroop()
    {
        var placeholder = Troop("placeholder", 4);
        _adapter.CultureTroop("culture").Returns(placeholder);

        Assert.AreSame(placeholder, _sut.CardCharacter(new SpecialCharacter("Nobody", "missing_id", "p", "r", 400f), "culture"));
    }

    [TestMethod]
    public void CardCharacter_AnExistingCharacter_IsUsed()
    {
        var gandalf = Troop("taom_fui_gandalf", 0);
        _adapter.Character("taom_fui_gandalf").Returns(gandalf);

        Assert.AreSame(gandalf, _sut.CardCharacter(new SpecialCharacter("Gandalf", "taom_fui_gandalf", "p", "r", 551f), "culture"));
    }

    // ── The 3D viewport ───────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void ViewportCharacter_TheConfiguredCharacter_WinsOverTheEliteTroop()
    {
        WriteConfig(FactionScreenConfigProvider.CharactersFile, "{ \"kingdom_of_rohan\": \"lord_4_1\" }");
        var theoden = Troop("lord_4_1", 0);
        _adapter.Character("lord_4_1").Returns(theoden);

        Assert.AreSame(theoden, _sut.ViewportCharacter(Faction("kingdom_of_rohan")));
    }

    [TestMethod]
    public void ViewportCharacter_NoConfiguredCharacterOrItIsMissing_ShowsTheEliteInfantry()
    {
        WriteConfig(FactionScreenConfigProvider.CharactersFile, "{ \"kingdom_of_rohan\": \"missing\" }");
        _adapter.CultureTroopTree("culture").Returns(new[] { Troop("sergeant", 5) });

        Assert.AreEqual("sergeant", _sut.ViewportCharacter(Faction("kingdom_of_rohan"))!.Id);
        Assert.AreEqual("sergeant", _sut.ViewportCharacter(Faction("havens_of_umbar"))!.Id);
    }

    [TestMethod]
    public void ViewportRace_AConfiguredRaceTheEngineKnows_IsUsed()
    {
        WriteConfig(FactionScreenConfigProvider.ViewportFile, "{ \"dominion_of_mordor\": { \"race\": 0 } }");

        Assert.AreEqual(0, _sut.ViewportRace(Faction("dominion_of_mordor")));
    }

    [TestMethod]
    public void ViewportRace_ARaceBeyondTheEnginesCount_IsIgnored()
    {
        WriteConfig(FactionScreenConfigProvider.ViewportFile, "{ \"dominion_of_mordor\": { \"race\": 12 } }");

        Assert.IsNull(_sut.ViewportRace(Faction("dominion_of_mordor")));
    }

    [TestMethod]
    public void ViewportTweaks_NoEntry_MeansNoOffsetAndWeaponsShown()
    {
        Assert.IsNull(_sut.ViewportRace(Faction("kingdom_of_rohan")));
        Assert.AreEqual(0f, _sut.ViewportOffset(Faction("kingdom_of_rohan")));
        Assert.IsFalse(_sut.HideViewportWeapons(Faction("kingdom_of_rohan")));
    }

    [TestMethod]
    public void ViewportTweaks_AnEntry_GivesItsOffsetAndHidesTheWeapons()
    {
        WriteConfig(FactionScreenConfigProvider.ViewportFile, "{ \"dominion_of_mordor\": { \"offset\": 40, \"hide_weapons\": true } }");

        Assert.AreEqual(40f, _sut.ViewportOffset(Faction("dominion_of_mordor")));
        Assert.IsTrue(_sut.HideViewportWeapons(Faction("dominion_of_mordor")));
    }

    // ── The minimap ───────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void NearestFaction_AClick_SelectsTheClosestPin()
    {
        var gondor = new FactionInfo { Key = "gondor", MapX = 0.62, MapY = 0.64 };
        var rohan = new FactionInfo { Key = "rohan", MapX = 0.55, MapY = 0.54 };
        var erebor = new FactionInfo { Key = "erebor", MapX = 0.74, MapY = 0.13 };

        Assert.AreSame(rohan, FactionRoster.NearestFaction(new List<FactionInfo> { gondor, rohan, erebor }, 0.56, 0.55));
        Assert.AreSame(erebor, FactionRoster.NearestFaction(new List<FactionInfo> { gondor, rohan, erebor }, 0.9, 0.0));
    }

    [TestMethod]
    public void NearestFaction_NoFactions_IsNull()
    {
        Assert.IsNull(FactionRoster.NearestFaction(new List<FactionInfo>(), 0.5, 0.5));
    }

    [DataTestMethod]
    [DataRow(double.NaN, 0.5)]
    [DataRow(0.5, double.NaN)]
    [DataRow(double.PositiveInfinity, 0.5)]
    public void NearestFaction_ANonFiniteClick_SelectsNothing(double x, double y)
    {
        // The click comes from the engine's mouse position and widget geometry; a non-finite one must fail
        // the distance gate, never select a faction.
        var gondor = new FactionInfo { Key = "gondor", MapX = 0.62, MapY = 0.64 };

        Assert.IsNull(FactionRoster.NearestFaction(new List<FactionInfo> { gondor }, x, y));
    }
}
