using TaleWorlds.CampaignSystem;

namespace TAOM.Features.BannerInjection;

public interface IBannerExclusionService
{
    void MarkAsPlayerModified(string id);
    bool IsPlayerModified(string id);
    void SyncData(IDataStore dataStore);
    // Phase 9b #124 R1 — singleton reset on new campaign
    void Reset();
}
