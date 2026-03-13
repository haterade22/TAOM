using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TAOM.Core.Logging;

namespace TAOM.Features.CharacterCreation;

public class CharacterCreationRegistrationBehavior : CampaignBehaviorBase
{
    private const int HandlerPriority = 1050;

    private readonly ICharacterCreationContentService _contentService;
    private readonly IModLogger _logger;

    public CharacterCreationRegistrationBehavior(
        ICharacterCreationContentService contentService,
        IModLogger logger)
    {
        _contentService = contentService;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnCharacterCreationInitializedEvent.AddNonSerializedListener(
            this,
            OnCharacterCreationInitialized);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // No persistent data to sync
    }

    private void OnCharacterCreationInitialized(CharacterCreationManager manager)
    {
        var handler = new TaomCharacterCreationContentHandler(_contentService, _logger);
        manager.RegisterCharacterCreationContentHandler(handler, HandlerPriority);
        _logger.LogInfo($"Registered TAOM character creation handler at priority {HandlerPriority}");
    }
}
