using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The MCM master switch. The interesting case is not "off blocks enlisting" — it is what happens
/// to someone ALREADY serving when the switch is flipped, because an enlisted player is parked
/// hidden and inactive, and the code that restores them is the code being switched off.
/// </summary>
[TestClass]
public class EnlistmentFeatureToggleTests
{
    private IEnlistmentFeatureSettingsProvider _feature;
    private IEnlistmentStore _store;
    private IDischargeService _discharge;
    private ICommanderLordAdapter _commander;
    private IServiceAttachmentService _attachment;
    private EnlistmentReconciler _reconciler;

    [TestInitialize]
    public void Setup()
    {
        var logger = Substitute.For<IModLogger>();
        _feature = Substitute.For<IEnlistmentFeatureSettingsProvider>();
        _feature.IsEnabled.Returns(true);

        var store = new EnlistmentStore(logger);
        _store = store;
        _discharge = Substitute.For<IDischargeService>();
        _commander = Substitute.For<ICommanderLordAdapter>();
        _attachment = Substitute.For<IServiceAttachmentService>();
        _attachment.GetPresence(Arg.Any<string>()).Returns(new PlayerPresenceSnapshot(mainPartyExists: true));
        _attachment.Assess(Arg.Any<EnlistmentState>(), Arg.Any<CommanderSnapshot>(), Arg.Any<PlayerPresenceSnapshot>(), Arg.Any<bool>())
            .Returns(new AttachmentAssessment(AttachmentStatus.Attached));
        _commander.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party", partyIsActive: true));

        store.Record.State = EnlistmentState.EnlistedAttached;
        store.Record.EnlistedHeroId = "main_hero";
        store.Record.CommanderHeroId = "lord_1";
        store.Record.EnlistedAtDay = 100.0;

        _reconciler = new EnlistmentReconciler(
            _store, new EnlistmentStateMachine(store, logger), _attachment, _commander, _discharge,
            new EnlistmentConfigProvider(logger), Substitute.For<IEncounterAdapter>(),
            new EncounterOwnershipPolicy(), Substitute.For<IEnlistmentDiagnosticsSettingsProvider>(),
            _feature, Substitute.For<IInquiryAdapter>(),
            Substitute.For<IArmyMembershipAdapter>(), logger);
    }

    private EnlistmentDialogGateService Gate()
    {
        var config = Substitute.For<IEnlistmentConfigProvider>();
        config.GetConfig().Returns(new EnlistmentCoreConfig());
        var commander = Substitute.For<ICommanderLordAdapter>();
        commander.IsLord(Arg.Any<string>()).Returns(true);
        commander.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party", partyIsActive: true));
        var store = new EnlistmentStore(Substitute.For<IModLogger>());
        return new EnlistmentDialogGateService(
            store, commander, Substitute.For<IPlayerContextAdapter>(), config, _feature);
    }

    // ---- the switch blocks new service ------------------------------------------------------

    [TestMethod]
    public void CanEnlistWith_FeatureDisabled_ReturnsFeatureDisabled()
    {
        _feature.IsEnabled.Returns(false);

        Assert.AreEqual(EnlistGateResult.FeatureDisabled, Gate().CanEnlistWith("lord_1"));
    }

    [TestMethod]
    public void CanEnlistWith_FeatureEnabled_UnaffectedByTheSwitch()
    {
        Assert.AreEqual(EnlistGateResult.Ok, Gate().CanEnlistWith("lord_1"));
    }

    [TestMethod]
    public void CanEnlistWith_NoProvider_StillWorks()
    {
        // MCM absent must not disable the feature.
        var config = Substitute.For<IEnlistmentConfigProvider>();
        config.GetConfig().Returns(new EnlistmentCoreConfig());
        var commander = Substitute.For<ICommanderLordAdapter>();
        commander.IsLord(Arg.Any<string>()).Returns(true);
        commander.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party", partyIsActive: true));
        var gate = new EnlistmentDialogGateService(
            new EnlistmentStore(Substitute.For<IModLogger>()), commander,
            Substitute.For<IPlayerContextAdapter>(), config, null);

        Assert.AreEqual(EnlistGateResult.Ok, gate.CanEnlistWith("lord_1"));
    }

    // ---- the switch releases anyone mid-service ---------------------------------------------

    [TestMethod]
    public void ReconcileHourly_FeatureDisabledWhileServing_DischargesHonourably()
    {
        // THE pin. Halting in place would leave the player hidden, inactive and menu-less on the
        // map — a soft-lock produced by a settings toggle. One discharge through the normal
        // pipeline instead, which restores presence and hands them back somewhere they can act.
        _feature.IsEnabled.Returns(false);

        _reconciler.ReconcileHourly(200.0);

        _discharge.Received(1).Execute(DischargeReason.PlayerRequest);
    }

    [TestMethod]
    public void ReconcileHourly_FeatureDisabled_DoesNotAlsoRunNormalReconciliation()
    {
        // Releasing and then re-parking in the same pass would undo the release.
        _feature.IsEnabled.Returns(false);

        _reconciler.ReconcileHourly(200.0);

        _attachment.DidNotReceive().EnsureParked(Arg.Any<string>());
        _attachment.DidNotReceive().SyncPosition(Arg.Any<string>());
    }

    [TestMethod]
    public void ReconcileHourly_FeatureDisabledButNotEnlisted_DoesNothing()
    {
        _feature.IsEnabled.Returns(false);
        _store.Record.State = EnlistmentState.NotEnlisted;

        _reconciler.ReconcileHourly(200.0);

        _discharge.DidNotReceiveWithAnyArgs().Execute(default);
    }

    [TestMethod]
    public void ReconcileHourly_FeatureEnabled_ReconcilesNormally()
    {
        _reconciler.ReconcileHourly(200.0);

        _discharge.DidNotReceiveWithAnyArgs().Execute(default);
    }

    // ---- fail direction ----------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(null, true, DisplayName = "MCM absent -> enabled")]
    [DataRow(true, true)]
    [DataRow(false, false)]
    public void ResolveEnabled_FailsOpen(bool? raw, bool expected)
    {
        // Must match the compiled default of TaomSettings.EnableEnlistment. A feature that
        // silently disables itself whenever MCM is missing is worse than one that stays on.
        Assert.AreEqual(expected, EnlistmentFeatureSettingsProvider.ResolveEnabled(raw));
    }

    [TestMethod]
    public void ResolveEnabled_DefaultMatchesTheCompiledSettingDefault()
    {
        Assert.AreEqual(new TaomSettings().EnableEnlistment,
            EnlistmentFeatureSettingsProvider.ResolveEnabled(null));
    }
}

/// <summary>
/// Guards added by the overnight review pass. Each pins a defect that was found by reading rather
/// than by a failing test — which is precisely why they need tests now.
/// </summary>
[TestClass]
public class EnlistmentReviewGuardTests
{
    private IEnlistmentConfigProvider _config;
    private IDischargeService _discharge;
    private ICommanderLordAdapter _commander;
    private IServiceAttachmentService _attachment;
    private EnlistmentStore _store;
    private EnlistmentReconciler _reconciler;

    [TestInitialize]
    public void Setup()
    {
        var logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(logger);
        _config = Substitute.For<IEnlistmentConfigProvider>();
        _discharge = Substitute.For<IDischargeService>();
        _commander = Substitute.For<ICommanderLordAdapter>();
        _attachment = Substitute.For<IServiceAttachmentService>();

        _attachment.GetPresence(Arg.Any<string>()).Returns(new PlayerPresenceSnapshot(mainPartyExists: true));
        _attachment.Assess(Arg.Any<EnlistmentState>(), Arg.Any<CommanderSnapshot>(), Arg.Any<PlayerPresenceSnapshot>(), Arg.Any<bool>())
            .Returns(new AttachmentAssessment(AttachmentStatus.Blocked, AttachmentBlockReason.CommanderPartyMissing));
        // Commander captured: alive, but party-less — the shape that starts a grace window.
        _commander.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(
            exists: true, isAlive: true, isPrisoner: true, partyId: null));

        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1";

        _reconciler = new EnlistmentReconciler(
            _store, new EnlistmentStateMachine(_store, logger), _attachment, _commander, _discharge,
            _config, Substitute.For<IEncounterAdapter>(), new EncounterOwnershipPolicy(),
            Substitute.For<IEnlistmentDiagnosticsSettingsProvider>(),
            EnlistmentTestDoubles.FeatureOn(), Substitute.For<IInquiryAdapter>(),
            Substitute.For<IArmyMembershipAdapter>(), logger);
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    [DataRow(0.0)]
    [DataRow(-5.0)]
    public void GraceDeadline_DegenerateConfig_StillProducesAnExpiringWindow(double graceDays)
    {
        // The sixth instance of the NaN-gate bug class, caught before shipping. `nowDays >=
        // GraceEndsAtDay` is FALSE FOREVER against a NaN deadline, so grace would never expire and
        // the player would sit in CommanderUnavailable permanently with no auto-discharge — a
        // soft-lock produced by one bad config number. A negative value is the opposite failure:
        // instant discharge the moment the commander blinks.
        _config.GetConfig().Returns(new EnlistmentCoreConfig { CommanderGraceDays = graceDays });

        _reconciler.ReconcileHourly(200.0);

        Assert.AreEqual(EnlistmentState.CommanderUnavailable, _store.Record.State);
        Assert.IsTrue(_store.Record.GraceEndsAtDay.HasValue, "a grace window must have been opened");

        var deadline = _store.Record.GraceEndsAtDay.Value;
        Assert.IsFalse(double.IsNaN(deadline), "a NaN deadline never expires — the player is stuck forever");
        Assert.IsFalse(double.IsInfinity(deadline), "an infinite deadline never expires either");
        Assert.IsTrue(deadline > 200.0, "the window must be in the FUTURE, or service ends the moment it starts");
    }

    [TestMethod]
    public void GraceDeadline_UsableConfig_IsHonoured()
    {
        _config.GetConfig().Returns(new EnlistmentCoreConfig { CommanderGraceDays = 3.0 });

        _reconciler.ReconcileHourly(200.0);

        Assert.AreEqual(203.0, _store.Record.GraceEndsAtDay.Value, 0.0001);
    }

    [TestMethod]
    public void ReconcileNow_ReentrantCall_DoesNotRecurse()
    {
        // The hazard is live, not theoretical: settlement following makes the reconciler call
        // LeaveSettlementAction, which dispatches OnSettlementLeft, which is now subscribed and
        // routes straight back to ReconcileNow — on a record the outer pass is mid-way through
        // mutating.
        _config.GetConfig().Returns(new EnlistmentCoreConfig());
        var reentered = 0;
        _attachment.When(a => a.GetPresence(Arg.Any<string>())).Do(_ =>
        {
            if (reentered++ == 0)
                _reconciler.ReconcileNow(200.0, "reentrant edge");
        });

        _reconciler.ReconcileHourly(200.0);

        Assert.AreEqual(1, reentered, "the inner call must have been refused by the in-flight guard");
    }
}
