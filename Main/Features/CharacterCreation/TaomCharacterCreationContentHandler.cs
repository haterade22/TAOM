using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TAOM.Core.Logging;

namespace TAOM.Features.CharacterCreation;

public class TaomCharacterCreationContentHandler : ICharacterCreationContentHandler
{
    private readonly ICharacterCreationContentService _contentService;
    private readonly IModLogger _logger;

    public TaomCharacterCreationContentHandler(
        ICharacterCreationContentService contentService,
        IModLogger logger)
    {
        _contentService = contentService;
        _logger = logger;
    }

    public void InitializeContent(CharacterCreationManager characterCreationManager)
    {
        // SandBox handler (priority 800) runs first and registers vanilla cultures + stages.
        // We do nothing here — all TAOM work happens in AfterInitializeContent.
    }

    public void AfterInitializeContent(CharacterCreationManager characterCreationManager)
    {
        _contentService.RegisterCustomCultures(characterCreationManager);
        _contentService.RegisterNarrativeMenus(characterCreationManager);
        _contentService.RegisterCareerMenu(characterCreationManager);
        _logger.LogInfo("TAOM character creation content initialized");
    }

    public void OnStageCompleted(CharacterCreationStageBase stage)
    {
        // Future: track stage completions if needed
    }

    public void OnCharacterCreationFinalize(CharacterCreationManager characterCreationManager)
    {
        _contentService.OnCharacterCreationFinalize(characterCreationManager);
    }
}
