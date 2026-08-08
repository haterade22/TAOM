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
        _attachment.GetPresence().Returns(new PlayerPresenceSnapshot(mainPartyExists: true));
        _attachment.Assess(Arg.Any<EnlistmentState>(), Arg.Any<CommanderSnapshot>(), Arg.Any<PlayerPresenceSnapshot>())
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
            _feature, logger);
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
