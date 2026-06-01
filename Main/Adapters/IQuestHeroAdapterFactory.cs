using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

/// <summary>
/// Creates an <see cref="IQuestHeroAdapter"/> for a hero. Lives at the engine boundary
/// (references <c>Hero</c>); the career-quest shell uses it to hand the service an adapter.
/// </summary>
public interface IQuestHeroAdapterFactory
{
    IQuestHeroAdapter Create(Hero hero);
}
