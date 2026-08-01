using TAOM.Core.Logging;
using TAOM.Features.Diplomacy.Models;
using TaleWorlds.CampaignSystem;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.Diplomacy;

public class WarOfTheRingBehavior : CampaignBehaviorBase
{
    private readonly IWarOfTheRingService _wotrService;
    private readonly IModLogger _logger;

    // Phase 9b #129 P1 — persisted phase. Pre-fix this state was re-derived from elapsed days on
    // every load, which means past-Phase2 saves replayed BOTH Peace→IsengardWar and IsengardWar→FullWar
    // transitions on every load. Currently idempotent (AreAtWar guards), but ANY non-idempotent side
    // effect added later (notifications, influence, story flags) would replay.
    private int _persistedPhase = (int)WarPhase.Peace;
    // WotR Momentum #327 — persisted outcome, same int-backed convention as phase.
    private int _persistedOutcome = (int)WarOutcome.None;

    private readonly ICoopSessionProvider _coopSession;

    public WarOfTheRingBehavior(IWarOfTheRingService wotrService, IModLogger logger, ICoopSessionProvider coopSession)
    {
        _wotrService = wotrService;
        _logger = logger;
        _coopSession = coopSession;
    }

    public override void RegisterEvents()
    {
        _logger.LogInfo("[WarOfTheRing] WarOfTheRingBehavior registering events");
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ =>
        {
            _persistedPhase = (int)WarPhase.Peace;
            _persistedOutcome = (int)WarOutcome.None;
        });
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

        // Eagerly recompute phase on load so diplomacy guards are active
        // before the first daily tick (prevents save/load peace exploit)
        var elapsedDays = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;
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

        var elapsedDays = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;
        _wotrService.CheckPhaseTransition(elapsedDays);
    }
}
