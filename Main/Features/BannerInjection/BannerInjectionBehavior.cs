using TaleWorlds.CampaignSystem;

namespace TAOM.Features.BannerInjection;

public class BannerInjectionBehavior : CampaignBehaviorBase
{
    private readonly IBannerInjectionService _service;

    public BannerInjectionBehavior(IBannerInjectionService service)
    {
        _service = service;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(
            this, _ => _service.InjectBanners());
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(
            this, _ => _service.InjectBanners());
    }

    public override void SyncData(IDataStore dataStore)
    {
        _service.SyncData(dataStore);
    }
}
