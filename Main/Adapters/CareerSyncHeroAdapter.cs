using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

/// <inheritdoc />
public sealed class CareerSyncHeroAdapter : ICareerSyncHeroAdapter
{
    private readonly ICareerHeroAdapterFactory _factory;

    public CareerSyncHeroAdapter(ICareerHeroAdapterFactory factory) => _factory = factory;

    public ICareerHeroAdapter? Find(string heroId)
    {
        // Hero.Find dereferences Campaign.Current unguarded: go through the object manager only when there is one.
        var manager = Campaign.Current?.CampaignObjectManager;
        var hero = string.IsNullOrEmpty(heroId) || manager == null ? null : manager.Find<Hero>(heroId);
        return hero == null ? null : _factory.Create(hero);
    }
}
