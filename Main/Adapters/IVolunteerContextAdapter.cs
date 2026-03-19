using TaleWorlds.CampaignSystem;
using TAOM.Features.TroopProgression;

namespace TAOM.Adapters;

public interface IVolunteerContextAdapter
{
    VolunteerContext GetContext(Hero hero);
    CharacterObject ResolveCharacter(string characterId);
}
