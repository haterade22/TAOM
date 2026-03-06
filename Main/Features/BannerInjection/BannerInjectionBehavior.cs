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
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(
            this, _ => _service.InjectBanners());
    }

    public override void SyncData(IDataStore dataStore)
    {
        _service.SyncData(dataStore);
    }
}
