using TaleWorlds.CampaignSystem.CharacterCreationContent;

namespace TAOM.Features.CharacterCreation;

public interface ICharacterCreationContentService
{
    void RegisterCustomCultures(CharacterCreationManager manager);
    void RegisterNarrativeMenus(CharacterCreationManager manager);
    void RegisterCareerMenu(CharacterCreationManager manager);
    void OnCharacterCreationFinalize(CharacterCreationManager manager);
}
