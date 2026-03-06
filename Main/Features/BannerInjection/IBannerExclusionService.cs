using TaleWorlds.CampaignSystem;

namespace TAOM.Features.BannerInjection;

public interface IBannerExclusionService
{
    void MarkAsPlayerModified(string id);
    bool IsPlayerModified(string id);
    void SyncData(IDataStore dataStore);
}
