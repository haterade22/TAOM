using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Two authorities now act on the same record: the real-time pump asserts continuous invariants,
/// the hourly reconciler owns terminal decisions. These tests pin the seams between them, because
/// a disagreement here does not fail loudly — it produces exactly the class of "the player is
/// stuck and nothing says why" bug the whole rewrite exists to eliminate.
/// </summary>
[TestClass]
public class EnlistmentPumpAuthorityTests
{
    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentStateMachine _machine = null!;
    private IServiceAttachmentService _attachment = null!;
    private ICommanderLordAdapter _commander = null!;
    private IEncounterAdapter _encounter = null!;
    private IDischargeService _discharge = null!;
    private EnlistmentReconciler _reconciler = null!;
    private ServiceMaintenanceService _pump = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        _machine = new EnlistmentStateMachine(_store, _logger);
        _attachment = Substitute.For<IServiceAttachmentService>();
        _commander = Substitute.For<ICommanderLordAdapter>();
        _encounter = Substitute.For<IEncounterAdapter>();
        _discharge = Substitute.For<IDischargeService>();

        var menu = Substitute.For<IGameMenuAdapter>();
        menu.EnsureMenuOpen(Arg.Any<string>()).Returns(true);
        var menuService = Substitute.For<IEnlistmentMenuService>();
        menuService.IsRedirectable(Arg.Any<string>()).Returns(true);

        _reconciler = new EnlistmentReconciler(_store, _machine, _attachment, _commander, _discharge,
            new EnlistmentConfigProvider(_logger), _encounter, new EncounterOwnershipPolicy(), Substitute.For<IEnlistmentDiagnosticsSettingsProvider>(),
            EnlistmentTestDoubles.FeatureOn(), _logger);
        _pump = new ServiceMaintenanceService(
            _store, _machine, _attachment, _commander, menu, menuService,
            Substitute.For<IServiceStatusService>(), _logger);

        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
    }

    /// <summary>
    /// The pump and the reconciler both demote a stale <c>EnlistedBattle</c>. If their predicates
    /// disagree, one re-parks the player mid-battle or the other leaves the latch closed and blocks
    /// every future join. Same inputs must give the same decision.
    /// </summary>
    [DataTestMethod]
    [DataRow(false, false, false, true, DisplayName = "nothing in progress -> demote")]
    [DataRow(true, false, false, false, DisplayName = "player in map event -> keep")]
    [DataRow(false, true, false, false, DisplayName = "commander in map event -> keep")]
    [DataRow(false, false, true, false, DisplayName = "loot encounter open -> keep")]
    [DataRow(true, true, true, false, DisplayName = "all three -> keep")]
    [DataRow(true, false, true, false, DisplayName = "player + encounter -> keep")]
    [DataRow(false, true, true, false, DisplayName = "commander + encounter -> keep")]
    [DataRow(true, true, false, false, DisplayName = "player + commander -> keep")]
    public void Reconciler_AndPump_UseIdenticalStaleBattlePredicate(
        bool playerInMapEvent, bool commanderInMapEvent, bool encounterOpen, bool expectDemote)
    {
        // --- pump ---
        _store.Record.State = EnlistmentState.EnlistedBattle;
        _commander.GetTickSnapshot(Arg.Any<string>()).Returns(new CommanderTickSnapshot(
            exists: true, isAlive: true, partyId: "lord_party_1", partyIsActive: true,
            partyIsInMapEvent: commanderInMapEvent));
        _attachment.GetPresenceFlags().Returns(new PlayerPresenceFlags(
            mainPartyExists: true, isInMapEvent: playerInMapEvent, hasPlayerEncounter: encounterOpen));

        _pump.Pump(0.25f, 100.0);
        var pumpDemoted = _store.Record.State == EnlistmentState.EnlistedAttached;

        // --- reconciler, same inputs ---
        _store.Record.State = EnlistmentState.EnlistedBattle;
        _commander.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party_1", partyIsActive: true,
            partyIsInMapEvent: commanderInMapEvent));
        _attachment.GetPresence(Arg.Any<string>()).Returns(new PlayerPresenceSnapshot(
            mainPartyExists: true, isInMapEvent: playerInMapEvent, hasPlayerEncounter: encounterOpen));
        _encounter.HasCurrent.Returns(encounterOpen);
        _attachment.Assess(Arg.Any<EnlistmentState>(), Arg.Any<CommanderSnapshot>(), Arg.Any<PlayerPresenceSnapshot>())
            .Returns(new AttachmentAssessment(AttachmentStatus.Attached));

        _reconciler.ReconcileHourly(100.0 / 24.0);
        var reconcilerDemoted = _store.Record.State == EnlistmentState.EnlistedAttached;

        Assert.AreEqual(expectDemote, pumpDemoted, "pump disagreed with the expected decision");
        Assert.AreEqual(pumpDemoted, reconcilerDemoted,
            "the pump and the hourly reconciler must reach the SAME stale-battle decision for identical inputs");
    }

    /// <summary>
    /// The shared retry budget is stored in campaign HOURS. The pump is handed hours; the
    /// reconciler is handed DAYS and converts. If that conversion ever drifts, the budget check
    /// silently suppresses hourly recovery entirely — restoring exactly the never-joins-a-battle
    /// bug this work exists to fix. Assert the equivalence rather than trusting a comment.
    /// </summary>
    [TestMethod]
    public void Reconciler_ConvertsDaysToTheSameHoursThePumpUses()
    {
        const double nowDays = 137.5;
        const double expectedHours = nowDays * 24.0;

        _store.Record.NextAttachRetryAtHours = expectedHours + 0.5;   // budget NOT yet expired
        _commander.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party_1", partyIsActive: true,
            partyIsInMapEvent: true));
        _attachment.GetPresence(Arg.Any<string>()).Returns(new PlayerPresenceSnapshot(mainPartyExists: true, isActive: false, isVisible: false));
        _attachment.Assess(Arg.Any<EnlistmentState>(), Arg.Any<CommanderSnapshot>(), Arg.Any<PlayerPresenceSnapshot>())
            .Returns(new AttachmentAssessment(AttachmentStatus.BattleJoinRequired));
        var raised = 0;
        _reconciler.BattleJoinRequested += _ => raised++;

        _reconciler.ReconcileHourly(nowDays);

        Assert.AreEqual(0, raised, "budget not expired in HOURS — the reconciler must be converting days*24");

        _store.Record.NextAttachRetryAtHours = expectedHours - 0.5;   // now expired
        _reconciler.ReconcileHourly(nowDays);

        Assert.AreEqual(1, raised, "budget expired — recovery must fire, or S2 returns");
    }

    [TestMethod]
    public void Reconciler_BattleJoinRequired_NoBudgetSet_RaisesImmediately()
    {
        _store.Record.NextAttachRetryAtHours = null;
        _commander.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party_1", partyIsActive: true, partyIsInMapEvent: true));
        _attachment.GetPresence(Arg.Any<string>()).Returns(new PlayerPresenceSnapshot(mainPartyExists: true, isActive: false, isVisible: false));
        _attachment.Assess(Arg.Any<EnlistmentState>(), Arg.Any<CommanderSnapshot>(), Arg.Any<PlayerPresenceSnapshot>())
            .Returns(new AttachmentAssessment(AttachmentStatus.BattleJoinRequired));
        var raised = 0;
        _reconciler.BattleJoinRequested += _ => raised++;

        _reconciler.ReconcileHourly(100.0);

        Assert.AreEqual(1, raised);
        Assert.IsTrue(_store.Record.NextAttachRetryAtHours.HasValue, "the budget must be advanced after raising");
    }
}
