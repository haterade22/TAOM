using TaleWorlds.CampaignSystem;

namespace TAOM.Features.BannerInjection;

public class BannerInjectionBehavior : CampaignBehaviorBase
{
    private readonly IBannerInjectionService _service;
    private readonly IBannerExclusionService _exclusions;

    public BannerInjectionBehavior(IBannerInjectionService service, IBannerExclusionService exclusions)
    {
        _service = service;
        _exclusions = exclusions;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(
            this, _ =>
            {
                // Phase 9b #124 R1 — fresh campaign in same process: clear stale exclusions
                // before injecting banners. Without this, TAOM canon banners would be skipped
                // for entities the player modified in a prior campaign.
                _exclusions.Reset();
                _service.InjectBanners();
            });
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(
            this, _ => _service.InjectBanners());
    }

    public override void SyncData(IDataStore dataStore)
    {
        _service.SyncData(dataStore);
    }
}
