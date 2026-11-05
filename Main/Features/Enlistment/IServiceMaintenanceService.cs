using System;

namespace TAOM.Features.Enlistment;

/// <summary>
/// The continuous half of the service loop.
///
/// OWNERSHIP CONTRACT — do not blur this, it is the whole design:
/// the hourly <see cref="IEnlistmentReconciler"/> remains the ONLY terminal authority (discharge,
/// grace open/expire, captivity transitions, <c>CommanderUnavailable</c>, the stranded-encounter
/// sweep). This pump asserts CONTINUOUS invariants — follow the commander, be in his battle, be on
/// the service menu — and makes exactly ONE state transition of its own: the
/// <c>EnlistedBattle → EnlistedAttached</c> demote that breaks a stale battle latch. It never
/// injects <see cref="IDischargeService"/> and never calls <c>ReconcileHourly</c>.
///
/// WHERE THIS PUMP CANNOT REACH — do not re-spec a poll for it:
/// <c>GameMenu.ActivateGameMenu</c> and <c>SwitchToMenu</c> set
/// <c>Campaign.TimeControlMode = Stop</c>, and <c>Campaign.Tick()</c> gates its dispatcher on
/// <c>_dt &gt; 0f</c>. So <c>CampaignEvents.TickEvent</c> does NOT fire while the player sits in
/// <c>encounter</c> / <c>join_encounter</c> / <c>town</c> / <c>castle</c>, or during a map
/// conversation. Neither pump source runs there. Only the existing <c>SetNextMenu</c> /
/// <c>EnterMenuMode</c> patch edges can act in those windows.
/// </summary>
public interface IServiceMaintenanceService
{
    /// <summary>
    /// Raised when the commander is in a battle the player is not. Carries the commander HERO id;
    /// the boundary behaviour converts it to a party id. Deliberately the same event shape the
    /// reconciler raises, so there is exactly ONE join implementation.
    /// </summary>
    event Action<string> BattleJoinRequested;

    /// <summary>
    /// A request to join the commander's battle, from the hourly reconciler (#954). A join into a RAID is deferred
    /// to the next wait-menu frame when it is not already inside one and the service wait menu is current: a join
    /// raised from a tick runs before <c>MapEventManager.Tick</c> in the same frame, which updates a raid even
    /// when it is the player's own event, so a simulation session could run with the soldier inside. Every other
    /// join, and any join off the service wait menu, is raised at once (the battle-start edge too, by design: see
    /// docs/features/enlistment.md, #954 open risk 1).
    /// </summary>
    void RequestBattleJoin(string commanderHeroId);

    /// <summary>
    /// The wait-menu frame: performs a deferred raid join (the safe point: the join's switch to the encounter menu
    /// sets Stop), then pumps with <paramref name="realElapsedSeconds"/> only when
    /// <paramref name="campaignDeltaSeconds"/> is exactly zero, meaning no campaign time passed since the previous
    /// wait-menu frame (while time runs, <c>TickEvent</c> already pumps the frame).
    /// </summary>
    void OnWaitMenuFrame(double campaignDeltaSeconds, float realElapsedSeconds, double nowHours);

    /// <summary>
    /// Advance the loop. <paramref name="dt"/> is the campaign tick's delta from the map source; from the
    /// wait-menu source it is real seconds since the previous wait-menu frame (that clock restarts every frame). The sources share one budget, and the wait-menu tick only feeds it while campaign
    /// time is stopped.
    /// <paramref name="nowHours"/> is campaign hours, supplied by the boundary behaviour — the
    /// same clock the hourly reconciler uses (it passes days; the shared retry budget is stored in
    /// HOURS and the reconciler converts, pinned by a test so the two can never disagree).
    /// </summary>
    void Pump(float dt, double nowHours);

    /// <summary>
    /// A party was added to some map event. Called for EVERY party joining EVERY battle in the
    /// world, so it must stay near-free on the non-match path — this is the zero-poll answer to a
    /// commander joining an ALREADY-RUNNING fight, which <c>MapEventStarted</c> cannot see because
    /// it is dispatched exactly once, as the last statement of <c>MapEvent.Initialize</c>.
    /// </summary>
    void OnPartyJoinedRunningMapEvent(string partyId);

    /// <summary>
    /// Drop the caches listed in <c>ResetSessionCaches</c>. Call on game load, on a new campaign and
    /// at game end: a commander-party handle cached from a previous campaign matches by StringId and
    /// would drive the position sync from a destroyed party. Runs on every peer, so keep it to
    /// in-memory field clears.
    /// </summary>
    void ResetSessionCaches();
}
