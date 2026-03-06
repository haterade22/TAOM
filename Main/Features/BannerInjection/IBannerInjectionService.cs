using TaleWorlds.CampaignSystem;

namespace TAOM.Features.BannerInjection;

public interface IBannerInjectionService
{
    void InjectBanners();
    void SyncData(IDataStore dataStore);
}
