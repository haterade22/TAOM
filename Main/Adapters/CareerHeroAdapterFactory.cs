using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

public class CareerHeroAdapterFactory : ICareerHeroAdapterFactory
{
    public ICareerHeroAdapter Create(Hero hero)
    {
        return hero != null ? new CareerHeroAdapter(hero) : null;
    }
}
