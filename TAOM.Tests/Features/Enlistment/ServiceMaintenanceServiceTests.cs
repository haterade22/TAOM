using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class ServiceMaintenanceServiceTests
{
    private const float Interval = 0.25f;

    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentStateMachine _machine = null!;
    private IServiceAttachmentService _attachment = null!;
    private ICommanderLordAdapter _commander = null!;
    private IGameMenuAdapter _gameMenu = null!;
    private IEnlistmentMenuService _menuService = null!;
    private ServiceMaintenanceService _pump = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        _machine = new EnlistmentStateMachine(_store, _logger);
        _attachment = Substitute.For<IServiceAttachmentService>();
        _commander = Substitute.For<ICommanderLordAdapter>();
        _gameMenu = Substitute.For<IGameMenuAdapter>();
        _menuService = Substitute.For<IEnlistmentMenuService>();
        _gameMenu.EnsureMenuOpen(Arg.Any<string>()).Returns(true);
        _menuService.IsRedirectable(Arg.Any<string>()).Returns(true);

        _pump = new ServiceMaintenanceService(
            _store, _machine, _attachment, _commander, _gameMenu, _menuService, _logger);

        Commander(followable: true);
        Presence(parked: true);
    }

    private void MakeEnlisted(EnlistmentState state = EnlistmentState.EnlistedAttached)
    {
        _store.Record.State = state;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
    }

    private void Commander(bool followable = true, bool inMapEvent = false, int token = 0)
    {
        _commander.GetTickSnapshot(Arg.Any<string>()).Returns(new CommanderTickSnapshot(
            exists: followable, isAlive: followable, isPrisoner: false,
            partyId: followable ? "lord_party_1" : null, partyIsActive: followable,
            partyIsInMapEvent: inMapEvent, mapEventToken: token));
    }

    private void Presence(bool parked = true, bool inMapEvent = false, bool encounter = false)
    {
        _attachment.GetPresenceFlags().Returns(new PlayerPresenceFlags(
            mainPartyExists: true, isActive: !parked, isVisible: !parked,
            isInMapEvent: inMapEvent, hasPlayerEncounter: encounter));
    }

    /// <summary>Pump enough to cross the throttle and reach the expensive tier.</summary>
    private void PumpExpensive(double nowHours = 100.0) => _pump.Pump(Interval, nowHours);

    // ---- gating -------------------------------------------------------------------------

    [TestMethod]
    public void Pump_NotEnlisted_DoesNothing()
    {
        _pump.Pump(1f, 100.0);

        _attachment.DidNotReceive().SyncPositionCached(Arg.Any<string>(), Arg.Any<string>());
        _commander.DidNotReceive().GetTickSnapshot(Arg.Any<string>());
    }

    [TestMethod]
    public void Pump_CheapTierSyncsEveryPass_EvenBelowTheThrottle()
    {
        MakeEnlisted();

        _pump.Pump(0.001f, 100.0);
        _pump.Pump(0.001f, 100.0);

        _attachment.Received(2).SyncPositionCached("lord_1_1", Arg.Any<string>());
        _commander.DidNotReceive().GetTickSnapshot(Arg.Any<string>());   // expensive tier not reached
    }

    [TestMethod]
    public void Pump_ExpensiveTierMakesExactlyOneCommanderLookupPerPass()
    {
        MakeEnlisted();

        PumpExpensive();

        _commander.Received(1).GetTickSnapshot("lord_1_1");
        _commander.DidNotReceive().GetSnapshot(Arg.Any<string>());   // the allocating read is forbidden here
    }

    [TestMethod]
    public void Pump_NaNDt_ContributesNothingToBudget()
    {
        // A NaN would otherwise poison the accumulator permanently: NaN propagates through every
        // later addition and every comparison against it is false, so the expensive tier would
        // never fire again for the rest of the campaign.
        MakeEnlisted();

        for (var i = 0; i < 1000; i++)
            _pump.Pump(float.NaN, 100.0);

        _commander.DidNotReceive().GetTickSnapshot(Arg.Any<string>());
    }

    [TestMethod]
    public void Pump_InfiniteDt_ContributesNothingToBudget()
    {
        MakeEnlisted();

        _pump.Pump(float.PositiveInfinity, 100.0);

        _commander.DidNotReceive().GetTickSnapshot(Arg.Any<string>());
    }

    // ---- battle latch -------------------------------------------------------------------

    [TestMethod]
    public void Pump_StaleBattleLatchWithNothingInProgress_DemotesToAttached()
    {
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        Commander(followable: true, inMapEvent: false);
        Presence(parked: true, inMapEvent: false, encounter: false);

        PumpExpensive();

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
    }

    [TestMethod]
    public void Pump_BattleStateWithOpenEncounter_DoesNotDemote()
    {
        // The loot/aftermath encounter is still open — demoting would re-park mid-battle.
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        Presence(parked: false, inMapEvent: false, encounter: true);

        PumpExpensive();

        Assert.AreEqual(EnlistmentState.EnlistedBattle, _store.Record.State);
    }

    // ---- join request -------------------------------------------------------------------

    [TestMethod]
    public void Pump_CommanderInBattlePlayerNot_RaisesJoinRequest()
    {
        MakeEnlisted();
        Commander(followable: true, inMapEvent: true, token: 7);
        string requested = null;
        _pump.BattleJoinRequested += id => requested = id;

        PumpExpensive();

        Assert.AreEqual("lord_1_1", requested);
        Assert.IsTrue(_store.Record.PendingCommanderAttachment);
    }

    [TestMethod]
    public void Pump_PlayerAlreadyInMapEvent_DoesNotRaise()
    {
        MakeEnlisted();
        Commander(followable: true, inMapEvent: true, token: 7);
        Presence(parked: false, inMapEvent: true);
        var raised = 0;
        _pump.BattleJoinRequested += _ => raised++;

        PumpExpensive();

        Assert.AreEqual(0, raised);
    }

    [TestMethod]
    public void Pump_SameBattleWithinBudget_DoesNotRaiseTwice()
    {
        MakeEnlisted();
        Commander(followable: true, inMapEvent: true, token: 7);
        var raised = 0;
        _pump.BattleJoinRequested += _ => raised++;

        PumpExpensive(100.0);
        PumpExpensive(100.1);   // same battle, budget not expired

        Assert.AreEqual(1, raised);
    }

    [TestMethod]
    public void Pump_SameBattleAfterBudgetExpires_RaisesAgain()
    {
        MakeEnlisted();
        Commander(followable: true, inMapEvent: true, token: 7);
        var raised = 0;
        _pump.BattleJoinRequested += _ => raised++;

        PumpExpensive(100.0);
        PumpExpensive(200.0);   // well past the 1-hour budget

        Assert.AreEqual(2, raised);
    }

    [TestMethod]
    public void Pump_DifferentBattle_RaisesImmediatelyRegardlessOfBudget()
    {
        MakeEnlisted();
        Commander(followable: true, inMapEvent: true, token: 7);
        var raised = 0;
        _pump.BattleJoinRequested += _ => raised++;
        PumpExpensive(100.0);

        Commander(followable: true, inMapEvent: true, token: 8);   // a genuinely new fight
        PumpExpensive(100.1);

        Assert.AreEqual(2, raised);
    }

    [TestMethod]
    public void Pump_CommanderNotFollowable_DoesNotRaise()
    {
        MakeEnlisted();
        _commander.GetTickSnapshot(Arg.Any<string>()).Returns(new CommanderTickSnapshot(
            exists: true, isAlive: true, isPrisoner: true, partyId: "p", partyIsActive: true,
            partyIsInMapEvent: true, mapEventToken: 7));
        var raised = 0;
        _pump.BattleJoinRequested += _ => raised++;

        PumpExpensive();

        Assert.AreEqual(0, raised);
    }

    [TestMethod]
    public void Pump_JoinSubscriberThrows_DoesNotPropagate()
    {
        MakeEnlisted();
        Commander(followable: true, inMapEvent: true, token: 7);
        _pump.BattleJoinRequested += _ => throw new System.InvalidOperationException("boom");

        PumpExpensive();   // must not throw

        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("subscriber threw")));
    }

    // ---- the running-battle edge --------------------------------------------------------

    [TestMethod]
    public void PartyJoinedRunningMapEvent_CommanderMatches_ArmsAnImmediateRetry()
    {
        // MapEventStarted cannot see this: it is dispatched exactly once, at the end of
        // MapEvent.Initialize, so a commander joining a fight in progress is otherwise invisible.
        MakeEnlisted();
        Commander(followable: true, inMapEvent: false);
        PumpExpensive();                     // caches the commander party id
        _store.Record.NextAttachRetryAtHours = 500.0;

        _pump.OnPartyJoinedRunningMapEvent("lord_party_1");

        Assert.IsTrue(_store.Record.PendingCommanderAttachment);
        Assert.IsNull(_store.Record.NextAttachRetryAtHours, "a real edge must clear the retry budget");
    }

    [TestMethod]
    public void PartyJoinedRunningMapEvent_SomeoneElse_IsIgnored()
    {
        MakeEnlisted();
        PumpExpensive();

        _pump.OnPartyJoinedRunningMapEvent("some_other_party");

        Assert.IsFalse(_store.Record.PendingCommanderAttachment);
    }

    [TestMethod]
    public void PartyJoinedRunningMapEvent_NotEnlisted_IsIgnored()
    {
        _pump.OnPartyJoinedRunningMapEvent("lord_party_1");

        Assert.IsFalse(_store.Record.PendingCommanderAttachment);
    }

    // ---- menu -----------------------------------------------------------------------------

    [TestMethod]
    public void Pump_ParkedAndOffTheWaitMenu_RestoresIt()
    {
        MakeEnlisted();
        _gameMenu.CurrentMenuId.Returns((string)null);

        PumpExpensive();

        _gameMenu.Received(1).EnsureMenuOpen(EnlistmentMenuService.ServiceWaitMenuId);
    }

    [TestMethod]
    public void Pump_EnlistedBattle_DoesNotTouchTheMenu()
    {
        // Load-bearing: "encounter" and "join_encounter" are in the redirect list, so asserting the
        // wait menu during battle would eat the battle, loot and aftermath menus.
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        Presence(parked: false, inMapEvent: true);
        _gameMenu.CurrentMenuId.Returns("encounter");

        PumpExpensive();

        _gameMenu.DidNotReceive().EnsureMenuOpen(Arg.Any<string>());
    }

    [TestMethod]
    public void Pump_OnAMenuWeDoNotOwn_LeavesItAlone()
    {
        MakeEnlisted();
        _gameMenu.CurrentMenuId.Returns("some_quest_menu");
        _menuService.IsRedirectable("some_quest_menu").Returns(false);

        PumpExpensive();

        _gameMenu.DidNotReceive().EnsureMenuOpen(Arg.Any<string>());
    }

    [TestMethod]
    public void Pump_AlreadyOnWaitMenu_DoesNotReopen()
    {
        MakeEnlisted();
        _gameMenu.CurrentMenuId.Returns(EnlistmentMenuService.ServiceWaitMenuId);

        PumpExpensive();

        _gameMenu.DidNotReceive().EnsureMenuOpen(Arg.Any<string>());
    }

    [TestMethod]
    public void Pump_MenuKeepsFailing_BacksOffInsteadOfSpinning()
    {
        MakeEnlisted();
        _gameMenu.CurrentMenuId.Returns((string)null);
        _gameMenu.EnsureMenuOpen(Arg.Any<string>()).Returns(false);

        for (var i = 0; i < 10; i++)
            PumpExpensive();

        _gameMenu.Received(3).EnsureMenuOpen(EnlistmentMenuService.ServiceWaitMenuId);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("backing off")));
    }

    [TestMethod]
    public void Pump_NotParked_DoesNotForceTheMenu()
    {
        MakeEnlisted();
        Presence(parked: false);
        _gameMenu.CurrentMenuId.Returns((string)null);

        PumpExpensive();

        _gameMenu.DidNotReceive().EnsureMenuOpen(Arg.Any<string>());
    }

    // ---- cross-authority pins -------------------------------------------------------------

    [TestMethod]
    public void Pump_NeverExecutesDischarge()
    {
        // Ownership contract: the hourly reconciler is the ONLY terminal authority. The pump must
        // not even be ABLE to discharge, so assert on its constructor surface rather than on
        // behaviour that a future edit could add.
        var takesDischarge = typeof(ServiceMaintenanceService)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Any(p => p.ParameterType == typeof(IDischargeService));

        Assert.IsFalse(takesDischarge,
            "ServiceMaintenanceService must not inject IDischargeService — terminal decisions belong to the hourly reconciler.");
    }
}
