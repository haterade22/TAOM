using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class ServiceBattleServiceTests
{
    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentStateMachine _machine = null!;
    private IEncounterAdapter _encounter = null!;
    private IMobilePartyAttachmentAdapter _partyAdapter = null!;
    private ServiceAttachmentService _attachment = null!;
    private IGameMenuAdapter _gameMenu = null!;
    private ServiceBattleService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        _machine = new EnlistmentStateMachine(_store, _logger);
        _encounter = Substitute.For<IEncounterAdapter>();
        _partyAdapter = Substitute.For<IMobilePartyAttachmentAdapter>();
        _partyAdapter.RestorePresence().Returns(true);
        _partyAdapter.ParkNear(Arg.Any<string>()).Returns(true);
        _partyAdapter.SyncPositionTo(Arg.Any<string>()).Returns(true);
        _attachment = new ServiceAttachmentService(_partyAdapter, _logger);
        _gameMenu = Substitute.For<IGameMenuAdapter>();
        // Default to a working menu switch — the service now treats a failed switch as a join
        // failure and rolls back, so an unstubbed (false) default would fail every happy path.
        _gameMenu.EnsureMenuOpen(Arg.Any<string>()).Returns(true);
        _service = new ServiceBattleService(_store, _machine, _encounter, _attachment, _gameMenu, _logger);

        _encounter.GetPartyBattleSide("lord_party_1").Returns(PartyBattleSide.Defender);
        _encounter.IsCommanderBattleJoinable("lord_party_1", PartyBattleSide.Defender).Returns(true);
        _encounter.EnsureEncounterAgainst("lord_party_1").Returns(true);
        _encounter.JoinBattle(PartyBattleSide.Defender).Returns(true);
        _encounter.Finish(Arg.Any<bool>()).Returns(true);
    }

    private void MakeEnlisted(EnlistmentState state = EnlistmentState.EnlistedAttached)
    {
        _store.Record.State = state;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
    }

    [TestMethod]
    public void BattleStarted_NotAttachedState_NoOp()
    {
        MakeEnlisted(EnlistmentState.EnlistedDetachedOnDuty);

        _service.OnCommanderBattleStarted("lord_party_1");

        Assert.AreEqual(EnlistmentState.EnlistedDetachedOnDuty, _store.Record.State);
        _encounter.DidNotReceive().JoinBattle(Arg.Any<PartyBattleSide>());
    }

    [TestMethod]
    public void BattleStarted_TransitionsToBattleBeforeMenuWork()
    {
        // The ordering contract that keeps the menu guard from eating battle menus:
        // state flips to EnlistedBattle (redirect-exempt) BEFORE any encounter/menu push.
        MakeEnlisted();
        EnlistmentState stateAtJoin = default;
        _encounter.JoinBattle(PartyBattleSide.Defender).Returns(_ =>
        {
            stateAtJoin = _store.Record.State;
            return true;
        });

        _service.OnCommanderBattleStarted("lord_party_1");

        Assert.AreEqual(EnlistmentState.EnlistedBattle, stateAtJoin);
        Assert.AreEqual(EnlistmentState.EnlistedBattle, _store.Record.State);
    }

    [TestMethod]
    public void BattleStarted_RestoresPresenceJoinsCommanderSideAndOpensEncounterMenu()
    {
        MakeEnlisted();

        _service.OnCommanderBattleStarted("lord_party_1");

        _partyAdapter.Received(1).RestorePresence();
        _encounter.Received(1).JoinBattle(PartyBattleSide.Defender);
        _gameMenu.Received(1).EnsureMenuOpen("encounter");
    }

    [TestMethod]
    public void BattleStarted_RestoresPresenceBeforeTouchingTheEncounter()
    {
        // Regression (2026-08-07): the engine skips inactive parties in encounter detection, and
        // a party that gains a MapEventSide without a live encounter freezes its map event for
        // good. Presence must be live before any encounter work, never after.
        MakeEnlisted();
        var presenceRestoredFirst = false;
        _encounter.EnsureEncounterAgainst("lord_party_1").Returns(_ =>
        {
            presenceRestoredFirst = _partyAdapter.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IMobilePartyAttachmentAdapter.RestorePresence));
            return true;
        });

        _service.OnCommanderBattleStarted("lord_party_1");

        Assert.IsTrue(presenceRestoredFirst, "presence must be restored before the encounter is seeded");
    }

    [TestMethod]
    public void BattleStarted_SyncsPositionToCommanderBeforeJoining()
    {
        // The restored party must sit on the commander to be inside the encounter radius.
        MakeEnlisted();

        _service.OnCommanderBattleStarted("lord_party_1");

        _partyAdapter.Received(1).SyncPositionTo("lord_1_1");
    }

    [TestMethod]
    public void BattleStarted_SeedsEncounterAgainstCommanderEvent()
    {
        MakeEnlisted();

        _service.OnCommanderBattleStarted("lord_party_1");

        _encounter.Received(1).EnsureEncounterAgainst("lord_party_1");
        _encounter.Received(1).JoinBattle(PartyBattleSide.Defender);
    }

    [TestMethod]
    public void BattleStarted_EncounterCannotBeSeeded_RollsBackWithoutJoining()
    {
        // Siege case: if the encounter cannot be pointed at the commander's map event there is
        // nothing to join, and JoinBattle would NRE on a null PlayerEncounter.Current.
        MakeEnlisted();
        _encounter.EnsureEncounterAgainst("lord_party_1").Returns(false);

        _service.OnCommanderBattleStarted("lord_party_1");

        _encounter.DidNotReceive().JoinBattle(Arg.Any<PartyBattleSide>());
        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _partyAdapter.Received(1).ParkNear("lord_1_1");
    }

    [TestMethod]
    public void BattleStarted_AttackerSide_LeavesBesiegedSettlementBeforeJoining()
    {
        // Siege assault as an attacker: the player can be held inside a settlement the encounter
        // is against, and must leave it before joining.
        MakeEnlisted();
        _encounter.GetPartyBattleSide("lord_party_1").Returns(PartyBattleSide.Attacker);
        _encounter.IsCommanderBattleJoinable("lord_party_1", PartyBattleSide.Attacker).Returns(true);
        _encounter.JoinBattle(PartyBattleSide.Attacker).Returns(true);

        _service.OnCommanderBattleStarted("lord_party_1");

        Received.InOrder(() =>
        {
            _encounter.EnsureEncounterAgainst("lord_party_1");
            _encounter.LeaveSettlementIfUnderSiege();
            _encounter.JoinBattle(PartyBattleSide.Attacker);
        });
    }

    [TestMethod]
    public void BattleStarted_DefenderSide_DoesNotLeaveTheSettlement()
    {
        // Regression (Codex P1, 2026-08-07): MapEvent.AddInvolvedPartyInternal rewrites a siege
        // ASSAULT to SiegeOutside when a defender is added with CurrentSettlement == null. Leaving
        // before a defender join would convert the battle type for every participant.
        MakeEnlisted();

        _service.OnCommanderBattleStarted("lord_party_1");

        _encounter.Received(1).JoinBattle(PartyBattleSide.Defender);
        _encounter.DidNotReceive().LeaveSettlementIfUnderSiege();
    }

    [TestMethod]
    public void BattleStarted_EncounterNotSeeded_DoesNotTryToLeaveSettlement()
    {
        MakeEnlisted();
        _encounter.EnsureEncounterAgainst("lord_party_1").Returns(false);

        _service.OnCommanderBattleStarted("lord_party_1");

        _encounter.DidNotReceive().LeaveSettlementIfUnderSiege();
    }

    [TestMethod]
    public void BattleStarted_CommanderSideUnresolvable_NoOpStaysAttached()
    {
        MakeEnlisted();
        _encounter.GetPartyBattleSide("lord_party_1").Returns((PartyBattleSide?)null);

        _service.OnCommanderBattleStarted("lord_party_1");

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _partyAdapter.DidNotReceive().RestorePresence();
    }

    [TestMethod]
    public void BattleStarted_BattleNotJoinable_NoOpStaysAttached()
    {
        MakeEnlisted();
        _encounter.IsCommanderBattleJoinable("lord_party_1", PartyBattleSide.Defender).Returns(false);

        _service.OnCommanderBattleStarted("lord_party_1");

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _partyAdapter.DidNotReceive().RestorePresence();
    }

    [TestMethod]
    public void BattleStarted_JoinFails_RolledBackToParkedAttached()
    {
        // The donor's wasHiddenServiceMode rollback, formalized: a failed join must never
        // strand a visible, active main party in battle state.
        MakeEnlisted();
        _encounter.JoinBattle(PartyBattleSide.Defender).Returns(false);

        _service.OnCommanderBattleStarted("lord_party_1");

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _partyAdapter.Received(1).ParkNear("lord_1_1");
        _gameMenu.DidNotReceive().EnsureMenuOpen("encounter");
    }

    [TestMethod]
    public void BattleStarted_MenuSwitchFails_RollsBackInsteadOfFreezingTheMapEvent()
    {
        // Regression (deep-review 2026-08-07, HIGH): a verified join gives MainParty a real
        // MapEventSide, but the engine only advances the PLAYER's map event through
        // PlayerEncounter.Update, which runs from the "encounter" menu. If that menu never opens
        // the event freezes forever — and the hourly recovery cannot see it, because Assess reads
        // player.IsInMapEvent as true and reports Attached rather than BattleJoinRequired.
        MakeEnlisted();
        _gameMenu.EnsureMenuOpen("encounter").Returns(false);

        _service.OnCommanderBattleStarted("lord_party_1");

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _encounter.Received(1).Finish(false);
        _partyAdapter.Received(1).ParkNear("lord_1_1");
    }

    [TestMethod]
    public void BattleStarted_MenuSwitchSucceeds_StaysInBattle()
    {
        MakeEnlisted();
        _gameMenu.EnsureMenuOpen("encounter").Returns(true);

        _service.OnCommanderBattleStarted("lord_party_1");

        Assert.AreEqual(EnlistmentState.EnlistedBattle, _store.Record.State);
        _encounter.DidNotReceive().Finish(Arg.Any<bool>());
    }

    [TestMethod]
    public void BattleStarted_JoinFails_FinishesTheOrphanedEncounter()
    {
        // Regression (2026-08-07): a PlayerEncounter left behind by a failed join blocks the main
        // party from ever entering another encounter (EncounterManager gates on Current == null),
        // which is how the commander ended up unable to start any battle at all.
        MakeEnlisted();
        _encounter.JoinBattle(PartyBattleSide.Defender).Returns(false);

        _service.OnCommanderBattleStarted("lord_party_1");

        _encounter.Received(1).Finish(false);
    }

    [TestMethod]
    public void TryJoinCommanderBattle_RecoveryPath_JoinsLikeTheEventPath()
    {
        // Regression (2026-08-07): IEnlistmentReconciler.BattleJoinRequested had no subscriber at
        // all, so the hourly "commander is fighting and the player is not" recovery did nothing.
        MakeEnlisted();

        _service.TryJoinCommanderBattle("lord_party_1");

        _encounter.Received(1).JoinBattle(PartyBattleSide.Defender);
        _gameMenu.Received(1).EnsureMenuOpen("encounter");
        Assert.AreEqual(EnlistmentState.EnlistedBattle, _store.Record.State);
    }

    [TestMethod]
    public void TryJoinCommanderBattle_AlreadyInBattleState_NoOp()
    {
        MakeEnlisted(EnlistmentState.EnlistedBattle);

        _service.TryJoinCommanderBattle("lord_party_1");

        _encounter.DidNotReceive().JoinBattle(Arg.Any<PartyBattleSide>());
    }

    [TestMethod]
    public void BattleEnded_NoEncounterLeft_ReturnsToAttachedAndParks()
    {
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        _encounter.HasCurrent.Returns(false);

        _service.OnCommanderBattleEnded();

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _partyAdapter.Received(1).ParkNear("lord_1_1");
    }

    [TestMethod]
    public void BattleEnded_ReopensTheServiceWaitMenu()
    {
        // Regression (in-game 2026-08-07): "after battle I was left behind and the option menu
        // isn't here". Re-parking alone leaves the player on the open map with no menu and no way
        // to act. The only wait-menu re-assert was OnConversationEnded, which never fires on the
        // battle path. The donor re-activates its wait menu on every return-to-service path.
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        _encounter.HasCurrent.Returns(false);
        _gameMenu.CurrentMenuId.Returns("encounter");

        _service.OnCommanderBattleEnded();

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _gameMenu.Received(1).EnsureMenuOpen(EnlistmentMenuService.ServiceWaitMenuId);
    }

    [TestMethod]
    public void BattleEnded_AlreadyOnWaitMenu_DoesNotReopenIt()
    {
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        _encounter.HasCurrent.Returns(false);
        _gameMenu.CurrentMenuId.Returns(EnlistmentMenuService.ServiceWaitMenuId);

        _service.OnCommanderBattleEnded();

        _gameMenu.DidNotReceive().EnsureMenuOpen(EnlistmentMenuService.ServiceWaitMenuId);
    }

    [TestMethod]
    public void BattleStarted_JoinFails_ReopensTheServiceWaitMenu()
    {
        // The rollback returns to parked service too, so it owes the player a menu as well.
        MakeEnlisted();
        _encounter.JoinBattle(PartyBattleSide.Defender).Returns(false);
        _gameMenu.CurrentMenuId.Returns((string)null);

        _service.OnCommanderBattleStarted("lord_party_1");

        _gameMenu.Received(1).EnsureMenuOpen(EnlistmentMenuService.ServiceWaitMenuId);
    }

    [TestMethod]
    public void BattleEnded_EncounterStillOpen_LeavesBattleStateForLootFlow()
    {
        // Loot/aftermath menus run inside the still-open encounter; flipping to Attached
        // here would let the menu guard eat them. The reconciler completes the return.
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        _encounter.HasCurrent.Returns(true);

        _service.OnCommanderBattleEnded();

        Assert.AreEqual(EnlistmentState.EnlistedBattle, _store.Record.State);
        _partyAdapter.DidNotReceive().ParkNear(Arg.Any<string>());
    }

    [TestMethod]
    public void BattleEnded_NotInBattleState_NoOp()
    {
        MakeEnlisted();

        _service.OnCommanderBattleEnded();

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _partyAdapter.DidNotReceive().ParkNear(Arg.Any<string>());
    }
}
