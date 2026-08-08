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
    /// Advance the loop. <paramref name="dt"/> is real seconds since this source last pumped;
    /// sources share one budget so adding a second cannot double the work rate.
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
    /// Drop every per-session cache. Call on game load and session launch — a commander-party
    /// handle cached from a previous campaign matches by StringId and would drive the position
    /// sync from a destroyed party.
    /// </summary>
    void ResetSessionCaches();
}
