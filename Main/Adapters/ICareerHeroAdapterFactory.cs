using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

public interface ICareerHeroAdapterFactory
{
    ICareerHeroAdapter Create(Hero hero);
}
