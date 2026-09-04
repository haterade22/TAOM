using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The hourly reconciler is the SINGLE terminal-decision authority: commander death,
/// grace start/expiry, captivity transitions, re-parking. Menus and dialogs only render
/// state — they never discharge (the donor's wait-menu made terminal decisions with a
/// different policy than its daily tick; that split is the bug class these tests pin).
/// </summary>
[TestClass]
public class EnlistmentReconcilerTests
{
    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentStateMachine _machine = null!;
    private ICommanderLordAdapter _commander = null!;
    private IMobilePartyAttachmentAdapter _partyAdapter = null!;
    private ServiceAttachmentService _attachment = null!;
    private DischargeService _discharge = null!;
    private IEncounterAdapter _encounter = null!;
    private IArmyMembershipAdapter _army = null!;
    private EnlistmentReconciler _reconciler = null!;

    private const double Now = 200.0;
    private const double Grace = 7.0; // EnlistmentCoreConfig default

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        _machine = new EnlistmentStateMachine(_store, _logger);
        _commander = Substitute.For<ICommanderLordAdapter>();
        _partyAdapter = Substitute.For<IMobilePartyAttachmentAdapter>();
        _partyAdapter.RestorePresence().Returns(true);
        _partyAdapter.ParkNear(Arg.Any<string>()).Returns(true);
        _partyAdapter.SyncPositionTo(Arg.Any<string>()).Returns(true);
        _attachment = new ServiceAttachmentService(_partyAdapter, Substitute.For<IGameMenuAdapter>(), _logger);
        _discharge = new DischargeService(_store, _machine, _partyAdapter, Substitute.For<IEncounterAdapter>(), new EncounterOwnershipPolicy(), Substitute.For<ICommanderLordAdapter>(), Substitute.For<IGameMenuAdapter>(), Substitute.For<IServiceDiplomacyService>(), Substitute.For<IArmyMembershipAdapter>(), _logger);
        _encounter = Substitute.For<IEncounterAdapter>();
        _army = Substitute.For<IArmyMembershipAdapter>();
        _reconciler = new EnlistmentReconciler(_store, _machine, _attachment, _commander, _discharge,
            new EnlistmentConfigProvider(_logger), _encounter, new EncounterOwnershipPolicy(), Substitute.For<IEnlistmentDiagnosticsSettingsProvider>(),
            EnlistmentTestDoubles.FeatureOn(), Substitute.For<IInquiryAdapter>(), _army, _logger);
    }

    private void MakeEnlisted(EnlistmentState state = EnlistmentState.EnlistedAttached)
    {
        _store.Record.State = state;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
        _store.Record.EnlistedAtDay = 100.0;
        _store.Record.ContractEndDay = 465.0;
    }

    private void CommanderHealthy(bool inMapEvent = false)
    {
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, isPrisoner: false,
            partyId: "lord_party_1", partyIsActive: true,
            partyIsInMapEvent: inMapEvent, name: "Lord Test"));
    }

    private void CommanderCaptured()
    {
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, isPrisoner: true,
            partyId: null, partyIsActive: false, name: "Lord Test"));
    }

    private void CommanderDead()
    {
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: false, name: "Lord Test"));
    }

    private void PlayerPresence(bool parked = true, bool captive = false, bool inMapEvent = false,
        bool hasEncounter = false, string settlementId = null)
    {
        _partyAdapter.GetPresence(Arg.Any<string>()).Returns(new PlayerPresenceSnapshot(
            mainPartyExists: true, isCaptive: captive,
            isActive: !parked, isVisible: !parked, isInMapEvent: inMapEvent,
            hasPlayerEncounter: hasEncounter, settlementId: settlementId));
    }

    /// <summary>
    /// The shape a stranded settlement encounter actually has: live, no conversation, and NO
    /// encountered mobile party, because a settlement encounter has none. `HasCurrent` is set for
    /// the same reason it is true in game, and it matters: the reconciler's own staleness predicate
    /// reads it, so leaving it false lets the latch break for the wrong reason and the test passes
    /// without exercising anything.
    /// </summary>
    private void SettlementShapedEncounter(bool playerInsideSettlement = false, bool isBattleEncounter = false)
    {
        _encounter.HasCurrent.Returns(true);
        _encounter.GetOwnership(Arg.Any<string>()).Returns(new EncounterOwnershipSnapshot(
            hasEncounter: true,
            conversationInProgress: false,
            hasEncounteredMobileParty: false,
            encounteredPartyId: null,
            encounteredPartyIsCommanderRelated: false,
            playerInMapEvent: false,
            playerInsideSettlement: playerInsideSettlement,
            isBattleEncounter: isBattleEncounter));
        _encounter.Finish(Arg.Any<bool>()).Returns(true);
    }

    private void CommanderInSettlement(string settlementId)
    {
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, isPrisoner: false,
            partyId: "lord_party_1", partyIsActive: true, partyIsInMapEvent: false,
            partyIsInSettlement: true, settlementId: settlementId, name: "Lord Test"));
    }

    /// <summary>
    /// THE DEADLOCK, and the reason this fix exists. Two self-heals were written independently and
    /// each is the other's precondition:
    ///
    ///   ServiceMaintenanceService.TryBreakBattleLatch is the only exit from EnlistedBattle when no
    ///   battle is running, and it returns early while `presence.HasPlayerEncounter`.
    ///
    ///   This sweep is the only thing that closes a stranded encounter, and it used to require
    ///   `State == EnlistedAttached`.
    ///
    /// So EnlistedBattle plus a stranded encounter was a permanent mutual block: the encounter
    /// stopped the return to Attached, and not being Attached stopped the sweep. The player cannot
    /// move (an open encounter holds the map), cannot open any other encounter, and loses the
    /// service menu because the pump's menu work is also gated on Attached. That is the reported
    /// "army left me behind after sieging East Osgiliath, unable to move, even the Enlist UI is
    /// gone".
    ///
    /// A siege is the way in. `LeaveSettlementAction.ApplyForParty` (installed v1.4.8) calls
    /// `PlayerEncounter.Finish()` in exactly one branch, when the LEAVING party leads its army and
    /// the main party is attached to it. An enlisted player is the main party and leads nothing, so
    /// the encounter TAOM opens for every settlement placement since #510 is left open, while a
    /// siege holds the state in EnlistedBattle.
    ///
    /// Fixed at the sweep rather than at the latch: the latch's encounter term is load-bearing for a
    /// battle being SET UP, and the sweep already asks EncounterOwnershipPolicy for permission.
    /// </summary>
    [TestMethod]
    public void Reconcile_StrandedEncounterWhileLatchedInBattle_ClosesItSoTheLatchCanBreak()
    {
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        CommanderHealthy(inMapEvent: false);
        PlayerPresence(parked: true, inMapEvent: false, hasEncounter: true, settlementId: null);
        SettlementShapedEncounter();

        _reconciler.ReconcileHourly(Now);

        _encounter.Received(1).Finish(Arg.Any<bool>());
    }

    [TestMethod]
    public void Reconcile_SettlementEncounterWhileActuallyInsideTheSettlement_LeavesItAlone()
    {
        // The protection that must survive the fix. Since #510 every TAOM settlement placement
        // opens an encounter deliberately, so a settlement-shaped encounter is CORRECT while the
        // player is inside one. Closing it would take down the town menu the player is standing in,
        // which is the failure R3 was written to prevent. "Stranded" means the encounter outlived
        // the settlement, and the only way to know that is that the player is no longer in one.
        //
        // The COMMANDER is in the same settlement on purpose. An earlier version of this test left
        // him outside, which makes Assess return SettlementExitRequired: the reconciler then walks
        // the player out and closes the now-genuinely-stranded encounter, which is correct behaviour
        // and not what this test is about. Same settlement is the state that reaches the sweep.
        //
        // The ownership snapshot must agree with presence, too. Both are one read of
        // MainParty.CurrentSettlement in the real adapter, so a test where presence says "in a town"
        // while ownership says "not inside a settlement" pins a state the game cannot produce.
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        CommanderInSettlement("town_ES1");
        PlayerPresence(parked: false, inMapEvent: false, hasEncounter: true, settlementId: "town_ES1");
        SettlementShapedEncounter(playerInsideSettlement: true);

        _reconciler.ReconcileHourly(Now);

        _encounter.DidNotReceive().Finish(Arg.Any<bool>());
    }

    [TestMethod]
    public void Reconcile_BattleAftermathEncounter_IsNeverTornDown()
    {
        // The regression the deep review caught, and the reason the state gate could not simply be
        // deleted. MapEventSide.Clear() nulls MainParty.MapEvent BEFORE the encounter closes, so the
        // loot screen after a siege reads as "no battle anywhere" to every guard in this method: the
        // state is still EnlistedBattle, neither party is in a map event, and the encounter is open.
        // Without R1b the sweep closes the player's own battle result, which is the exact scenario
        // the strand fix was written for.
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        CommanderHealthy(inMapEvent: false);
        PlayerPresence(parked: true, inMapEvent: false, hasEncounter: true);
        SettlementShapedEncounter(isBattleEncounter: true);

        _reconciler.ReconcileHourly(Now);

        _encounter.DidNotReceive().Finish(Arg.Any<bool>());
    }

    [TestMethod]
    public void Reconcile_StrandedEncounterWhileCommanderIsFighting_LeavesItAlone()
    {
        // The guard that actually matters, and it is NOT the state. While either party is in a map
        // event the encounter may belong to a battle being set up, and closing it would break the
        // join. Widening the state gate must not widen this one.
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        CommanderHealthy(inMapEvent: true);
        PlayerPresence(parked: true, inMapEvent: false, hasEncounter: true);
        SettlementShapedEncounter();

        _reconciler.ReconcileHourly(Now);

        _encounter.DidNotReceive().Finish(Arg.Any<bool>());
    }

    [TestMethod]
    public void Reconcile_StrandedEncounterWhilePlayerIsInAMapEvent_LeavesItAlone()
    {
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        CommanderHealthy(inMapEvent: false);
        PlayerPresence(parked: true, inMapEvent: true, hasEncounter: true);
        SettlementShapedEncounter();

        _reconciler.ReconcileHourly(Now);

        _encounter.DidNotReceive().Finish(Arg.Any<bool>());
    }

    [TestMethod]
    public void Reconcile_StrandedEncounterDuringCommanderGrace_ClosesIt()
    {
        // "The army left me behind" is most naturally a commander lost in the assault, which is
        // CommanderUnavailable, not EnlistedBattle. That path never touched the encounter: it waits
        // out a grace of up to seven campaign days and only then discharges, and discharge is the
        // first thing that closes an encounter. So a stranded player sat unable to move for a week
        // of campaign time before the game let them go. Bounded, unlike the battle latch, and still
        // unacceptable.
        MakeEnlisted(EnlistmentState.CommanderUnavailable);
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, isPrisoner: true,
            partyId: null, partyIsActive: false, name: "Lord Test"));
        PlayerPresence(parked: true, inMapEvent: false, hasEncounter: true, settlementId: null);
        SettlementShapedEncounter();

        _reconciler.ReconcileHourly(Now);

        _encounter.Received(1).Finish(Arg.Any<bool>());
    }

    [TestMethod]
    public void Reconcile_StrandedEncounterWhileCaptive_LeavesItAlone()
    {
        // Vanilla captivity owns the party. Grace is already frozen for captives for exactly this
        // reason, and the sweep must not reach around that.
        MakeEnlisted(EnlistmentState.CommanderUnavailable);
        CommanderCaptured();
        PlayerPresence(parked: true, captive: true, hasEncounter: true);
        SettlementShapedEncounter();

        _reconciler.ReconcileHourly(Now);

        _encounter.DidNotReceive().Finish(Arg.Any<bool>());
    }

    [TestMethod]
    public void Reconcile_StrandedPartyEncounterThatIsNotTheCommanders_LeavesItAlone()
    {
        // R4 still governs party-shaped encounters: someone else's is never ours to close, and the
        // widened state gate grants no new authority over them.
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        CommanderHealthy(inMapEvent: false);
        PlayerPresence(parked: true, inMapEvent: false, hasEncounter: true);
        _encounter.HasCurrent.Returns(true);
        _encounter.GetOwnership(Arg.Any<string>()).Returns(new EncounterOwnershipSnapshot(
            hasEncounter: true, conversationInProgress: false,
            hasEncounteredMobileParty: true, encounteredPartyId: "some_other_party",
            encounteredPartyIsCommanderRelated: false, playerInMapEvent: false));
        _encounter.Finish(Arg.Any<bool>()).Returns(true);

        _reconciler.ReconcileHourly(Now);

        _encounter.DidNotReceive().Finish(Arg.Any<bool>());
    }

    [TestMethod]
    public void Reconcile_NotEnlisted_DoesNothing()
    {
        _reconciler.ReconcileHourly(Now);

        _commander.DidNotReceive().GetSnapshot(Arg.Any<string>());
        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
    }

    [TestMethod]
    public void Reconcile_AttachedHealthyParked_SyncsPosition()
    {
        MakeEnlisted();
        CommanderHealthy();
        PlayerPresence(parked: true);

        _reconciler.ReconcileHourly(Now);

        _partyAdapter.Received(1).SyncPositionTo("lord_1_1");
        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
    }

    [TestMethod]
    public void Reconcile_AttachedNotParked_Parks()
    {
        MakeEnlisted();
        CommanderHealthy();
        PlayerPresence(parked: false);

        _reconciler.ReconcileHourly(Now);

        _partyAdapter.Received(1).ParkNear("lord_1_1");
    }

    [TestMethod]
    public void Reconcile_CommanderDead_DischargesCommanderDead()
    {
        MakeEnlisted();
        CommanderDead();
        PlayerPresence();

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
        _partyAdapter.Received(1).RestorePresence();
    }

    [TestMethod]
    public void Reconcile_PrisonerCommanderWithLiveParty_StillEntersGrace()
    {
        // Defense-in-depth twin of the Assess prisoner test: even if the engine leaves a
        // captured commander's party alive, service pauses into grace, never re-parks.
        MakeEnlisted();
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, isPrisoner: true,
            partyId: "lord_party_1", partyIsActive: true, name: "Lord Test"));
        PlayerPresence(parked: true);

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual(EnlistmentState.CommanderUnavailable, _store.Record.State);
        _partyAdapter.Received(1).RestorePresence();
    }

    [TestMethod]
    public void Reconcile_CommanderCaptured_StartsGraceAndFreesPlayer()
    {
        // The donor's wait menu discharged instantly on a captured commander; the design
        // fix is a grace window with the player free to roam.
        MakeEnlisted();
        CommanderCaptured();
        PlayerPresence(parked: true);

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual(EnlistmentState.CommanderUnavailable, _store.Record.State);
        Assert.AreEqual(Now + Grace, _store.Record.GraceEndsAtDay);
        _partyAdapter.Received(1).RestorePresence();
    }

    [TestMethod]
    public void Reconcile_GraceRunning_NoDischargeBeforeExpiry()
    {
        MakeEnlisted(EnlistmentState.CommanderUnavailable);
        _store.Record.GraceEndsAtDay = Now + 3.0;
        CommanderCaptured();
        PlayerPresence(parked: false);

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual(EnlistmentState.CommanderUnavailable, _store.Record.State);
    }

    [TestMethod]
    public void Reconcile_GraceExpired_DischargesGraceExpired()
    {
        MakeEnlisted(EnlistmentState.CommanderUnavailable);
        _store.Record.GraceEndsAtDay = Now - 0.5;
        CommanderCaptured();
        PlayerPresence(parked: false);

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
    }

    [TestMethod]
    public void Reconcile_GraceMissing_StartsIt()
    {
        // A dropped/malformed grace timer must restart, not instantly expire.
        MakeEnlisted(EnlistmentState.CommanderUnavailable);
        _store.Record.GraceEndsAtDay = null;
        CommanderCaptured();
        PlayerPresence(parked: false);

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual(EnlistmentState.CommanderUnavailable, _store.Record.State);
        Assert.AreEqual(Now + Grace, _store.Record.GraceEndsAtDay);
    }

    [TestMethod]
    public void Reconcile_CommanderRecovered_ReattachesAndClearsGrace()
    {
        MakeEnlisted(EnlistmentState.CommanderUnavailable);
        _store.Record.GraceEndsAtDay = Now + 3.0;
        CommanderHealthy();
        PlayerPresence(parked: false);

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        Assert.IsNull(_store.Record.GraceEndsAtDay);
        _partyAdapter.Received(1).ParkNear("lord_1_1");
    }

    [TestMethod]
    public void Reconcile_CommanderDeadDuringGrace_DischargesCommanderDead()
    {
        MakeEnlisted(EnlistmentState.CommanderUnavailable);
        _store.Record.GraceEndsAtDay = Now + 3.0;
        CommanderDead();
        PlayerPresence(parked: false);

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
    }

    [TestMethod]
    public void Reconcile_PlayerCapturedWhileAttached_TransitionsToCaptiveWithoutTouchingParty()
    {
        MakeEnlisted();
        CommanderHealthy();
        PlayerPresence(parked: false, captive: true);

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual(EnlistmentState.EnlistedPlayerCaptive, _store.Record.State);
        _partyAdapter.DidNotReceive().ParkNear(Arg.Any<string>());
        _partyAdapter.DidNotReceive().RestorePresence();
    }

    [TestMethod]
    public void Reconcile_PlayerReleased_ReattachesToCommander()
    {
        MakeEnlisted(EnlistmentState.EnlistedPlayerCaptive);
        CommanderHealthy();
        PlayerPresence(parked: false, captive: false);

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _partyAdapter.Received(1).ParkNear("lord_1_1");
    }

    [TestMethod]
    public void Reconcile_PlayerStillCaptive_NoStateChangeNoGraceProcessing()
    {
        MakeEnlisted(EnlistmentState.EnlistedPlayerCaptive);
        CommanderHealthy();
        PlayerPresence(captive: true);

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual(EnlistmentState.EnlistedPlayerCaptive, _store.Record.State);
    }

    [TestMethod]
    public void Reconcile_CaptiveDuringCommanderGrace_GraceFrozen()
    {
        // Grace must not expire into a discharge (and a presence-restore) while vanilla
        // captivity owns the party.
        MakeEnlisted(EnlistmentState.CommanderUnavailable);
        _store.Record.GraceEndsAtDay = Now - 1.0;
        CommanderCaptured();
        PlayerPresence(parked: false, captive: true);

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual(EnlistmentState.CommanderUnavailable, _store.Record.State);
    }

    [TestMethod]
    public void Reconcile_StaleBattleStateNoEventsAnywhere_ReturnsToAttached()
    {
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        CommanderHealthy(inMapEvent: false);
        PlayerPresence(parked: true, inMapEvent: false);
        _encounter.HasCurrent.Returns(false);

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
    }

    [TestMethod]
    public void Reconcile_StaleBattleStateNoEventsAnywhere_LeavesTheCommanderArmy()
    {
        // Deep-review finding (2026-08-11). This branch is the ONLY code that notices a battle
        // which resolved without a MapEventEnded edge — a save/load across the end, a throw in
        // OnCommanderBattleEnded, a co-op host handoff. It flipped the state back to attached and
        // left the player merged into the commander's army indefinitely.
        //
        // MobileParty.AttachedTo then stays set, and PlayerEncounter.FinishEncounterInternal
        // (verified on installed 1.4.8) grants the post-defeat escape ONLY when AttachedTo == null.
        // So the next unrelated ambush re-creates field report 7b — "jumped immediately after being
        // defeated" — with no army battle anywhere to explain it.
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        CommanderHealthy(inMapEvent: false);
        PlayerPresence(parked: true, inMapEvent: false);
        _encounter.HasCurrent.Returns(false);
        _army.IsInArmy.Returns(true);

        _reconciler.ReconcileHourly(Now);

        _army.Received(1).LeaveArmy();
    }

    [TestMethod]
    public void Reconcile_AttachedButStillMergedAfterAReload_LeavesTheCommanderArmy()
    {
        // Codex P1 (2026-08-12). The detach cannot key on EnlistedBattle, because
        // EnlistmentRecord.ToPersistedState COERCES EnlistedBattle to EnlistedAttached — so a save
        // taken mid-battle reloads reading EnlistedAttached while the main party is still merged
        // into the commander's army. That is the one shape a state-keyed check is blind to, and it
        // leaves AttachedTo set, which forfeits the vanilla post-defeat escape on the next
        // unrelated ambush.
        MakeEnlisted(EnlistmentState.EnlistedAttached);
        CommanderHealthy(inMapEvent: false);
        PlayerPresence(parked: true, inMapEvent: false);
        _encounter.HasCurrent.Returns(false);
        _army.IsInArmy.Returns(true);

        _reconciler.ReconcileHourly(Now);

        _army.Received(1).LeaveArmy();
        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
    }

    [TestMethod]
    public void Reconcile_AttachedAndNotInAnyArmy_DoesNotCallLeaveArmy()
    {
        // The guard is on IsInArmy so the ordinary parked tick does no work at all — an hourly
        // LeaveArmy would race the battle path for ownership of an army raised seconds earlier.
        MakeEnlisted(EnlistmentState.EnlistedAttached);
        CommanderHealthy(inMapEvent: false);
        PlayerPresence(parked: true, inMapEvent: false);
        _encounter.HasCurrent.Returns(false);
        _army.IsInArmy.Returns(false);

        _reconciler.ReconcileHourly(Now);

        _army.DidNotReceive().LeaveArmy();
    }

    [TestMethod]
    public void Reconcile_BattleStateWithOpenEncounter_StaysInArmy()
    {
        // The mirror of the test above, and the reason the LeaveArmy call sits INSIDE the
        // stale-battle branch rather than at the top of ReconcileAttached. While the battle is
        // genuinely live, detaching would undo the team merge mid-fight and put the player back
        // behind his own commander's line — the exact placement bug #443 exists to fix.
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        CommanderHealthy(inMapEvent: true);
        PlayerPresence(parked: false, inMapEvent: true);
        _encounter.HasCurrent.Returns(true);
        _army.IsInArmy.Returns(true);

        _reconciler.ReconcileHourly(Now);

        _army.DidNotReceive().LeaveArmy();
    }

    [TestMethod]
    public void Reconcile_AttachedHealthy_DoesNotTouchArmyMembership()
    {
        // Ordinary parked service must not call LeaveArmy every hour: it is not free — the adapter
        // disbands the army it raised, and an hourly disband would fight the battle path for
        // ownership of the same object.
        MakeEnlisted();
        CommanderHealthy();
        PlayerPresence(parked: true);

        _reconciler.ReconcileHourly(Now);

        _army.DidNotReceive().LeaveArmy();
    }

    [TestMethod]
    public void Reconcile_BattleStateWithOpenEncounter_StaysInBattle()
    {
        // Regression (2026-08-07): the map event reads as gone while the loot/aftermath encounter
        // is still open. Demoting here re-parked the player mid-battle and handed the aftermath
        // menus to the redirect guard — an hourly tick could undo a successful join.
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        CommanderHealthy(inMapEvent: false);
        PlayerPresence(parked: false, inMapEvent: false);
        _encounter.HasCurrent.Returns(true);

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual(EnlistmentState.EnlistedBattle, _store.Record.State);
    }

    [TestMethod]
    public void Reconcile_CommanderFightingPlayerIsNot_RaisesBattleJoinRequested()
    {
        // Regression (2026-08-07): this event had NO subscriber, so the only recovery path for a
        // missed MapEventStarted did nothing at all. The wiring lives in EnlistmentBattleBehavior;
        // this pins that the reconciler still raises it with the commander hero id.
        MakeEnlisted();
        CommanderHealthy(inMapEvent: true);
        PlayerPresence(parked: true, inMapEvent: false);
        string requestedFor = null;
        _reconciler.BattleJoinRequested += id => requestedFor = id;

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual("lord_1_1", requestedFor);
    }


}
