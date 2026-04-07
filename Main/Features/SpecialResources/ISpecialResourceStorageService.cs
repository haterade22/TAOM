using TaleWorlds.CampaignSystem;

namespace TAOM.Features.SpecialResources;

public interface ISpecialResourceStorageService
{
    float Get(string heroId);
    void Set(string heroId, float amount);
    void Add(string heroId, float delta);
    void SyncData(IDataStore dataStore);
}
