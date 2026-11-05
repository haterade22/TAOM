using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Duties;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// "Speak with your commander" — the option that makes the enlisted dialog surface reachable at
/// all. Reassignment, the quartermaster and in-person discharge were all shipped and all
/// unreachable, because nothing in the feature could open a conversation.
/// </summary>
[TestClass]
public class EnlistmentPlayerActionServiceTests
{
    private EnlistmentStore _store;
    private ICommanderLordAdapter _commander;
    private IMapConversationAdapter _conversation;
    private IDutyOrchestrationService _duties;
    private IMobilePartyAttachmentAdapter _attachment = null!;
    private IEncounterAdapter _encounter = null!;
    private IGameMenuAdapter _gameMenu = null!;
    private IModLogger _logger = null!;
    private EnlistmentPlayerActionService _sut;

    [TestInitialize]
    public void Setup()
    {
        var logger = _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(logger);
        _commander = Substitute.For<ICommanderLordAdapter>();
        _conversation = Substitute.For<IMapConversationAdapter>();
        _duties = Substitute.For<IDutyOrchestrationService>();

        _conversation.CanOpenConversation.Returns(true);
        _conversation.OpenWithHero(Arg.Any<string>()).Returns(true);
        _commander.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party", partyIsActive: true));

        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1";

        _attachment = Substitute.For<IMobilePartyAttachmentAdapter>();
        _gameMenu = Substitute.For<IGameMenuAdapter>();
        _encounter = Substitute.For<IEncounterAdapter>();
        _encounter.EnsureSettlementEncounter(Arg.Any<string>()).Returns(true);
        _sut = new EnlistmentPlayerActionService(
            _store, _commander, _conversation, _duties, Coop(), _attachment, _encounter, _gameMenu, logger);
    }

    [TestMethod]
    public void CanTalkToCommander_Serving_Opened()
    {
        Assert.AreEqual(TalkToCommanderResult.Opened, _sut.CanTalkToCommander());
    }

    [TestMethod]
    public void CanTalkToCommander_NotEnlisted_NotEnlisted()
    {
        _store.Record.State = EnlistmentState.NotEnlisted;
        Assert.AreEqual(TalkToCommanderResult.NotEnlisted, _sut.CanTalkToCommander());
    }

    [TestMethod]
    public void CanTalkToCommander_InBattle_InBattle()
    {
        // A conversation tears the seeded PlayerEncounter the battle service owns, and that
        // encounter is the only thing advancing the player's map event — losing it freezes the
        // battle with no way back.
        _store.Record.State = EnlistmentState.EnlistedBattle;
        Assert.AreEqual(TalkToCommanderResult.InBattle, _sut.CanTalkToCommander());
    }


    [DataTestMethod]
    [DataRow(false, true, false, false)]
    [DataRow(true, false, false, false)]
    [DataRow(true, true, true, false)]
    [DataRow(true, true, false, true)]
    public void CanTalkToCommander_CommanderUnfit_CommanderUnavailable(
        bool exists, bool alive, bool prisoner, bool partyless)
    {
        _commander.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(
            exists: exists, isAlive: alive, isPrisoner: prisoner,
            partyId: partyless ? null : "lord_party", partyIsActive: !partyless));

        Assert.AreEqual(TalkToCommanderResult.CommanderUnavailable, _sut.CanTalkToCommander());
    }

    [TestMethod]
    public void CanTalkToCommander_NotOnTheMap_NotOnMap()
    {
        // The engine's map-conversation entry dereferences `(ActiveState as MapState)` with no
        // null check, so "not on the map" is a crash, not a no-op. The adapter owns that guard.
        _conversation.CanOpenConversation.Returns(false);
        Assert.AreEqual(TalkToCommanderResult.NotOnMap, _sut.CanTalkToCommander());
    }

    [TestMethod]
    public void TalkToCommander_Serving_OpensWithTheCommander()
    {
        Assert.AreEqual(TalkToCommanderResult.Opened, _sut.TalkToCommander());
        _conversation.Received(1).OpenWithHero("lord_1");
    }

    [TestMethod]
    public void TalkToCommander_BattleStartedAfterTheConditionPassed_DoesNotOpen()
    {
        // THE pin for the re-check. A menu option's condition and its consequence are separated
        // by at least a frame; a commander battle starting in that gap is exactly the case where
        // acting on the stale answer costs the player the whole fight.
        _store.Record.State = EnlistmentState.EnlistedBattle;

        Assert.AreEqual(TalkToCommanderResult.InBattle, _sut.TalkToCommander());
        _conversation.DidNotReceive().OpenWithHero(Arg.Any<string>());
    }

    [TestMethod]
    public void TalkToCommander_EngineRefuses_ReportsUnavailableRatherThanClaimingSuccess()
    {
        _conversation.OpenWithHero(Arg.Any<string>()).Returns(false);

        Assert.AreEqual(TalkToCommanderResult.CommanderUnavailable, _sut.TalkToCommander());
    }

    [TestMethod]
    public void RequestDutyNow_DelegatesToTheOneOfferPath()
    {
        _duties.RequestDutyNow(10.0, 12.0).Returns(DutyRequestResult.DutyAssigned);

        Assert.AreEqual(DutyRequestResult.DutyAssigned, _sut.RequestDutyNow(10.0, 12.0));
        _duties.Received(1).RequestDutyNow(10.0, 12.0);
    }

    // ---- Shore leave: the pass hands the player a VANILLA settlement menu (issue #510) --------
    // The follow path put him inside with EnterSettlementAction alone, so there is no
    // PlayerEncounter and no LocationEncounter. Releasing the town menu in that state is a CTD
    // on "Wait here for some time", the tavern, the arena and the town-centre walk. The pass must
    // establish the encounter first, and must refuse itself if it cannot.

    [TestMethod]
    public void TakeTownLeave_EstablishesTheSettlementEncounterBeforeGrantingThePass()
    {
        InsideSettlement("town_EW1");

        Assert.IsTrue(_sut.TakeTownLeave());

        _encounter.Received(1).EnsureSettlementEncounter("town_EW1");
        Assert.IsTrue(_store.Record.OnTownLeave);
    }

    [TestMethod]
    public void TakeTownLeave_EncounterCannotBeEstablished_RefusesThePass()
    {
        InsideSettlement("town_EW1");
        _encounter.EnsureSettlementEncounter("town_EW1").Returns(false);

        Assert.IsFalse(_sut.TakeTownLeave());
        Assert.IsFalse(_store.Record.OnTownLeave);
    }

    [TestMethod]
    public void TakeTownLeave_NotInASettlement_RefusesWithoutTouchingTheEncounter()
    {
        _attachment.GetPresenceFlags().Returns(new PlayerPresenceFlags(mainPartyExists: true));

        Assert.IsFalse(_sut.TakeTownLeave());
        _encounter.DidNotReceive().EnsureSettlementEncounter(Arg.Any<string>());
        Assert.IsFalse(_store.Record.OnTownLeave);
    }

    // ---- A discharge during load (#828): the saved wait menu comes back with nobody serving ----

    [TestMethod]
    public void IsServing_NotEnlisted_ReturnsFalse()
    {
        _store.Record.State = EnlistmentState.NotEnlisted;
        Assert.IsFalse(_sut.IsServing);
    }

    [TestMethod]
    public void IsServing_Enlisted_ReturnsTrue()
    {
        Assert.IsTrue(_sut.IsServing);
    }

    private const string ServiceMenu = "taom_enlistment_service_wait";

    private void NotServing() => _store.Record.State = EnlistmentState.NotEnlisted;

    private void InsideSettlementInMapEvent(string settlementId) =>
        _attachment.GetPresenceFlags().Returns(new PlayerPresenceFlags(
            mainPartyExists: true, isActive: true, isVisible: true, isInMapEvent: true, settlementId: settlementId));

    [TestMethod]
    public void LeaveAfterDischarge_StillServing_ReentersServiceMenu()
    {
        _sut.LeaveAfterDischarge();

        _gameMenu.Received(1).SwitchTo(ServiceMenu);
        _gameMenu.DidNotReceive().ExitToLast();
    }

    [TestMethod]
    public void LeaveAfterDischarge_OnMap_ExitsToLast()
    {
        NotServing();
        _attachment.GetPresenceFlags().Returns(new PlayerPresenceFlags(mainPartyExists: true));

        _sut.LeaveAfterDischarge();

        _gameMenu.Received(1).ExitToLast();
        _encounter.DidNotReceive().EnsureSettlementEncounter(Arg.Any<string>());
        _attachment.DidNotReceive().LeaveSettlement();
    }

    [TestMethod]
    public void LeaveAfterDischarge_InSettlementDuringMapEvent_ExitsWithoutTouchingSettlement()
    {
        NotServing();
        InsideSettlementInMapEvent("town_EW1");

        _sut.LeaveAfterDischarge();

        _gameMenu.Received(1).ExitToLast();
        _encounter.DidNotReceive().EnsureSettlementEncounter(Arg.Any<string>());
        _attachment.DidNotReceive().LeaveSettlement();
    }

    [TestMethod]
    public void LeaveAfterDischarge_InsideSettlement_OpensItsMenu()
    {
        NotServing();
        InsideSettlement("town_EW1");
        _gameMenu.CurrentSettlementMenuId.Returns("town");
        _gameMenu.EnsureMenuOpen("town").Returns(true);

        _sut.LeaveAfterDischarge();

        _encounter.Received(1).EnsureSettlementEncounter("town_EW1");
        _gameMenu.Received(1).EnsureMenuOpen("town");
        _gameMenu.DidNotReceive().ExitToLast();
        _attachment.DidNotReceive().LeaveSettlement();
    }

    [TestMethod]
    public void LeaveAfterDischarge_SettlementMenuWontOpen_FinishesWalksOutAndExits()
    {
        NotServing();
        InsideSettlement("town_EW1");
        _gameMenu.CurrentSettlementMenuId.Returns("town");
        _gameMenu.EnsureMenuOpen("town").Returns(false);
        _encounter.Finish(true).Returns(true);
        _attachment.LeaveSettlement().Returns(true);

        _sut.LeaveAfterDischarge();

        Received.InOrder(() =>
        {
            _encounter.Finish(true);
            _attachment.LeaveSettlement();
            _gameMenu.ExitToLast();
        });
    }

    [TestMethod]
    public void LeaveAfterDischarge_NullSettlementMenuId_FinishesWalksOutAndExits()
    {
        NotServing();
        InsideSettlement("town_EW1");
        _gameMenu.CurrentSettlementMenuId.Returns((string)null);
        _encounter.Finish(true).Returns(true);
        _attachment.LeaveSettlement().Returns(true);

        _sut.LeaveAfterDischarge();

        Received.InOrder(() =>
        {
            _encounter.Finish(true);
            _attachment.LeaveSettlement();
            _gameMenu.ExitToLast();
        });
    }

    [TestMethod]
    public void LeaveAfterDischarge_EncounterFails_WalksOutWithoutFinish()
    {
        NotServing();
        InsideSettlement("town_EW1");
        _encounter.EnsureSettlementEncounter("town_EW1").Returns(false);
        _attachment.LeaveSettlement().Returns(true);

        _sut.LeaveAfterDischarge();

        _encounter.DidNotReceive().Finish(Arg.Any<bool>());
        _attachment.Received(1).LeaveSettlement();
        _gameMenu.Received(1).ExitToLast();
    }

    [TestMethod]
    public void LeaveAfterDischarge_WalkOutFails_KeepsServiceMenu()
    {
        NotServing();
        InsideSettlement("town_EW1");
        _encounter.EnsureSettlementEncounter("town_EW1").Returns(false);
        _attachment.LeaveSettlement().Returns(false);

        _sut.LeaveAfterDischarge();

        _gameMenu.Received(1).EnsureMenuOpen(ServiceMenu);
        _gameMenu.DidNotReceive().ExitToLast();
    }

    [TestMethod]
    public void LeaveAfterDischarge_EncounterStillLiveInMapEvent_WarnsAndExits()
    {
        NotServing();
        _attachment.GetPresenceFlags().Returns(new PlayerPresenceFlags(mainPartyExists: true, isInMapEvent: true));
        _encounter.HasCurrent.Returns(true);

        _sut.LeaveAfterDischarge();

        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("encounter")));
        _logger.DidNotReceiveWithAnyArgs().LogError(default!);
        _gameMenu.Received(1).ExitToLast();
    }

    [TestMethod]
    public void LeaveAfterDischarge_EncounterStillLiveWithoutMapEvent_LogsErrorAndExits()
    {
        NotServing();
        _attachment.GetPresenceFlags().Returns(new PlayerPresenceFlags(mainPartyExists: true));
        _encounter.HasCurrent.Returns(true);

        _sut.LeaveAfterDischarge();

        _logger.Received().LogError(Arg.Is<string>(m => m.Contains("no map event")));
        _gameMenu.Received(1).ExitToLast();
    }

    private void InsideSettlement(string settlementId) =>
        _attachment.GetPresenceFlags().Returns(new PlayerPresenceFlags(
            mainPartyExists: true, isActive: true, isVisible: true, settlementId: settlementId));

    private static ICoopSessionProvider Coop()
    {
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(true);
        return coop;
    }
}
