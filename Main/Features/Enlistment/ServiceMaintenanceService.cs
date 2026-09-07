using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

/// <summary>
/// The continuous half of the service loop. See <see cref="IServiceMaintenanceService"/> for the
/// ownership contract against the hourly reconciler and for where this pump structurally cannot
/// reach.
///
/// Two sources share ONE budget (campaign tick and the wait-menu tick), so adding the second
/// source cannot double the work rate. Each pass has two tiers:
///   CHEAP  — every pass: position sync, using a cached party handle (zero lookups on the steady path).
///   EXPENSIVE — throttled: one <c>Find&lt;Hero&gt;</c> via <c>GetTickSnapshot</c>, then the
///               battle-latch, join-request and menu invariants.
/// </summary>
public class ServiceMaintenanceService : IServiceMaintenanceService
{
    /// <summary>Real seconds between expensive passes. The donor ran its whole loop at this rate.</summary>
    private const float MaintenanceIntervalSeconds = 0.25f;

    /// <summary>
    /// How often the wait-menu status board is rebuilt. Much slower than the maintenance tier
    /// because it uses the EXPENSIVE commander read (GetSnapshot renders Name and walks Culture /
    /// Clan.MapFaction / CurrentSettlement) — forbidden at 4 Hz, harmless at 0.5 Hz, and the
    /// underlying facts (in a town, in a battle, promoted) change on a scale of campaign minutes.
    /// The push itself is additionally gated on the model actually differing.
    /// </summary>
    private const float StatusIntervalSeconds = 2f;

    /// <summary>Campaign hours between attach/join attempts. Shared with the hourly reconciler.</summary>
    private const double AttachRetryIntervalHours = 1.0;

    /// <summary>Consecutive failed menu assertions before backing off, so a wedged menu cannot spin.</summary>
    private const int MaxMenuFailures = 3;

    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentStateMachine _machine;
    private readonly IServiceAttachmentService _attachment;
    private readonly ICommanderLordAdapter _commander;
    private readonly IGameMenuAdapter _gameMenu;
    private readonly IEnlistmentMenuService _menuService;
    private readonly IServiceStatusService _status;
    private readonly IArmyMembershipAdapter _army;
    private readonly IEncounterAdapter _encounter;
    private readonly IEncounterOwnershipPolicy _ownership;
    private readonly IEnlistmentReconciler _reconciler;
    private readonly IModLogger _logger;

    private float _budget;
    private float _statusBudget;
    private int _menuFailures;
    private string _cachedCommanderPartyId;
    private int _lastJoinRequestedForMapEventToken;

    public ServiceMaintenanceService(
        IEnlistmentStore store,
        IEnlistmentStateMachine machine,
        IServiceAttachmentService attachment,
        ICommanderLordAdapter commander,
        IGameMenuAdapter gameMenu,
        IEnlistmentMenuService menuService,
        IServiceStatusService status,
        IArmyMembershipAdapter army,
        IEncounterAdapter encounter,
        IEncounterOwnershipPolicy ownership,
        IEnlistmentReconciler reconciler,
        IModLogger logger)
    {
        _store = store;
        _machine = machine;
        _attachment = attachment;
        _commander = commander;
        _gameMenu = gameMenu;
        _menuService = menuService;
        _status = status;
        _army = army;
        _encounter = encounter;
        _ownership = ownership;
        _reconciler = reconciler;
        _logger = logger;
    }

    public event Action<string> BattleJoinRequested;

    public void Pump(float dt, double nowHours)
    {
        var record = _store.Record;
        if (!record.IsEnlisted)
            return;

        // BRACES ARE LOAD-BEARING. Both accumulators must sit INSIDE the guard: NaN propagates
        // through every subsequent addition and every comparison against it is false, so one
        // unguarded `+= dt` permanently wedges that budget below its threshold and the tier it
        // gates never runs again. This is the NaN-gate bug class the codebase has shipped five
        // times; it very nearly became a sixth here.
        if (dt > 0f && !float.IsInfinity(dt))
        {
            _budget += dt;
            _statusBudget += dt;
        }

        // CHEAP TIER — every pass. Only meaningful while parked and attached; any other state
        // either owns its own placement (duty, captivity) or is mid-transition.
        if (record.State == EnlistmentState.EnlistedAttached)
            _attachment.SyncPositionCached(record.CommanderHeroId, _cachedCommanderPartyId);

        if (_budget < MaintenanceIntervalSeconds)
            return;
        _budget = 0f;

        // EXPENSIVE TIER — the one Find<Hero> this pump is allowed.
        var commander = _commander.GetTickSnapshot(record.CommanderHeroId);
        _cachedCommanderPartyId = commander.PartyId;

        var presence = _attachment.GetPresenceFlags();

        // Shore leave dies with the settlement stop (field report 1). Done here, on the presence
        // this pump already read, rather than through a second service doing its own lookup — one
        // owner for the revoke means it cannot be half-applied.
        if (TownLeavePolicy.ShouldRevokeLeave(record.State, presence.IsInSettlement, record.OnTownLeave))
        {
            record.OnTownLeave = false;

            // CLOSE WHAT THE PASS OPENED. TakeTownLeave establishes a settlement PlayerEncounter so
            // the vanilla town menu is safe to show, and nothing else ever will take it down:
            // EncounterOwnershipPolicy R3 returns SkipNotOurs for an encounter with no encountered
            // MOBILE party, which is exactly a settlement encounter's shape, so the reconciler's
            // stranded sweep deliberately walks past it. Leaked, it blocks every main-party
            // encounter for the rest of the term.
            //
            // THROUGH THE POLICY, never a bare Finish. Leave is revoked for every reason including
            // "a battle started", and at that moment the live encounter may be the one
            // ServiceBattleService seeded — destroying it freezes the map event, because
            // MapEventManager.Tick skips the player's own and only PlayerEncounter.Update advances
            // it. ShoreLeaveEnd finishes the settlement shape and defers on everything else.
            //
            // forcePlayerOutFromSettlement MIRRORS WHERE THE PLAYER ACTUALLY IS, and a bare false
            // here is a soft-lock. Leave is revoked for every reason that is not
            // attached-and-in-a-settlement, so it ALSO fires on a state change while the player is
            // still standing in the town. Vanilla Finish always runs `TimeControlMode = Stop` and
            // `GameMenu.ExitToLast()`, and walks the player out only under
            // `if (InsideSettlement && AttachedTo == null && forcePlayerOutFromSettlement)`.
            // Passing false in that case takes their menu, stops time, and leaves them inside a
            // settlement — and MobileParty.DoUpdatePosition refuses to move a party with
            // CurrentSettlement set. Out on the map with time stopped is merely paused.
            //
            // THE FLAG IS NECESSARY, NOT SUFFICIENT, and it must be asked of the ENGINE's predicate
            // rather than ours. Vanilla's InsideSettlement is `MainParty.IsActive && CurrentSettlement
            // != null`; PlayerPresenceFlags.IsInSettlement is only the settlement id, with no IsActive
            // term. Enlistment routinely parks the party inactive WITHOUT leaving the settlement
            // (ParkNear), so `presence.IsInSettlement && !PlayerEncounter.InsideSettlement` is a
            // reachable shape in which passing true silently no-ops the walk-out this comment argues
            // for. IEncounterAdapter.IsInsideSettlement is that engine predicate.
            var verdict = _ownership.Evaluate(
                EncounterFinishIntent.ShoreLeaveEnd, _encounter.GetOwnership(_cachedCommanderPartyId));
            if (verdict == EncounterFinishVerdict.Finish)
            {
                if (!_encounter.Finish(_encounter.IsInsideSettlement))
                    _logger?.LogError("[EnlistDiag] shore leave ended but its PlayerEncounter would not close — the player may be unable to start encounters");
            }
            else if (verdict != EncounterFinishVerdict.NothingToFinish)
            {
                _logger?.LogInfo($"[EnlistDiag] shore leave ended; left the live encounter alone: {verdict}");
            }

            _logger?.LogInfo("[Enlistment] shore leave ended — the column has moved on");
        }

        TryBreakBattleLatch(record, commander, presence);
        TryRequestBattleJoin(record, commander, presence, nowHours);
        EnsureServiceMenu(record, presence);
        RefreshStatusBoard(record);
    }

    /// <summary>
    /// Keep the wait menu's text honest. Without this the board is written once at menu init and
    /// then never again — which is precisely why the old one-line text appeared frozen for an
    /// entire term of service.
    /// </summary>
    private void RefreshStatusBoard(EnlistmentRecord record)
    {
        // Attached OR waiting on a lost commander. Widened by exactly one state on 2026-08-09,
        // deliberately not to IsEnlisted: ServiceStatusService.ResolveActivity reads the
        // COMMANDER's activity, so a captive player would be told "You march with X's company"
        // from a dungeon.
        //
        // The one state added is the one that had a finished line waiting for it.
        // ServiceStatusTextWriter already returns "{=taom_enlist_act_lost}You have lost the
        // column. Await word of your commander." for CommanderUnavailable — authored, registered,
        // and translated into all twelve languages — and this early return was the only reason it
        // could never reach the board. A player whose commander's party was destroyed got no
        // message anywhere; the message existed the whole time.
        if (record.State != EnlistmentState.EnlistedAttached
            && record.State != EnlistmentState.CommanderUnavailable)
            return;
        if (_statusBudget < StatusIntervalSeconds)
            return;

        _statusBudget = 0f;
        _status?.RefreshIfChanged();
    }

    /// <summary>
    /// Drop per-session caches. MUST be called on game load and session launch: the cached party
    /// id is matched by StringId, and lord-party ids are identical across a reload of the same
    /// campaign — so a stale handle from a destroyed campaign HITS the cache test and the cheap
    /// position sync then drives the player from a dead party's position at frame rate.
    ///
    /// This method is the ONE place that knows the lifetime of the feature's per-session state, so
    /// collaborators' caches are dropped from here too rather than each being wired separately into
    /// the load hook — the same reason <c>_attachment.InvalidateCommanderCache()</c> is called here
    /// and not from <c>EnlistmentBehavior</c>.
    /// </summary>
    public void ResetSessionCaches()
    {
        _cachedCommanderPartyId = null;
        _lastJoinRequestedForMapEventToken = 0;
        _menuFailures = 0;
        _budget = 0f;
        _statusBudget = 0f;
        _attachment.InvalidateCommanderCache();
        _status?.Invalidate();

        // The army adapter holds a live Army REFERENCE on a singleton that outlives the campaign.
        // After a reload it names either a dead campaign's object or an instance this world no
        // longer contains, and the identity test in LeaveArmy can then never match again for the
        // rest of the process — so the army raised for a battle would never be disbanded, and a
        // bare-ctor army carries a null AiBehaviorObject that crashes the map UI. See
        // IArmyMembershipAdapter.ResetSessionCaches.
        _army?.ResetSessionCaches();

        // The reconciler is a singleton too, and its stale-battle-latch anchor is an absolute
        // campaign day (#551). A campaign that ended WHILE latched leaves that anchor behind; load a
        // later save and the elapsed time is enormous, so the recovery fires on the first latched
        // tick and finishes a live loot screen with no real waiting. Dropped here for the same
        // reason the army handle is, rather than being wired separately into the load hook.
        _reconciler?.ResetForNewSession();
    }

    public void OnPartyJoinedRunningMapEvent(string partyId)
    {
        // Fires for EVERY party joining EVERY battle in the world. Stay near-free on the
        // non-match path: one bool, one ordinal compare against the CACHED id. No logging here.
        if (!_store.Record.IsEnlisted)
            return;
        if (partyId == null || !string.Equals(partyId, _cachedCommanderPartyId, StringComparison.Ordinal))
            return;

        // The commander just entered a battle that was ALREADY RUNNING. MapEventStarted cannot see
        // this — it is dispatched exactly once, as the last statement of MapEvent.Initialize — so
        // without this edge the join waits for the next expensive pass at best.
        _store.Record.PendingCommanderAttachment = true;
        _store.Record.NextAttachRetryAtHours = null;   // retry immediately, this is a real edge
        _logger?.LogInfo($"[EnlistDiag] commander '{partyId}' joined a running map event — arming an immediate join");
    }

    /// <summary>
    /// The ONLY state transition this pump makes. Kept byte-identical to the reconciler's
    /// staleness predicate — <c>Reconciler_AndPump_UseIdenticalStaleBattlePredicate</c> pins that.
    /// </summary>
    private void TryBreakBattleLatch(EnlistmentRecord record, CommanderTickSnapshot commander, PlayerPresenceFlags presence)
    {
        if (record.State != EnlistmentState.EnlistedBattle)
            return;
        if (presence.IsInMapEvent || commander.PartyIsInMapEvent || presence.HasPlayerEncounter)
            return;

        // Nobody is in a battle and no encounter is open, yet we are latched in EnlistedBattle.
        // That latch would block every future join at ServiceBattleService's entry guard.
        if (_machine.TryTransition(EnlistmentState.EnlistedAttached))
            _logger?.LogInfo("[EnlistDiag] maintenance broke a stale EnlistedBattle latch");
    }

    private void TryRequestBattleJoin(EnlistmentRecord record, CommanderTickSnapshot commander, PlayerPresenceFlags presence, double nowHours)
    {
        if (record.State != EnlistmentState.EnlistedAttached)
            return;
        if (!commander.PartyIsInMapEvent || presence.IsInMapEvent)
            return;
        if (!commander.IsFollowable)
            return;

        // A save taken mid-battle resumes the retry here. _lastJoinRequestedForMapEventToken is
        // a private field that resets to 0 on load, so without this the persisted flag was
        // write-only and its documented purpose — surviving a reload — was not implemented.
        // Clearing the token forces one immediate attempt for the battle still in progress.
        if (record.PendingCommanderAttachment && _lastJoinRequestedForMapEventToken == 0)
            record.NextAttachRetryAtHours = null;

        // Dedupe on the map-event token so a battle we already tried and failed is not retried
        // every pass — only once per battle, then on the shared hourly budget.
        var sameBattleAlreadyTried = commander.MapEventToken != 0
            && commander.MapEventToken == _lastJoinRequestedForMapEventToken;

        if (sameBattleAlreadyTried && !BudgetExpired(record, nowHours))
            return;

        _lastJoinRequestedForMapEventToken = commander.MapEventToken;
        record.PendingCommanderAttachment = true;
        record.NextAttachRetryAtHours = nowHours + AttachRetryIntervalHours;

        _logger?.LogInfo($"[EnlistDiag] maintenance BattleJoinRequested for '{record.CommanderHeroId}' (battle token {commander.MapEventToken})");
        try
        {
            BattleJoinRequested?.Invoke(record.CommanderHeroId);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[EnlistDiag] maintenance BattleJoinRequested subscriber threw: {ex.Message}");
        }
    }

    private static bool BudgetExpired(EnlistmentRecord record, double nowHours) =>
        !record.NextAttachRetryAtHours.HasValue || nowHours >= record.NextAttachRetryAtHours.Value;

    private void EnsureServiceMenu(EnlistmentRecord record, PlayerPresenceFlags presence)
    {
        // SHORE LEAVE OUTRANKS THE INVARIANT. IsRedirectable is id-only by design ("am I entitled
        // to replace what is on screen?"), so it answers TRUE for "town" — meaning that without this
        // the pump would close the settlement menu out from under a player who is deliberately away
        // from camp, seconds after he opened it. The revoke above is what bounds this: the moment
        // the column moves, the flag clears and the very next pass restores the menu.
        if (record.OnTownLeave)
            return;

        // The EnlistedAttached gate is LOAD-BEARING, not defensive: "encounter" and
        // "join_encounter" are in the default redirect list, so asserting the wait menu while in
        // EnlistedBattle would eat the battle, loot and aftermath menus.
        if (record.State != EnlistmentState.EnlistedAttached)
            return;

        // Two shapes are legitimate service now: parked outside the column, or INSIDE the
        // commander's settlement — where the party is deliberately active and visible, so
        // LooksParked is false. Gating on parked alone would abandon the menu for the whole
        // settlement stop, which is the one place a stray vanilla town menu can appear.
        if (!presence.LooksParked && !presence.IsInSettlement)
            return;
        // STATE TRANSITION BEFORE THE GATE. This check used to sit ABOVE the reset below, so
        // once the counter hit the cap it returned on every subsequent pass and neither reset
        // was ever reachable — three transient failures disabled the wait-menu invariant for
        // the rest of the PROCESS, on a singleton, across re-enlistment and across campaigns.
        // That is the latch failure mode the harmony-patches rule exists to prevent.
        var current = _gameMenu.CurrentMenuId;
        if (current == EnlistmentMenuService.ServiceWaitMenuId)
        {
            _menuFailures = 0;
            return;
        }

        if (_menuFailures >= MaxMenuFailures)
            return;

        // Only take over a menu we are entitled to take over. A settlement or encounter menu the
        // player legitimately owns is not ours to close.
        if (!string.IsNullOrEmpty(current) && !_menuService.IsRedirectable(current))
            return;

        if (_gameMenu.EnsureMenuOpen(EnlistmentMenuService.ServiceWaitMenuId))
        {
            _menuFailures = 0;
            _logger?.LogInfo($"[EnlistDiag] maintenance restored the service wait menu (was '{current ?? "none"}')");
        }
        else if (++_menuFailures >= MaxMenuFailures)
        {
            _logger?.LogError($"[EnlistDiag] maintenance could not open the service wait menu {MaxMenuFailures}x — backing off to avoid spinning");
        }
    }
}
