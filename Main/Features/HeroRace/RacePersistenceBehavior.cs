using TaleWorlds.CampaignSystem;

namespace TAOM.Features.HeroRace;

public class RacePersistenceBehavior : CampaignBehaviorBase
{
    private readonly IRacePersistenceService _service;

    /// <summary>
    /// SubModule.OnGameStart builds this behavior with <c>new</c> for every campaign, before any SyncData, so the reset
    /// here also covers a load the engine gives no SyncData call (#1035).
    /// </summary>
    public RacePersistenceBehavior(IRacePersistenceService service)
    {
        _service = service;
        _service.ResetForNewSession();
    }

    public override void RegisterEvents()
    {
        // Phase 9b #130 R1 — reset singleton state on new campaign. Must fire BEFORE SyncData(load)
        // for fresh new games to avoid carrying over a prior campaign's race map.
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => _service.ResetForNewCampaign());
        CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, _service.CaptureHeroRacesForSave);
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => OnSessionLaunched());
    }

    /// <summary>
    /// Restore the persisted races, then re-capture them.
    ///
    /// The capture half was added when a co-op save transfer was believed to skip <c>OnBeforeSaveEvent</c> — a co-op
    /// host handing its world to a joining client (multiplayer field report 2026-08-03 §1). The co-op
    /// module's transfer save does raise it (SaveInterface.cs:109), so this capture is now a backup
    /// for a transfer path that skips the event.
    ///
    /// ORDER IS LOAD-BEARING and is pinned by a test: capturing BEFORE the restore would snapshot the
    /// pre-restore state — every hero at whatever race the raw XML gave them — and write that over the
    /// good map we just loaded, which is the very data the restore is about to apply.
    /// </summary>
    internal void OnSessionLaunched()
    {
        _service.RestoreHeroRaces();
        _service.CaptureHeroRaces();
    }

    public override void SyncData(IDataStore dataStore)
    {
        _service.SyncRaceData(dataStore);
    }
}
