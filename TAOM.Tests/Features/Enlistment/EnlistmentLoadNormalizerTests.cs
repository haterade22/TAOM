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
        var attachment = new ServiceAttachmentService(_partyAdapter, Substitute.For<IGameMenuAdapter>(), _logger);
        var discharge = new DischargeService(_store, _machine, _partyAdapter, Substitute.For<IEncounterAdapter>(), new EncounterOwnershipPolicy(), Substitute.For<ICommanderLordAdapter>(), Substitute.For<IGameMenuAdapter>(), _logger);
        var reconciler = new EnlistmentReconciler(_store, _machine, attachment, _commander, discharge,
            new EnlistmentConfigProvider(_logger), Substitute.For<IEncounterAdapter>(), new EncounterOwnershipPolicy(), Substitute.For<IEnlistmentDiagnosticsSettingsProvider>(), _logger);
        _normalizer = new EnlistmentLoadNormalizer(
            _store, _machine, reconciler, _partyAdapter, discharge, _logger);
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

    private void Presence(bool parked, bool captive = false)
    {
        _partyAdapter.GetPresence().Returns(new PlayerPresenceSnapshot(
            mainPartyExists: true, isCaptive: captive, isActive: !parked, isVisible: !parked));
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
    public void Normalize_DetachedDuty_NoPresenceMutation()
    {
        MakeEnlisted(EnlistmentState.EnlistedDetachedOnDuty);
        CommanderHealthy();
        Presence(parked: false);

        _normalizer.Normalize("main_hero", Now);

        Assert.AreEqual(EnlistmentState.EnlistedDetachedOnDuty, _store.Record.State);
        _partyAdapter.DidNotReceive().ParkNear(Arg.Any<string>());
        _partyAdapter.DidNotReceive().RestorePresence();
    }
}
