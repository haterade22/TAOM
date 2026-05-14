using TAOM.Core.Logging;
using TAOM.Features.Diplomacy.Models;
using TaleWorlds.CampaignSystem;

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

    public WarOfTheRingBehavior(IWarOfTheRingService wotrService, IModLogger logger)
    {
        _wotrService = wotrService;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        _logger.LogInfo("[WarOfTheRing] WarOfTheRingBehavior registering events");
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => _persistedPhase = (int)WarPhase.Peace);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // Phase 9b #129 P1 — persist phase across save-load. Stored as int (enum-backed) since
        // dataStore primitives are safer than enum direct.
        if (dataStore.IsSaving)
            _persistedPhase = (int)_wotrService.CurrentPhase;

        dataStore.SyncData("WarOfTheRing_CurrentPhase", ref _persistedPhase);

        if (dataStore.IsLoading)
            _wotrService.SetPhaseFromSave((WarPhase)_persistedPhase);
    }

    private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
    {
        // Eagerly recompute phase on load so diplomacy guards are active
        // before the first daily tick (prevents save/load peace exploit)
        var elapsedDays = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;
        _wotrService.CheckPhaseTransition(elapsedDays);
        _logger.LogInfo($"[WarOfTheRing] Session launched — phase restored at day {elapsedDays:F0}");
    }

    private void OnDailyTick()
    {
        var elapsedDays = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;
        _wotrService.CheckPhaseTransition(elapsedDays);
    }
}
