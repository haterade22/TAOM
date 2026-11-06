using TAOM.Features.CoopInterop;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.BannerInjection;

public class BannerInjectionBehavior : CampaignBehaviorBase
{
    private readonly IBannerInjectionService _service;
    private readonly IBannerExclusionService _exclusions;
    private readonly ICoopSessionProvider _coop;

    public BannerInjectionBehavior(IBannerInjectionService service, IBannerExclusionService exclusions, ICoopSessionProvider coop)
    {
        _service = service;
        _exclusions = exclusions;
        _coop = coop;
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
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
    }

    private void OnGameLoaded(CampaignGameStarter starter)
    {
        // A co-op join is a save load: clan banners are shared state the server sets (#1086).
        if (!_coop.IsAuthority)
            return;
        _service.InjectBanners();
    }

    public override void SyncData(IDataStore dataStore)
    {
        _service.SyncData(dataStore);
    }
}
