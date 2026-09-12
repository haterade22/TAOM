using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Entity State Matrix tests — one per load-time row (csharp-architecture mandate).
/// Direction rule under test: restore-direction mutations are safe everywhere;
/// park-direction only when every precondition verifies; never leave an ownerless hidden
/// MainParty.
/// </summary>
[TestClass]
public class EnlistmentLoadNormalizerTests
{
    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentStateMachine _machine = null!;
    private ICommanderLordAdapter _commander = null!;
    private IMobilePartyAttachmentAdapter _partyAdapter = null!;
    private IEncounterAdapter _encounter = null!;
    private EnlistmentLoadNormalizer _normalizer = null!;

    private const double Now = 200.0;

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
        // ONE encounter adapter for the normalizer and the reconciler, as the container has one
        // singleton: a fixture that hands each its own substitute lets the two disagree about
        // whether an encounter is open, which the game cannot do.
        _encounter = Substitute.For<IEncounterAdapter>();
        var attachment = new ServiceAttachmentService(_partyAdapter, Substitute.For<IGameMenuAdapter>(), _logger);
        var discharge = new DischargeService(_store, _machine, _partyAdapter, _encounter, new EncounterOwnershipPolicy(), Substitute.For<ICommanderLordAdapter>(), Substitute.For<IGameMenuAdapter>(), Substitute.For<IServiceDiplomacyService>(), Substitute.For<IArmyMembershipAdapter>(), _logger);
        var reconciler = new EnlistmentReconciler(_store, _machine, attachment, _commander, discharge,
            new EnlistmentConfigProvider(_logger), _encounter, new EncounterOwnershipPolicy(), Substitute.For<IEnlistmentDiagnosticsSettingsProvider>(),
            EnlistmentTestDoubles.FeatureOn(), Substitute.For<IInquiryAdapter>(),
            Substitute.For<IArmyMembershipAdapter>(), _logger);
        _normalizer = new EnlistmentLoadNormalizer(
            _store, _machine, reconciler, _partyAdapter, discharge, _encounter, _logger);
    }

    private void MakeEnlisted(EnlistmentState state = EnlistmentState.EnlistedAttached)
    {
        _store.Record.State = state;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
    }

    private void CommanderHealthy()
    {
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party_1", partyIsActive: true));
    }

    private void Presence(bool parked, bool captive = false, bool inMapEvent = false,
        bool hasEncounter = false, string settlementId = null)
    {
        _partyAdapter.GetPresence(Arg.Any<string>()).Returns(new PlayerPresenceSnapshot(
            mainPartyExists: true, isCaptive: captive, isActive: !parked, isVisible: !parked,
            isInMapEvent: inMapEvent, hasPlayerEncounter: hasEncounter, settlementId: settlementId));
    }

    private void OpenEncounter(bool isBattle)
    {
        _encounter.HasCurrent.Returns(true);
        _encounter.GetOwnership(Arg.Any<string>()).Returns(new EncounterOwnershipSnapshot(
            hasEncounter: true, hasEncounteredMobileParty: isBattle,
            encounteredPartyId: isBattle ? "enemy_lord_party" : null, encounteredPartyIsCommanderRelated: false,
            playerInMapEvent: false, playerInsideSettlement: !isBattle, isBattleEncounter: isBattle));
    }

    // ---- The save coercion: EnlistedBattle persists as EnlistedAttached (#577) -----------------
    //
    // EnlistmentRecord.ToPersistedState writes EnlistedBattle as EnlistedAttached on the grounds
    // that battle reality is re-derived at load. These pin that re-derivation, which did not exist
    // before 2026-09-12: without it every gate keyed on EnlistedBattle (the deployment-screen
    // model, the role strip, the placement, the presence hold) reads the wrong state for the
    // battle the player saved in.

    [TestMethod]
    public void Normalize_SavedAtTheEncounterMenu_PartyStillInTheMapEvent_RestoresBattleState()
    {
        MakeEnlisted(EnlistmentState.EnlistedAttached);
        CommanderHealthy();
        Presence(parked: false, inMapEvent: true, hasEncounter: true);
        OpenEncounter(isBattle: true);

        _normalizer.Normalize("main_hero", Now);

        Assert.AreEqual(EnlistmentState.EnlistedBattle, _store.Record.State);
        _partyAdapter.DidNotReceive().ParkNear(Arg.Any<string>());
    }

    [TestMethod]
    public void Normalize_SavedInTheAftermath_BattleEncounterStillOpen_RestoresBattleStateAndHoldsPresence()
    {
        // The map event is already gone (MapEventSide.Clear() runs before the encounter closes),
        // the encounter's own battle handle is not. Without the re-derivation this reloads as an
        // active, unparked Attached soldier, Assess says AttachRequired, and the reconciler parks
        // the party out of its live encounter: #577's crash shape, reached by reload.
        MakeEnlisted(EnlistmentState.EnlistedAttached);
        CommanderHealthy();
        Presence(parked: false, inMapEvent: false, hasEncounter: true);
        OpenEncounter(isBattle: true);

        _normalizer.Normalize("main_hero", Now);

        Assert.AreEqual(EnlistmentState.EnlistedBattle, _store.Record.State);
        _partyAdapter.DidNotReceive().ParkNear(Arg.Any<string>());
    }

    [TestMethod]
    public void Normalize_SavedInsideASettlement_EncounterIsNotABattle_StaysAttached()
    {
        // Since #510 every settlement placement opens an encounter deliberately. An open encounter
        // alone must not read as a battle, or every town stop would reload as EnlistedBattle.
        MakeEnlisted(EnlistmentState.EnlistedAttached);
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party_1", partyIsActive: true,
            partyIsInSettlement: true, settlementId: "town_A"));
        Presence(parked: false, inMapEvent: false, hasEncounter: true, settlementId: "town_A");
        OpenEncounter(isBattle: false);

        _normalizer.Normalize("main_hero", Now);

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
    }

    [TestMethod]
    public void Normalize_NotEnlistedButPartyParked_RescuesPresenceAndWarns()
    {
        // Foreign/corrupt save rescue: a hidden inactive MainParty with no enlistment
        // record must never survive a load.
        Presence(parked: true);

        _normalizer.Normalize("main_hero", Now);

        _partyAdapter.Received(1).RestorePresence();
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("ownerless") || s.Contains("rescue")));
    }

    [TestMethod]
    public void Normalize_NotEnlistedPartyNormal_NoAction()
    {
        Presence(parked: false);

        _normalizer.Normalize("main_hero", Now);

        _partyAdapter.DidNotReceive().RestorePresence();
    }

    [TestMethod]
    public void Normalize_NotEnlistedButCaptive_NeverTouchesParty()
    {
        // Vanilla captivity legitimately hides the party — not ours to rescue.
        Presence(parked: true, captive: true);

        _normalizer.Normalize("main_hero", Now);

        _partyAdapter.DidNotReceive().RestorePresence();
    }

    [TestMethod]
    public void Normalize_HeroIdentityMismatch_QuietDischargeRestoresPresence()
    {
        // Co-op join / heir succession: the recorded EnlistedHeroId is not the current
        // MainHero — quiet, penalty-free discharge.
        MakeEnlisted();
        CommanderHealthy();
        Presence(parked: true);

        _normalizer.Normalize("different_hero", Now);

        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
        _partyAdapter.Received().RestorePresence();
    }

    [TestMethod]
    public void Normalize_EnlistedHealthyCommander_ReassertsPark()
    {
        MakeEnlisted();
        CommanderHealthy();
        Presence(parked: false);

        _normalizer.Normalize("main_hero", Now);

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _partyAdapter.Received(1).ParkNear("lord_1_1");
    }

    [TestMethod]
    public void Normalize_EnlistedCommanderDeadAtLoad_Discharges()
    {
        MakeEnlisted();
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(exists: true, isAlive: false));
        Presence(parked: true);

        _normalizer.Normalize("main_hero", Now);

        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
        _partyAdapter.Received().RestorePresence();
    }

    [TestMethod]
    public void Normalize_EnlistedCommanderPartylessAtLoad_GraceWithPresenceRestored()
    {
        MakeEnlisted();
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, isPrisoner: true));
        Presence(parked: true);

        _normalizer.Normalize("main_hero", Now);

        Assert.AreEqual(EnlistmentState.CommanderUnavailable, _store.Record.State);
        Assert.IsNotNull(_store.Record.GraceEndsAtDay);
        _partyAdapter.Received().RestorePresence();
    }

    [TestMethod]
    public void Normalize_EnlistedButCaptiveAtLoad_MovesToCaptiveTouchesNothing()
    {
        MakeEnlisted();
        CommanderHealthy();
        Presence(parked: false, captive: true);

        _normalizer.Normalize("main_hero", Now);

        Assert.AreEqual(EnlistmentState.EnlistedPlayerCaptive, _store.Record.State);
        _partyAdapter.DidNotReceive().ParkNear(Arg.Any<string>());
        _partyAdapter.DidNotReceive().RestorePresence();
    }

    [TestMethod]
    public void Normalize_SavedCaptiveButReleased_Reattaches()
    {
        MakeEnlisted(EnlistmentState.EnlistedPlayerCaptive);
        CommanderHealthy();
        Presence(parked: false, captive: false);

        _normalizer.Normalize("main_hero", Now);

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _partyAdapter.Received(1).ParkNear("lord_1_1");
    }

    [TestMethod]
    public void Normalize_RetiredDetachedDutyState_IsNotReachableAfterParse()
    {
        // REPLACES Normalize_DetachedDuty_NoPresenceMutation, which asserted a save loading in
        // EnlistedDetachedOnDuty STAYS there. After 2026-08-09 nothing produces that state and
        // EnlistmentRecord.ToPersistedState coerces it to attached ON PARSE, so the old test was
        // pinning a save shape that can no longer exist — and pinning it as a state the player
        // would be stuck in, since the reconciler branch that serviced it is gone.
        //
        // The normalizer is downstream of that coercion, so the property worth pinning here is
        // that it does not undo it: an attached record is left attached, presence untouched.
        MakeEnlisted(EnlistmentState.EnlistedAttached);
        CommanderHealthy();
        Presence(parked: true);

        _normalizer.Normalize("main_hero", Now);

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _partyAdapter.DidNotReceive().RestorePresence();
    }
}
