using System;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy.Models;
using TaleWorlds.CampaignSystem;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.Diplomacy;

public class WarOfTheRingBehavior : CampaignBehaviorBase
{
    private readonly IWarOfTheRingService _wotrService;
    private readonly IModLogger _logger;

    // Phase 9b #129 P1 — persisted phase. Before #129 the phase was re-derived from elapsed days on
    // every load, so past-Phase2 saves replayed both transitions on every load; TransitionToPhase now
    // runs once per campaign. #772 deliberately re-runs the Full War declarations on every authority
    // session launch (ReconcileDeclaredWars in OnSessionLaunched). That is idempotent only because
    // each declaration is guarded by IAllianceAdapter.HasDeclaredWar (the raw stance link; the
    // transitions stay idempotent because DeclareWarAction sets the link to War). Never guard with
    // AreAtWar: during Full War, TaomDiplomacyModel.IsAtConstantWar makes it true for every Hostile
    // pair, which would skip every declaration. A side effect added to DeclareFullWarWars outside
    // that guarded branch would replay on every load, so put it inside the branch or in
    // TransitionToPhase.
    private int _persistedPhase = (int)WarPhase.Peace;
    // WotR Momentum #327 — persisted outcome, same int-backed convention as phase.
    private int _persistedOutcome = (int)WarOutcome.None;

    private readonly ICoopSessionProvider _coopSession;
    private readonly Func<float> _elapsedDays;

    // elapsedDays: tests pass a fixed clock so the host paths can be asserted; the game reads the
    // campaign's elapsed days.
    public WarOfTheRingBehavior(IWarOfTheRingService wotrService, IModLogger logger, ICoopSessionProvider coopSession,
        Func<float>? elapsedDays = null)
    {
        _wotrService = wotrService;
        _logger = logger;
        _coopSession = coopSession;
        _elapsedDays = elapsedDays ?? (() => Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow);

        // #764: the service is a process-lifetime singleton, and this behavior is built fresh in every
        // campaign's OnGameStart, before any campaign event and before a load's SyncData. Resetting
        // here means no reader sees the previous campaign's phase; a load then restores the saved one
        // in SyncData. OnSessionLaunched was too late: campaign-event listeners run newest-first, so
        // the momentum behavior (added after this one) read the stale phase first, and vanilla caches
        // each kingdom's at-war list during OnNewGameCreated.
        _wotrService.ResetForNewSession();
    }

    public override void RegisterEvents()
    {
        _logger.LogInfo("[WarOfTheRing] WarOfTheRingBehavior registering events");
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // Phase 9b #129 P1 — persist phase across save-load. Stored as int (enum-backed) since
        // dataStore primitives are safer than enum direct.
        if (dataStore.IsSaving)
        {
            _persistedPhase = (int)_wotrService.CurrentPhase;
            _persistedOutcome = (int)_wotrService.Outcome;
        }

        dataStore.SyncData("WarOfTheRing_CurrentPhase", ref _persistedPhase);
        dataStore.SyncData("WarOfTheRing_Outcome", ref _persistedOutcome);

        if (dataStore.IsLoading)
        {
            _wotrService.SetPhaseFromSave((WarPhase)_persistedPhase);
            _wotrService.SetOutcomeFromSave((WarOutcome)_persistedOutcome);
        }
    }

    // internal for TAOM.Tests (InternalsVisibleTo) — lets the co-op authority gate be asserted directly.
    internal void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
    {
        // CO-OP: host-only, for the SAME reason as OnDailyTick — this calls the identical
        // CheckPhaseTransition, which declares wars between arbitrary AI kingdoms. Gating the tick
        // alone was not enough: OnSessionLaunchedEvent fires on EVERY peer, and a co-op join IS a
        // save-load, so a joining client would recompute the phase and issue its own DeclareWar set
        // the instant it connected. The client does not need the recompute anyway — SyncData restores
        // the phase from the host's save before this runs. (deep-review 2026-08-01, data-flow HIGH #1)
        if (!_coopSession.IsAuthority) return;

        // #772: a save already at Full War may hold Hostile pairs that were never really declared.
        // Runs before the phase check, so it sees only the phase the save restored; a transition
        // fired below declares its own wars.
        _wotrService.ReconcileDeclaredWars();

        // Eagerly recompute phase on load so diplomacy guards are active
        // before the first daily tick (prevents save/load peace exploit)
        var elapsedDays = _elapsedDays();
        _wotrService.CheckPhaseTransition(elapsedDays);
        _logger.LogInfo($"[WarOfTheRing] Session launched — phase restored at day {elapsedDays:F0}");
    }

    // internal for TAOM.Tests (InternalsVisibleTo) — lets the co-op authority gate be asserted directly.
    internal void OnDailyTick()
    {
        // CO-OP: host-only. CheckPhaseTransition declares wars between arbitrary AI kingdoms. Phase
        // is persisted in TAOM SyncData that no co-op mod replicates, and the client's clock is
        // slewed rather than identical, so both peers would cross the threshold independently and
        // issue duplicate DeclareWar calls.
        if (!_coopSession.IsAuthority) return;

        _wotrService.CheckPhaseTransition(_elapsedDays());
    }
}
