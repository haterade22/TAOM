using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CheatMenu;
using TAOM.Features.CoopInterop;
using TAOM.Features.SpecialResources;

namespace TAOM.Tests.Features.CheatMenu;

/// <summary>
/// The cheats behind the menu buttons and the taom.cheat_* commands (CheatMenuService): amounts checked before any engine
/// call, co-op refused before anything else, teleport only from a free party (CheatMenuPolicy) to a known settlement.
/// </summary>
[TestClass]
public class CheatMenuServiceTests
{
    private ICheatWorldAdapter _world = null!;
    private ISpecialResourceService _resources = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private CheatMenuService _service = null!;

    internal static TeleportState Free() => new(
        CampaignRunning: true, InMission: false, InSettlement: false, InMapEventOrEncounter: false, InSiege: false,
        InArmyNotLeader: false, IsPrisoner: false, PartyActive: true, AtSea: false, InFieldCamp: false);

    [TestInitialize]
    public void Setup()
    {
        _world = Substitute.For<ICheatWorldAdapter>();
        _resources = Substitute.For<ISpecialResourceService>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _service = new CheatMenuService(_world, _resources, _coop, _dedicated, Substitute.For<IModLogger>());
        _world.ReadTeleportState().Returns(Free());
    }

    // ---- co-op ----

    [TestMethod]
    public void EveryCheat_InACoopSession_IsRefusedAndTouchesNothing()
    {
        _coop.IsSessionActive.Returns(true);

        Assert.AreEqual(CheatOutcomeKind.CoopRefused, _service.GiveGold(100).Kind);
        Assert.AreEqual(CheatOutcomeKind.CoopRefused, _service.AddInfluence(100).Kind);
        Assert.AreEqual(CheatOutcomeKind.CoopRefused, _service.AddRenown(100).Kind);
        Assert.AreEqual(CheatOutcomeKind.CoopRefused, _service.AddTroops(false, 10).Kind);
        Assert.AreEqual(CheatOutcomeKind.CoopRefused, _service.Heal().Kind);
        Assert.AreEqual(CheatOutcomeKind.CoopRefused, _service.AddRelation("gondor", 20).Kind);
        Assert.AreEqual(CheatOutcomeKind.CoopRefused, _service.AddSpecialResource(100).Kind);
        Assert.AreEqual(CheatOutcomeKind.CoopRefused, _service.Teleport("town_a").Kind);
        Assert.AreEqual(0, _world.ReceivedCalls().Count(), "a refused cheat reads and writes nothing");
        Assert.AreEqual(0, _resources.ReceivedCalls().Count());
    }

    [TestMethod]
    public void IsSinglePlayer_DeferringOrDedicatedServer_False()
    {
        _coop.ShouldDeferToHost.Returns(true);
        Assert.IsFalse(_service.IsSinglePlayer);

        _coop.ShouldDeferToHost.Returns(false);
        _dedicated.IsDedicatedServer.Returns(true);
        Assert.IsFalse(_service.IsSinglePlayer);

        _dedicated.IsDedicatedServer.Returns(false);
        Assert.IsTrue(_service.IsSinglePlayer);
    }

    // ---- gold, influence, renown ----

    [TestMethod]
    public void GiveGold_InRange_GivesAndReportsTheNewTotal()
    {
        _world.GiveGold(10_000).Returns(12_345);

        var outcome = _service.GiveGold(10_000);

        Assert.IsTrue(outcome.Ok);
        Assert.AreEqual(12_345f, outcome.Number);
    }

    // #1297: these four results have no Name, so the shared "Done: {NAME} {NUMBER}" line read "Done:  25,000".
    [TestMethod]
    public void UnnamedResults_EachHaveTheirOwnWords()
    {
        _world.GiveGold(Arg.Any<int>()).Returns(12_345);
        _world.AddInfluence(Arg.Any<int>()).Returns(true);
        _world.AddRenown(Arg.Any<int>()).Returns(true);
        _world.HealMainParty().Returns(7);

        Assert.IsTrue(CheatMenuTexts.Describe(_service.GiveGold(100)).Value.StartsWith("{=taom_cheat_done_gold}"));
        Assert.IsTrue(CheatMenuTexts.Describe(_service.AddInfluence(100)).Value.StartsWith("{=taom_cheat_done_influence}"));
        Assert.IsTrue(CheatMenuTexts.Describe(_service.AddRenown(100)).Value.StartsWith("{=taom_cheat_done_renown}"));
        Assert.IsTrue(CheatMenuTexts.Describe(_service.Heal()).Value.StartsWith("{=taom_cheat_done_heal}"));
    }

    [TestMethod]
    public void RelationAndTeleportResults_HaveTheirOwnWords()
    {
        _world.AddRelationWithKingdom("gondor", 20).Returns(12);
        _world.SettlementName("town_minas_tirith").Returns("Minas Tirith");

        Assert.IsTrue(CheatMenuTexts.Describe(_service.AddRelation("gondor", 20)).Value.StartsWith("{=taom_cheat_done_relation}"));
        Assert.IsTrue(CheatMenuTexts.Describe(_service.Teleport("town_minas_tirith")).Value.StartsWith("{=taom_cheat_done_teleport}"));
    }

    [TestMethod]
    public void GiveGold_ZeroNegativeOrOverTheLimit_IsBadAndGivesNothing()
    {
        Assert.AreEqual(CheatOutcomeKind.BadAmount, _service.GiveGold(0).Kind);
        Assert.AreEqual(CheatOutcomeKind.BadAmount, _service.GiveGold(-5).Kind);
        Assert.AreEqual(CheatOutcomeKind.BadAmount, _service.GiveGold(CheatMenuService.MaxGold + 1).Kind);
        Assert.IsTrue(_service.GiveGold(CheatMenuService.MaxGold).Kind != CheatOutcomeKind.BadAmount, "the limit itself is allowed");
        _world.DidNotReceive().GiveGold(0);
        _world.DidNotReceive().GiveGold(-5);
    }

    [TestMethod]
    public void GiveGold_NoMainHero_NothingToDo()
    {
        _world.GiveGold(Arg.Any<int>()).Returns((int?)null);

        Assert.AreEqual(CheatOutcomeKind.NothingToDo, _service.GiveGold(10).Kind);
    }

    [TestMethod]
    public void AddInfluence_NoKingdom_NothingToDo_WithKingdom_Done()
    {
        _world.AddInfluence(100).Returns(false);
        Assert.AreEqual(CheatOutcomeKind.NothingToDo, _service.AddInfluence(100).Kind);

        _world.AddInfluence(100).Returns(true);
        Assert.IsTrue(_service.AddInfluence(100).Ok);
        Assert.AreEqual(CheatOutcomeKind.BadAmount, _service.AddInfluence(CheatMenuService.MaxInfluenceOrRenown + 1).Kind);
    }

    [TestMethod]
    public void AddRenown_InRange_Done_OutOfRange_Bad()
    {
        _world.AddRenown(100).Returns(true);

        Assert.IsTrue(_service.AddRenown(100).Ok);
        Assert.AreEqual(CheatOutcomeKind.BadAmount, _service.AddRenown(0).Kind);
        _world.DidNotReceive().AddRenown(0);
    }

    // ---- troops, heal ----

    [TestMethod]
    public void AddTroops_EliteOrBasic_AsksForThatTroopAndNamesIt()
    {
        _world.AddCultureTroops(true, 10).Returns("Tower Guard");

        var outcome = _service.AddTroops(elite: true, 10);

        Assert.IsTrue(outcome.Ok);
        Assert.AreEqual("Tower Guard", outcome.Name);
        _world.DidNotReceive().AddCultureTroops(false, Arg.Any<int>());
    }

    [TestMethod]
    public void AddTroops_CultureWithoutTheTroop_NothingToDo_TooMany_Bad()
    {
        _world.AddCultureTroops(false, 10).Returns((string?)null);

        Assert.AreEqual(CheatOutcomeKind.NothingToDo, _service.AddTroops(false, 10).Kind);
        Assert.AreEqual(CheatOutcomeKind.BadAmount, _service.AddTroops(false, CheatMenuService.MaxTroops + 1).Kind);
    }

    [TestMethod]
    public void Heal_ReportsHowManySoldiersWereHealed()
    {
        _world.HealMainParty().Returns(7);

        var outcome = _service.Heal();

        Assert.IsTrue(outcome.Ok);
        Assert.AreEqual(7f, outcome.Number);
    }

    // ---- relations ----

    [TestMethod]
    public void AddRelation_KnownKingdom_ChangesItsLords()
    {
        _world.AddRelationWithKingdom("gondor", 20).Returns(12);

        var outcome = _service.AddRelation("gondor", 20);

        Assert.IsTrue(outcome.Ok);
        Assert.AreEqual(12f, outcome.Number);
    }

    [TestMethod]
    public void AddRelation_NamesTheKingdomByItsDisplayName_OrItsIdWhenItHasNone()
    {
        // spkingdoms.xslt keeps vanilla ids (Gondor is empire_w), so the id is no name a player knows (#1317).
        _world.Kingdoms().Returns(new List<(string Id, string Name)> { ("empire_w", "Gondor") });
        _world.AddRelationWithKingdom(Arg.Any<string>(), 20).Returns(12);

        Assert.AreEqual("Gondor", _service.AddRelation("empire_w", 20).Name);
        Assert.AreEqual("eliminated_one", _service.AddRelation("eliminated_one", 20).Name);
    }

    [TestMethod]
    public void AddRelation_UnknownOrMissingKingdom_UnknownTarget()
    {
        _world.AddRelationWithKingdom("nope", 20).Returns((int?)null);

        Assert.AreEqual(CheatOutcomeKind.UnknownTarget, _service.AddRelation("nope", 20).Kind);
        Assert.AreEqual(CheatOutcomeKind.UnknownTarget, _service.AddRelation(" ", 20).Kind);
        _world.DidNotReceive().AddRelationWithKingdom(" ", Arg.Any<int>());
    }

    [TestMethod]
    public void AddRelation_ZeroOrPastTheLimit_Bad_NegativeWithinTheLimit_Allowed()
    {
        _world.AddRelationWithKingdom("gondor", -100).Returns(3);

        Assert.AreEqual(CheatOutcomeKind.BadAmount, _service.AddRelation("gondor", 0).Kind);
        Assert.AreEqual(CheatOutcomeKind.BadAmount, _service.AddRelation("gondor", 101).Kind);
        Assert.AreEqual(CheatOutcomeKind.BadAmount, _service.AddRelation("gondor", -101).Kind);
        Assert.IsTrue(_service.AddRelation("gondor", -100).Ok);
    }

    // ---- special resource ----

    [TestMethod]
    public void AddSpecialResource_GrantsThroughTheCappedServiceForTheMainHero()
    {
        _world.MainHeroIds().Returns(("main_hero", "gondor", "gondor_culture"));
        _resources.GrantAmount("main_hero", "gondor", "gondor_culture", 1000f)
            .Returns(new ResourceGrantResult(true, "marks", "Marks", 50f, 1050f, 10_000f));

        var outcome = _service.AddSpecialResource(1000f);

        Assert.IsTrue(outcome.Ok);
        Assert.AreEqual("Marks", outcome.Name);
        Assert.AreEqual(1050f, outcome.Number);
    }

    [TestMethod]
    public void AddSpecialResource_NoResourceForTheHero_NothingToDo()
    {
        _world.MainHeroIds().Returns(("main_hero", null, "x"));
        _resources.GrantAmount(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<float>())
            .Returns(ResourceGrantResult.None);

        Assert.AreEqual(CheatOutcomeKind.NothingToDo, _service.AddSpecialResource(1000f).Kind);
    }

    [TestMethod]
    public void AddSpecialResource_NaNZeroOrOverTheCap_BadAndNotGranted()
    {
        Assert.AreEqual(CheatOutcomeKind.BadAmount, _service.AddSpecialResource(float.NaN).Kind);
        Assert.AreEqual(CheatOutcomeKind.BadAmount, _service.AddSpecialResource(0f).Kind);
        Assert.AreEqual(CheatOutcomeKind.BadAmount, _service.AddSpecialResource(CheatMenuService.MaxResource + 1f).Kind);
        Assert.AreEqual(0, _resources.ReceivedCalls().Count());
    }

    // ---- teleport ----

    [TestMethod]
    public void Teleport_FreePartyAndKnownSettlement_MovesToTheGate()
    {
        _world.SettlementName("town_minas_tirith").Returns("Minas Tirith");

        var outcome = _service.Teleport("town_minas_tirith");

        Assert.IsTrue(outcome.Ok);
        Assert.AreEqual("Minas Tirith", outcome.Name);
        _world.Received(1).TeleportToGate("town_minas_tirith");
    }

    [TestMethod]
    public void Teleport_PartyInsideASettlement_RefusedAndNotMoved()
    {
        _world.ReadTeleportState().Returns(Free() with { InSettlement = true });
        _world.SettlementName(Arg.Any<string>()).Returns("Somewhere");

        var outcome = _service.Teleport("town_a");

        Assert.AreEqual(CheatOutcomeKind.TeleportRefused, outcome.Kind);
        Assert.AreEqual(TeleportRefusal.InSettlement, outcome.Refusal);
        _world.DidNotReceive().TeleportToGate(Arg.Any<string>());
    }

    [TestMethod]
    public void Teleport_UnknownOrEmptySettlement_UnknownTargetAndNotMoved()
    {
        _world.SettlementName("nowhere").Returns((string?)null);

        Assert.AreEqual(CheatOutcomeKind.UnknownTarget, _service.Teleport("nowhere").Kind);
        Assert.AreEqual(CheatOutcomeKind.UnknownTarget, _service.Teleport("").Kind);
        _world.DidNotReceive().TeleportToGate(Arg.Any<string>());
    }
}
