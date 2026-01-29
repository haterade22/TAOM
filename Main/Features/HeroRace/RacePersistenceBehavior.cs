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
        CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, _service.CaptureHeroRaces);
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => _service.RestoreHeroRaces());
    }

    public override void SyncData(IDataStore dataStore)
    {
        _service.SyncRaceData(dataStore);
    }
}
