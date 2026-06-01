using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

public class QuestHeroAdapterFactory : IQuestHeroAdapterFactory
{
    public IQuestHeroAdapter Create(Hero hero)
    {
        return hero != null ? new QuestHeroAdapter(hero) : null;
    }
}
