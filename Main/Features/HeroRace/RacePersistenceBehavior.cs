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
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => _service.RestoreHeroRaces());
    }

    public override void SyncData(IDataStore dataStore)
    {
        _service.SyncRaceData(dataStore);
    }
}
