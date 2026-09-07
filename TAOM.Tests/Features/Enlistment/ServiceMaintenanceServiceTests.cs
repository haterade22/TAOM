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
    private IServiceStatusService _status = null!;
    private IArmyMembershipAdapter _army = null!;
    private IEncounterAdapter _encounter = null!;
    private IEnlistmentReconciler _reconciler = null!;
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

        _status = Substitute.For<IServiceStatusService>();
        _army = Substitute.For<IArmyMembershipAdapter>();
        _reconciler = Substitute.For<IEnlistmentReconciler>();
        _encounter = Substitute.For<IEncounterAdapter>();
        _encounter.Finish(Arg.Any<bool>()).Returns(true);
        Encounter(none: true);

        _pump = new ServiceMaintenanceService(
            _store, _machine, _attachment, _commander, _gameMenu, _menuService,
            _status, _army, _encounter, new EncounterOwnershipPolicy(),
            _reconciler, _logger);

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

    private void Presence(bool parked = true, bool inMapEvent = false, bool encounter = false,
        string settlementId = null)
    {
        _attachment.GetPresenceFlags().Returns(new PlayerPresenceFlags(
            mainPartyExists: true, isActive: !parked, isVisible: !parked,
            isInMapEvent: inMapEvent, hasPlayerEncounter: encounter, settlementId: settlementId));
    }

    /// <summary>
    /// What <c>IEncounterAdapter.GetOwnership</c> reports. <paramref name="none"/> is the steady
    /// state; a settlement encounter is the shape shore leave opens (no encountered mobile party),
    /// and a party encounter is someone else's.
    /// </summary>
    private void Encounter(bool none = false, bool partyShaped = false, bool playerInMapEvent = false)
    {
        _encounter.GetOwnership(Arg.Any<string>()).Returns(new EncounterOwnershipSnapshot(
            hasEncounter: !none,
            conversationInProgress: false,
            hasEncounteredMobileParty: partyShaped,
            encounteredPartyId: partyShaped ? "lord_party_1" : null,
            encounteredPartyIsCommanderRelated: partyShaped,
            playerInMapEvent: playerInMapEvent));
    }

    /// <summary>Pump enough to cross the throttle and reach the expensive tier.</summary>
    private void PumpExpensive(double nowHours = 100.0) => _pump.Pump(Interval, nowHours);

    // ---- per-session cache lifetime -----------------------------------------------------

    /// <summary>
    /// This method is the one place that knows how long the feature's per-session state lives, so
    /// every collaborator cache is dropped from here rather than being wired separately into the
    /// load hook.
    ///
    /// The army adapter's is the one that hurts if it is missed: it holds a live <c>Army</c>
    /// REFERENCE on a singleton whose container outlives the campaign. After a reload that handle
    /// names either a dead campaign's object or an instance this world no longer contains, and the
    /// identity test in <c>LeaveArmy</c> can then never match again for the rest of the process —
    /// so the army raised for a battle would never be disbanded. A bare-ctor army carries a null
    /// <c>AiBehaviorObject</c>, which crashes <c>Army.GetLongTermBehaviorTextForAILeadedParty</c>
    /// from the map party tooltip and the kingdom Armies tab, and survives every later reload.
    /// </summary>
    [TestMethod]
    public void ResetSessionCaches_AlsoDropsTheArmyAdapterHandle()
    {
        _pump.ResetSessionCaches();

        _army.Received(1).ResetSessionCaches();
        _attachment.Received(1).InvalidateCommanderCache();
        _status.Received(1).Invalidate();
        _reconciler.Received(1).ResetForNewSession();
    }

    [TestMethod]
    public void ResetSessionCaches_AlsoDropsTheReconcilersStaleBattleLatchAnchor()
    {
        // Issue #551. The reconciler is Reuse.Singleton and its latch anchor is an ABSOLUTE campaign
        // day, so a campaign that ended while latched leaves it behind. Load a later save and the
        // elapsed time is enormous: the recovery fires on the first latched tick and finishes what
        // may be a live loot screen with no real waiting, which is the destructive Finish that R1b
        // exists to prevent, committed by the safety net itself.
        //
        // Asserted separately from the army handle above so that deleting either wiring names which
        // one broke.
        _pump.ResetSessionCaches();

        _reconciler.Received(1).ResetForNewSession();
    }

    [TestMethod]
    public void Pump_DoesNotResetTheReconcilerAnchor()
    {
        // Same reasoning as the army handle: this is a LOAD-time action. Clearing the anchor on an
        // ordinary pump would restart the clock every tick and the recovery could never elapse.
        MakeEnlisted();

        PumpExpensive();

        _reconciler.DidNotReceive().ResetForNewSession();
    }

    [TestMethod]
    public void Pump_DoesNotResetTheArmyAdapterCache()
    {
        // The reset is a LOAD-time action. Dropping the handle on an ordinary pump would orphan an
        // army raised moments earlier for a battle still in progress.
        MakeEnlisted();

        PumpExpensive();

        _army.DidNotReceive().ResetSessionCaches();
    }

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
    // ---- the wait-menu board while the commander is lost ----

    [TestMethod]
    public void Pump_CommanderUnavailable_RefreshesTheStatusBoard()
    {
        // ServiceStatusTextWriter has returned "You have lost the column. Await word of your
        // commander." for this case since it was written, registered and translated into all 12
        // languages — and RefreshStatusBoard's `!= EnlistedAttached` early return was the only
        // reason it could never render. A player whose commander's party was destroyed got no
        // message anywhere while the message already existed.
        MakeEnlisted(EnlistmentState.CommanderUnavailable);
        Commander(followable: false);

        _pump.Pump(5f, 0.0);

        _status.Received().RefreshIfChanged();
    }

    [TestMethod]
    public void Pump_PlayerCaptive_DoesNotRefreshTheStatusBoard()
    {
        // The guard on the fix. Widening to IsEnlisted would have been the obvious move and is
        // wrong: ServiceStatusService.ResolveActivity reads the COMMANDER's activity, so a captive
        // player whose commander is fine would be told "You march with X's company" from a cell.
        // Exactly one state was added, not a predicate.
        MakeEnlisted(EnlistmentState.EnlistedPlayerCaptive);

        _pump.Pump(5f, 0.0);

        _status.DidNotReceive().RefreshIfChanged();
    }

    // ---- Shore leave revoke closes the encounter the pass opened (issue #510) ----------------
    // TakeTownLeave establishes a settlement PlayerEncounter so the vanilla town menu is safe.
    // Nothing else will ever close it: EncounterOwnershipPolicy R3 returns SkipNotOurs for an
    // encounter with no encountered MOBILE party, which is exactly a settlement encounter's shape,
    // so the reconciler's stranded-encounter sweep deliberately walks past it. A leak here blocks
    // every future main-party encounter for the rest of the term.

    [TestMethod]
    public void Pump_ShoreLeaveRevoked_FinishesTheEncounterThePassOpened()
    {
        MakeEnlisted();
        _store.Record.OnTownLeave = true;
        Presence(parked: false, settlementId: null);
        Encounter(); // settlement shape: live, no encountered mobile party

        PumpExpensive();

        Assert.IsFalse(_store.Record.OnTownLeave);
        _encounter.Received(1).Finish(false);
    }

    [TestMethod]
    public void Pump_ShoreLeaveStillValid_LeavesTheEncounterAlone()
    {
        MakeEnlisted();
        _store.Record.OnTownLeave = true;
        Presence(parked: false, encounter: true, settlementId: "town_EW1");
        Encounter();

        PumpExpensive();

        Assert.IsTrue(_store.Record.OnTownLeave);
        _encounter.DidNotReceive().Finish(Arg.Any<bool>());
    }

    /// <summary>
    /// Leave is revoked for EVERY reason that is not attached-and-in-a-settlement, and one of them
    /// is "a battle started". At that moment the live encounter is the one ServiceBattleService
    /// seeded, and destroying it freezes the map event: MapEventManager.Tick skips the player's own
    /// event and only PlayerEncounter.Update advances it. The revoke goes through the ownership
    /// policy for exactly this case.
    /// </summary>
    [TestMethod]
    public void Pump_ShoreLeaveRevokedDuringABattle_DoesNotDestroyTheBattleEncounter()
    {
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        _store.Record.OnTownLeave = true;
        Presence(parked: false, inMapEvent: true, encounter: true, settlementId: null);
        Encounter(partyShaped: true, playerInMapEvent: true);

        PumpExpensive();

        Assert.IsFalse(_store.Record.OnTownLeave);
        _encounter.DidNotReceive().Finish(Arg.Any<bool>());
    }

    /// <summary>
    /// The revoke also fires on a STATE change, not only when the column marches, so it can run
    /// while the player is still standing in the town. Vanilla `PlayerEncounter.Finish` always
    /// stops time and calls `GameMenu.ExitToLast()`, and only walks the player out when
    /// forcePlayerOutFromSettlement is true. Passing false there strands them inside a settlement
    /// with no menu, which `MobileParty.DoUpdatePosition` will not move.
    /// </summary>
    [TestMethod]
    public void Pump_ShoreLeaveRevokedWhileStillInsideTheSettlement_ForcesThePlayerOut()
    {
        MakeEnlisted(EnlistmentState.CommanderUnavailable);
        _store.Record.OnTownLeave = true;
        Presence(parked: false, settlementId: "town_EW1");
        _encounter.IsInsideSettlement.Returns(true);
        Encounter();

        PumpExpensive();

        Assert.IsFalse(_store.Record.OnTownLeave);
        _encounter.Received(1).Finish(true);
    }

    /// <summary>
    /// The force flag must come from the ENGINE's predicate, not ours. Vanilla's
    /// <c>PlayerEncounter.InsideSettlement</c> is <c>MainParty.IsActive &amp;&amp; CurrentSettlement != null</c>;
    /// <c>PlayerPresenceFlags.IsInSettlement</c> carries only the settlement id. Enlistment parks
    /// the party INACTIVE without leaving the settlement, so the two disagree, and in that shape
    /// vanilla's eject is unreachable no matter what we pass. Asking the engine keeps the argument
    /// honest instead of asserting a walk-out that silently will not happen.
    /// </summary>
    [TestMethod]
    public void Pump_ShoreLeaveRevokedWhileParkedInactiveInsideASettlement_DoesNotClaimToEjectThem()
    {
        MakeEnlisted(EnlistmentState.CommanderUnavailable);
        _store.Record.OnTownLeave = true;
        Presence(parked: true, settlementId: "town_EW1");   // TAOM says inside...
        _encounter.IsInsideSettlement.Returns(false);        // ...the engine says no, party inactive
        Encounter();

        PumpExpensive();

        _encounter.Received(1).Finish(false);
    }

    [TestMethod]
    public void Pump_NoShoreLeave_NeverFinishesAnEncounter()
    {
        // The revoke owns exactly one encounter: the one the pass opened. An encounter the player
        // or the battle service owns is not this pump's to close.
        MakeEnlisted();
        Presence(parked: true, encounter: true);
        Encounter();

        PumpExpensive();

        _encounter.DidNotReceive().Finish(Arg.Any<bool>());
    }
}
