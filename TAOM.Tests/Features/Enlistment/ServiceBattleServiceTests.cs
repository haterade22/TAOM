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
    private IArmyMembershipAdapter _army = null!;
    private IServiceDiplomacyService _diplomacy = null!;
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
        _attachment = new ServiceAttachmentService(_partyAdapter, Substitute.For<IGameMenuAdapter>(), _logger);
        _gameMenu = Substitute.For<IGameMenuAdapter>();
        // Default to a working menu switch — the service now treats a failed switch as a join
        // failure and rolls back, so an unstubbed (false) default would fail every happy path.
        _gameMenu.EnsureMenuOpen(Arg.Any<string>()).Returns(true);
        _army = Substitute.For<IArmyMembershipAdapter>();
        _army.JoinCommanderArmy(Arg.Any<string>()).Returns(true);
        _army.LeaveArmy().Returns(true);
        // Default: the player HAS the battle team-merge. FlushArmyLeaveAfterBattle gates on this,
        // so an unstubbed (false) default would make every deferred-detach test vacuously pass.
        _army.IsInArmy.Returns(true);
        _diplomacy = Substitute.For<IServiceDiplomacyService>();
        _diplomacy.MirrorWarAtJoin(Arg.Any<string>()).Returns(true);
        _service = new ServiceBattleService(_store, _machine, _encounter, _attachment, _gameMenu, new EncounterOwnershipPolicy(), _army, _diplomacy, _logger);
        _encounter.GetOpposingLeaderFactionId(Arg.Any<string>()).Returns((string)null!);

        _encounter.GetPartyBattleSide("lord_party_1").Returns(PartyBattleSide.Defender);
        _encounter.IsCommanderBattleJoinable("lord_party_1", PartyBattleSide.Defender).Returns(true);
        _encounter.EnsureEncounterAgainst("lord_party_1").Returns(true);
        _encounter.JoinBattle(PartyBattleSide.Defender).Returns(true);
        _encounter.Finish(Arg.Any<bool>()).Returns(true);
        // Default shape: the live encounter is the commander's, so the rollback owns it.
        _encounter.GetOwnership(Arg.Any<string>()).Returns(new EncounterOwnershipSnapshot(
            hasEncounter: true, hasEncounteredMobileParty: true,
            encounteredPartyId: "lord_party_1", encounteredPartyIsCommanderRelated: true));
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

    // ---- #831: village hostile actions are not battles to join ------------------------------

    private void CommanderRaids()
    {
        _encounter.GetPartyBattleSide("lord_party_1").Returns(PartyBattleSide.Attacker);
        _encounter.IsCommanderBattleJoinable("lord_party_1", PartyBattleSide.Attacker).Returns(true);
        _encounter.JoinBattle(PartyBattleSide.Attacker).Returns(true);
        _encounter.IsPartyRaiding("lord_party_1").Returns(true);
    }

    [TestMethod]
    public void BattleStarted_CommanderRaiding_SkipsWithNoTransitionJoinOrArmyChange()
    {
        MakeEnlisted();
        CommanderRaids();
        _encounter.IsMainPartyInMapEvent.Returns(false);

        _service.OnCommanderBattleStarted("lord_party_1");

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _encounter.DidNotReceive().JoinBattle(Arg.Any<PartyBattleSide>());
        _encounter.DidNotReceive().EnsureEncounterAgainst(Arg.Any<string>());
        _army.DidNotReceive().JoinCommanderArmy(Arg.Any<string>());
        _army.DidNotReceive().LeaveArmy();
        _partyAdapter.DidNotReceive().RestorePresence();
        _gameMenu.DidNotReceive().EnsureMenuOpen(Arg.Any<string>());
        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains("commander raiding: not a battle to join")));
        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void BattleStarted_CommanderRaiding_ButPlayerAlreadyInAMapEvent_StillJoins()
    {
        // The propagation edge: the player is already in a map event (any event), so the floor must
        // not strand the existing join.
        MakeEnlisted();
        CommanderRaids();
        _encounter.IsMainPartyInMapEvent.Returns(true);

        _service.OnCommanderBattleStarted("lord_party_1");

        _encounter.Received(1).JoinBattle(PartyBattleSide.Attacker);
    }

    [TestMethod]
    public void BattleStarted_CommanderNotRaiding_Joins()
    {
        MakeEnlisted();
        CommanderRaids();
        _encounter.IsPartyRaiding("lord_party_1").Returns(false);
        _encounter.IsMainPartyInMapEvent.Returns(false);

        _service.OnCommanderBattleStarted("lord_party_1");

        _encounter.Received(1).JoinBattle(PartyBattleSide.Attacker);
    }

    // ---- #832: top up a missing war before the join ----

    [TestMethod]
    public void BattleStarted_TopsUpTheWarBeforeJoining()
    {
        MakeEnlisted();
        _encounter.GetOpposingLeaderFactionId("lord_party_1").Returns("mordor");

        _service.OnCommanderBattleStarted("lord_party_1");

        Received.InOrder(() =>
        {
            _diplomacy.MirrorWarAtJoin("mordor");
            _encounter.JoinBattle(PartyBattleSide.Defender);
        });
    }

    [TestMethod]
    public void BattleStarted_RefusedTopUp_StillJoins()
    {
        MakeEnlisted();
        _encounter.GetOpposingLeaderFactionId("lord_party_1").Returns("mordor");
        _diplomacy.MirrorWarAtJoin("mordor").Returns(false);

        _service.OnCommanderBattleStarted("lord_party_1");

        _encounter.Received(1).JoinBattle(PartyBattleSide.Defender);
    }

    [TestMethod]
    public void BattleStarted_EveryRefusedTopUpIsLogged()
    {
        MakeEnlisted();
        _encounter.GetOpposingLeaderFactionId("lord_party_1").Returns("mordor");
        _diplomacy.MirrorWarAtJoin("mordor").Returns(false);

        _service.OnCommanderBattleStarted("lord_party_1");
        _store.Record.State = EnlistmentState.EnlistedAttached;
        _service.OnCommanderBattleStarted("lord_party_1");

        _logger.Received(2).LogInfo(Arg.Is<string>(m => m.Contains("war top-up refused") && m.Contains("mordor")));
        _encounter.Received(2).JoinBattle(PartyBattleSide.Defender);
    }

    // ---- #960: a vassal sits out a battle his kingdom is not at war in ----

    [TestMethod]
    public void BattleStarted_VassalSitOut_DoesNotTransitionOrJoin()
    {
        MakeEnlisted();
        _encounter.GetOpposingLeaderFactionId("lord_party_1").Returns("mordor");
        _diplomacy.ShouldSitOutAsVassal("mordor").Returns(true);
        _diplomacy.MirrorWarAtJoin("mordor").Returns(false);
        _encounter.IsMainPartyInMapEvent.Returns(false);

        _service.OnCommanderBattleStarted("lord_party_1");

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _encounter.DidNotReceive().JoinBattle(Arg.Any<PartyBattleSide>());
        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains("sitting this battle out") && m.Contains("mordor")));
        _diplomacy.DidNotReceiveWithAnyArgs().MirrorWarAtJoin(default!);
        _logger.DidNotReceive().LogInfo(Arg.Is<string>(m => m.Contains("joining anyway")));
    }

    [TestMethod]
    public void HourlyRecovery_VassalSitOut_DoesNotJoin()
    {
        MakeEnlisted();
        _encounter.GetOpposingLeaderFactionId("lord_party_1").Returns("mordor");
        _diplomacy.ShouldSitOutAsVassal("mordor").Returns(true);
        _diplomacy.MirrorWarAtJoin("mordor").Returns(false);
        _encounter.IsMainPartyInMapEvent.Returns(false);

        _service.TryJoinCommanderBattle("lord_party_1");

        _encounter.DidNotReceive().JoinBattle(Arg.Any<PartyBattleSide>());
        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _diplomacy.DidNotReceiveWithAnyArgs().MirrorWarAtJoin(default!);
        _logger.DidNotReceive().LogInfo(Arg.Is<string>(m => m.Contains("joining anyway")));
    }

    [TestMethod]
    public void BattleStarted_NoVassalSitOut_Joins()
    {
        MakeEnlisted();
        _encounter.GetOpposingLeaderFactionId("lord_party_1").Returns("mordor");
        _diplomacy.ShouldSitOutAsVassal("mordor").Returns(false);

        _service.OnCommanderBattleStarted("lord_party_1");

        _encounter.Received(1).JoinBattle(PartyBattleSide.Defender);
    }

    [TestMethod]
    public void BattleStarted_VassalSitOut_ButPlayerAlreadyInAMapEvent_FallsThrough()
    {
        MakeEnlisted();
        _encounter.GetOpposingLeaderFactionId("lord_party_1").Returns("mordor");
        _diplomacy.ShouldSitOutAsVassal("mordor").Returns(true);
        _encounter.IsMainPartyInMapEvent.Returns(true);

        _service.OnCommanderBattleStarted("lord_party_1");

        _encounter.Received(1).JoinBattle(PartyBattleSide.Defender);
    }

    [TestMethod]
    public void BattleStarted_EmptyFoeRead_JoinsWithoutAskingTheSitOut()
    {
        MakeEnlisted();
        _encounter.GetOpposingLeaderFactionId("lord_party_1").Returns((string)null!);
        _diplomacy.ShouldSitOutAsVassal(Arg.Any<string>()).Returns(true);

        _service.OnCommanderBattleStarted("lord_party_1");

        _diplomacy.DidNotReceiveWithAnyArgs().ShouldSitOutAsVassal(default!);
        _encounter.Received(1).JoinBattle(PartyBattleSide.Defender);
    }

    [TestMethod]
    public void BattleStarted_NoOpposingFaction_NoTopUp()
    {
        MakeEnlisted();
        _encounter.GetOpposingLeaderFactionId("lord_party_1").Returns((string)null!);

        _service.OnCommanderBattleStarted("lord_party_1");

        _diplomacy.DidNotReceiveWithAnyArgs().MirrorWarAtJoin(default!);
        _encounter.Received(1).JoinBattle(PartyBattleSide.Defender);
    }

    [TestMethod]
    public void BattleStarted_RaidFloorSkips_NoTopUp()
    {
        MakeEnlisted();
        CommanderRaids();
        _encounter.GetOpposingLeaderFactionId("lord_party_1").Returns("mordor");
        _encounter.IsMainPartyInMapEvent.Returns(false);

        _service.OnCommanderBattleStarted("lord_party_1");

        _diplomacy.DidNotReceiveWithAnyArgs().MirrorWarAtJoin(default!);
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

    /// <summary>
    /// The real shape of a landed join whose encounter menu failed (#1021): no army, the join put
    /// the player in the commander's map event, the encounter's party is the opposing leader.
    /// </summary>
    private void JoinLandsButMenuFails(string currentMenuId, bool army = false)
    {
        MakeEnlisted();
        _army.JoinCommanderArmy(Arg.Any<string>()).Returns(army);
        _army.IsInArmy.Returns(army);
        _encounter.IsMainPartyInMapEvent.Returns(true);
        _encounter.GetOwnership(Arg.Any<string>()).Returns(new EncounterOwnershipSnapshot(
            hasEncounter: true, hasEncounteredMobileParty: true,
            encounteredPartyId: "enemy_lord_party", encounteredPartyIsCommanderRelated: false,
            playerInMapEvent: true));
        _gameMenu.EnsureMenuOpen("encounter").Returns(false);
        _gameMenu.CurrentMenuId.Returns(currentMenuId);
    }

    [TestMethod]
    public void BattleStarted_JoinLandsButMenuFails_StaysInBattleAndHandsTheMenuToTheGame()
    {
        // The engine opens `encounter` itself when no menu is up, and the player is already in his
        // map event, so a rollback could only freeze the battle (unmerged) or pull him out of it.
        JoinLandsButMenuFails(EnlistmentMenuService.ServiceWaitMenuId);

        _service.OnCommanderBattleStarted("lord_party_1");

        Assert.AreEqual(EnlistmentState.EnlistedBattle, _store.Record.State);
        _gameMenu.Received(1).ExitToLast();
        _encounter.DidNotReceive().Finish(Arg.Any<bool>());
        _partyAdapter.DidNotReceive().ParkNear(Arg.Any<string>());
        _army.DidNotReceive().LeaveArmy();
        _gameMenu.DidNotReceive().EnsureMenuOpen(EnlistmentMenuService.ServiceWaitMenuId);
    }

    [TestMethod]
    public void BattleStarted_JoinLandsButMenuFails_EncounterMenuAlreadyUp_DoesNotExit()
    {
        JoinLandsButMenuFails("encounter");

        _service.OnCommanderBattleStarted("lord_party_1");

        _gameMenu.DidNotReceive().ExitToLast();
        Assert.AreEqual(EnlistmentState.EnlistedBattle, _store.Record.State);
    }

    [TestMethod]
    public void BattleStarted_JoinLandsButMenuFails_ArmyMerged_DoesNotPullHimOut()
    {
        JoinLandsButMenuFails(EnlistmentMenuService.ServiceWaitMenuId, army: true);

        _service.OnCommanderBattleStarted("lord_party_1");

        _army.DidNotReceive().LeaveArmy();
        _encounter.DidNotReceive().Finish(Arg.Any<bool>());
        Assert.AreEqual(EnlistmentState.EnlistedBattle, _store.Record.State);
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

        _encounter.Received(1).Finish(true);
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
    public void TryJoin_ReusedEncounter_ForgetsTheEndedBattleResultBeforeJoining()
    {
        // A repulsed assault's encounter is reused with its result still set, and every component's
        // continue check returns false on it: the next assault would end at once (#1021).
        MakeEnlisted();

        _service.TryJoinCommanderBattle("lord_party_1");

        Received.InOrder(() =>
        {
            _encounter.EnsureEncounterAgainst("lord_party_1");
            _encounter.ForgetEndedBattleResult();
            _encounter.JoinBattle(PartyBattleSide.Defender);
        });
    }

    [TestMethod]
    public void TryJoin_EncounterNotReady_DoesNotForgetAnyResult()
    {
        MakeEnlisted();
        _encounter.EnsureEncounterAgainst("lord_party_1").Returns(false);

        _service.TryJoinCommanderBattle("lord_party_1");

        _encounter.DidNotReceive().ForgetEndedBattleResult();
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

        _service.OnCommanderBattleEnded(mainPartyWasInEndingEvent: true);

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

        _service.OnCommanderBattleEnded(mainPartyWasInEndingEvent: true);

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _gameMenu.Received(1).EnsureMenuOpen(EnlistmentMenuService.ServiceWaitMenuId);
    }

    [TestMethod]
    public void BattleEnded_AlreadyOnWaitMenu_DoesNotReopenIt()
    {
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        _encounter.HasCurrent.Returns(false);
        _gameMenu.CurrentMenuId.Returns(EnlistmentMenuService.ServiceWaitMenuId);

        _service.OnCommanderBattleEnded(mainPartyWasInEndingEvent: true);

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
        // The encounter is still open (for the player's own event, MapEventEnded runs inside
        // PlayerEncounter.Finish); flipping to Attached here would re-park mid-Finish. The
        // reconciler completes the return.
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        _encounter.HasCurrent.Returns(true);

        _service.OnCommanderBattleEnded(mainPartyWasInEndingEvent: true);

        Assert.AreEqual(EnlistmentState.EnlistedBattle, _store.Record.State);
        _partyAdapter.DidNotReceive().ParkNear(Arg.Any<string>());
    }

    [TestMethod]
    public void BattleEnded_NotInBattleState_NoOp()
    {
        MakeEnlisted();

        _service.OnCommanderBattleEnded(mainPartyWasInEndingEvent: true);

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _partyAdapter.DidNotReceive().ParkNear(Arg.Any<string>());
    }

    [TestMethod]
    public void TryJoin_RollbackWhenEncounterIsNotOurs_SkipsFinishButStillReparksAndReassertsMenu()
    {
        // The rollback's Finish is now conditional on ownership, but everything AFTER it is not:
        // whatever the verdict, the player must end up parked, in Attached, with a menu. A
        // conditional cleanup must never make the recovery itself conditional.
        MakeEnlisted();
        _encounter.JoinBattle(PartyBattleSide.Defender).Returns(false);
        _encounter.GetOwnership(Arg.Any<string>()).Returns(new EncounterOwnershipSnapshot(
            hasEncounter: true, hasEncounteredMobileParty: false));   // a settlement visit — not ours
        _gameMenu.CurrentMenuId.Returns((string)null);

        _service.OnCommanderBattleStarted("lord_party_1");

        _encounter.DidNotReceive().Finish(Arg.Any<bool>());
        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _partyAdapter.Received(1).ParkNear("lord_1_1");
        _gameMenu.Received(1).EnsureMenuOpen(EnlistmentMenuService.ServiceWaitMenuId);
    }

    // ---- transient army membership (#443, field reports 4 and 7) ----

    [TestMethod]
    public void BattleStarted_JoinsTheCommandersArmyBeforeJoiningTheBattle()
    {
        // Ordering, not just occurrence. Mission.GetAgentTeam reads IsInSameArmyAsPlayer when the
        // mission builds its teams, which is downstream of JoinBattle — so a merge that lands after
        // it is a merge the team assignment never sees.
        MakeEnlisted();

        _service.OnCommanderBattleStarted("lord_party_1");

        Received.InOrder(() =>
        {
            _army.JoinCommanderArmy("lord_1_1");
            _encounter.JoinBattle(PartyBattleSide.Defender);
        });
    }

    [TestMethod]
    public void BattleStarted_ArmyMergeUnavailable_StillJoinsTheBattle()
    {
        // Best-effort by design. A kingdomless commander cannot have an army raised for him, and
        // that is not a reason to miss the battle — without the merge the player fights on the
        // ally-team path, which is exactly today's behaviour.
        MakeEnlisted();
        _army.JoinCommanderArmy(Arg.Any<string>()).Returns(false);

        _service.OnCommanderBattleStarted("lord_party_1");

        _encounter.Received(1).JoinBattle(PartyBattleSide.Defender);
        Assert.AreEqual(EnlistmentState.EnlistedBattle, _store.Record.State);
    }

    [TestMethod]
    public void BattleEnded_DoesNotDetachInsideTheMapEventEndedDispatch()
    {
        // ISSUE #557, and the regression this test exists to pin. This method used to call
        // LeaveArmy() directly, and that was the bug.
        //
        // Campaign-event listeners are LIFO (MbEvent.AddNonSerializedListener head-inserts,
        // installed v1.4.8 :24-30), and TAOM registers after SandBox, so THIS handler runs BEFORE
        // vanilla's SiegeAftermathCampaignBehavior.OnMapEventEnded. Clearing AttachedTo here makes
        // SetAttachedToInternal :1780-1783 drop the main party out of MapEventSide._battleParties,
        // so vanilla's IsMainPartyAmongParties() went false, its assignment block was skipped, and
        // its own aftermath menu dereferenced a null _besiegerParty. Crash bundle d7d9f7d3.
        //
        // The detach now happens in FlushArmyLeaveAfterBattle, called from Patch85's postfix on
        // PlayerEncounter.FinalizeBattle — after every vanilla listener has read the party list.
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        _encounter.HasCurrent.Returns(true);

        _service.OnCommanderBattleEnded(mainPartyWasInEndingEvent: true);

        _army.DidNotReceive().LeaveArmy();
        Assert.AreEqual(EnlistmentState.EnlistedBattle, _store.Record.State);   // loot flow preserved
    }

    [TestMethod]
    public void FlushArmyLeaveAfterBattle_DetachesOnceTheBattleIsOver()
    {
        // The other half of the #557 fix, and the one that keeps the post-defeat escape working.
        // PlayerEncounter.Finish runs FinalizeBattle() :1071 then FinishEncounterInternal() :1072 as
        // consecutive statements, and the escape (teleport out + SetDoNotAttackMainParty(2)) is
        // granted only when MainParty.AttachedTo == null. Patch85 postfixes the first, so the detach
        // still lands before the second reads it. That is the field report *"after they were
        // defeated I immediately got jumped by the enemy army"* staying fixed.
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        _encounter.IsMainPartyInMapEvent.Returns(false);

        _service.FlushArmyLeaveAfterBattle();

        _army.Received(1).LeaveArmy();
    }

    [TestMethod]
    public void FlushArmyLeaveAfterBattle_StillInALiveMapEvent_DoesNotDetach()
    {
        // ISSUE #551 carried into the deferred seam. FinalizeBattle can run while the player is
        // still inside another live map event; detaching there would pull him out of it, null that
        // event's TroopUpgradeTracker, and hand the CTD to MapEventSide.AllocateTroops. The
        // reconciler's no-battle sweep picks him up instead, which is correct rather than a
        // fallback.
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        _encounter.IsMainPartyInMapEvent.Returns(true);

        _service.FlushArmyLeaveAfterBattle();

        _army.DidNotReceive().LeaveArmy();
    }

    [TestMethod]
    public void FlushArmyLeaveAfterBattle_NotEnlisted_DoesNothing()
    {
        // Patch85's postfix fires on EVERY player battle end, enlisted or not, so the no-op path is
        // the common one and must not touch a party this feature does not own.
        _encounter.IsMainPartyInMapEvent.Returns(false);

        _service.FlushArmyLeaveAfterBattle();

        _army.DidNotReceive().LeaveArmy();
    }

    [TestMethod]
    public void FlushArmyLeaveAfterBattle_EnlistedButNotInAnArmy_DoesNothing()
    {
        // Stateless by design: there is no "detach owed" flag, so every call re-derives its terms
        // from live state. A player who never got the team merge has nothing to leave.
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        _encounter.IsMainPartyInMapEvent.Returns(false);
        _army.IsInArmy.Returns(false);

        _service.FlushArmyLeaveAfterBattle();

        _army.DidNotReceive().LeaveArmy();
    }

    [TestMethod]
    public void BattleEnded_ForeignEventWhileInAnotherLiveBattle_DoesNotLeaveTheArmy()
    {
        // ISSUE #551, the crash. The detach below is `MobileParty.MainParty.AttachedTo = null`, and
        // the engine's SetAttachedToInternal answers that with
        // `Party.MapEventSide.HandleMapEventEndForPartyInternal(Party); Party.MapEventSide = null;`
        // — it pulls the main party OUT of whatever map event it is in. Doing that to a battle the
        // player has just joined nulls MapEvent.TroopUpgradeTracker (RemoveInvolvedPartyInternal
        // :855-858) while MapEvent.BattleObserver stays attached, and MapEventSide.AllocateTroops
        // :552 derefs that tracker unguarded on the next simulation tick. Straight CTD.
        //
        // The gate in EnlistmentBattleBehavior is the primary fix; this is the floor beneath it,
        // because the operation is silently destructive whenever it reaches a foreign event.
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        _encounter.IsMainPartyInMapEvent.Returns(true);

        _service.OnCommanderBattleEnded(mainPartyWasInEndingEvent: false);

        _army.DidNotReceive().LeaveArmy();
        Assert.AreEqual(EnlistmentState.EnlistedBattle, _store.Record.State);
    }

    [TestMethod]
    public void BattleEnded_OurOwnEventEnding_LeavesTheDetachToTheDeferredSeam()
    {
        // The normal path. OnMapEventEnded is dispatched from MapEvent.FinalizeEventAux :2079,
        // BEFORE the side teardown, so the main party still reads as in a map event here — the
        // ending one. Detaching at this instant is exactly what corrupted vanilla's own read of
        // that side (#557), so the handler leaves it alone and Patch85 does it a statement later.
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        _encounter.IsMainPartyInMapEvent.Returns(true);

        _service.OnCommanderBattleEnded(mainPartyWasInEndingEvent: true);

        _army.DidNotReceive().LeaveArmy();
    }

    [TestMethod]
    public void BattleEnded_NotInBattleState_StillLeavesTheDetachToTheDeferredSeam()
    {
        // Army membership is still ours to clean up whatever state we are in — leaking it strands
        // the player attached, and an attached party with a null Army is the shape
        // DefaultEncounterGameMenuModel.GetGenericStateMenu derefs unguarded on every map frame.
        // What changed in #557 is only WHERE it happens, never WHETHER: Patch85's postfix on
        // FinalizeBattle, with the reconciler's no-battle sweep behind it.
        MakeEnlisted();

        _service.OnCommanderBattleEnded(mainPartyWasInEndingEvent: true);

        _army.DidNotReceive().LeaveArmy();
    }

    [TestMethod]
    public void BattleStarted_JoinFails_LeavesTheArmyOnRollback()
    {
        // The merge may have succeeded before a later step failed. A rollback that leaves the player
        // attached to an army he is not fighting with is the same forfeited-escape state, with no
        // battle to justify it.
        MakeEnlisted();
        _encounter.JoinBattle(PartyBattleSide.Defender).Returns(false);

        _service.OnCommanderBattleStarted("lord_party_1");

        _army.Received(1).LeaveArmy();
        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
    }
}
