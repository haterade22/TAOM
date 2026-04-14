using TaleWorlds.CampaignSystem.CharacterCreationContent;

namespace TAOM.Features.CharacterCreation;

public interface ICareerMenuService
{
    void RegisterCareerMenu(CharacterCreationManager manager);
    string SelectedCareerStringId { get; }
}
