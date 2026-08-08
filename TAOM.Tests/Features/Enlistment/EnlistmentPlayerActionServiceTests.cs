using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
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
    private EnlistmentPlayerActionService _sut;

    [TestInitialize]
    public void Setup()
    {
        var logger = Substitute.For<IModLogger>();
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

        _sut = new EnlistmentPlayerActionService(_store, _commander, _conversation, _duties, logger);
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

    [TestMethod]
    public void CanTalkToCommander_OnDuty_OnDuty()
    {
        // Detached, the player can ride up and click the lord like anyone else. A teleporting
        // menu option here would be strictly worse than the thing it replaces.
        _store.Record.State = EnlistmentState.EnlistedDetachedOnDuty;
        Assert.AreEqual(TalkToCommanderResult.OnDuty, _sut.CanTalkToCommander());
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
}
