using TaleWorlds.CampaignSystem;

namespace TAOM.Features.HeroRace;

public class RacePersistenceBehavior : CampaignBehaviorBase
{
    private readonly IRacePersistenceService _service;

    public RacePersistenceBehavior(IRacePersistenceService service)
    {
        _service = service;
    }

    public override void RegisterEvents()
    {
        // Phase 9b #130 R1 — reset singleton state on new campaign. Must fire BEFORE SyncData(load)
        // for fresh new games to avoid carrying over a prior campaign's race map.
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => _service.ResetForNewCampaign());
        CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, _service.CaptureHeroRaces);
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => OnSessionLaunched());
    }

    /// <summary>
    /// Restore the persisted races, then re-capture them.
    ///
    /// The capture half exists for save transfers that never raise <c>OnBeforeSaveEvent</c> — a co-op
    /// host handing its world to a joining client does exactly that (multiplayer field report
    /// 2026-08-03 §1), so without capturing here the race map is empty at serialization time and the
    /// joiner receives a world with no race data at all.
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
