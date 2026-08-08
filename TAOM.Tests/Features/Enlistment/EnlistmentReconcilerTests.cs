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
        _discharge = new DischargeService(_store, _machine, _partyAdapter, Substitute.For<IEncounterAdapter>(), new EncounterOwnershipPolicy(), Substitute.For<ICommanderLordAdapter>(), Substitute.For<IGameMenuAdapter>(), _logger);
        _encounter = Substitute.For<IEncounterAdapter>();
        _reconciler = new EnlistmentReconciler(_store, _machine, _attachment, _commander, _discharge,
            new EnlistmentConfigProvider(_logger), _encounter, new EncounterOwnershipPolicy(), Substitute.For<IEnlistmentDiagnosticsSettingsProvider>(),
            EnlistmentTestDoubles.FeatureOn(), _logger);
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

    private void PlayerPresence(bool parked = true, bool captive = false, bool inMapEvent = false)
    {
        _partyAdapter.GetPresence().Returns(new PlayerPresenceSnapshot(
            mainPartyExists: true, isCaptive: captive,
            isActive: !parked, isVisible: !parked, isInMapEvent: inMapEvent));
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

    [TestMethod]
    public void Reconcile_DetachedDutyCommanderDead_Discharges()
    {
        MakeEnlisted(EnlistmentState.EnlistedDetachedOnDuty);
        CommanderDead();
        PlayerPresence(parked: false);

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
    }

    [TestMethod]
    public void Reconcile_DetachedDutyCommanderCaptured_GraceStartsNoPresenceChange()
    {
        MakeEnlisted(EnlistmentState.EnlistedDetachedOnDuty);
        CommanderCaptured();
        PlayerPresence(parked: false);

        _reconciler.ReconcileHourly(Now);

        Assert.AreEqual(EnlistmentState.CommanderUnavailable, _store.Record.State);
        Assert.AreEqual(Now + Grace, _store.Record.GraceEndsAtDay);
        _partyAdapter.DidNotReceive().RestorePresence();
        _partyAdapter.DidNotReceive().ParkNear(Arg.Any<string>());
    }
}
