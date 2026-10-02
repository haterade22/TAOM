using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.FactionUI.Presets;

/// <summary>
/// Registers the faction screen's character-creation handler at priority 1060 (#704), after TAOM's own
/// 1050, so a copied pick is applied over TAOM's starting kit and race as Kysaro's postfix on TAOM's
/// finalize did, and before Player Switcher's 1100, whose handover makes the player a hero taken over on
/// the faction screen. Vanilla's core handler is 800, StoryMode 900, NavalDLC 1000; 1060 is free. The
/// engine's handler list is a sorted list that throws on a duplicate priority, so the registration is
/// wrapped: a clash costs the faction screen's picks, never character creation. Also clears the last
/// campaign's pick, and when the culture stage completes, lets a takeover skip to the career choice.
/// </summary>
public class FactionPresetRegistrationBehavior : CampaignBehaviorBase
{
    public const int HandlerPriority = 1060;

    private readonly FactionPickService _picks;
    private readonly IModLogger _logger;

    public FactionPresetRegistrationBehavior(FactionPickService picks, IModLogger logger)
    {
        _picks = picks;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnCharacterCreationInitializedEvent.AddNonSerializedListener(this, OnCharacterCreationInitialized);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // Nothing persists: a pick lives only during the character creation of a new campaign.
    }

    private void OnCharacterCreationInitialized(CharacterCreationManager manager)
    {
        _picks.ResetForNewCharacterCreation();
        try
        {
            manager.RegisterCharacterCreationContentHandler(
                new FactionPresetContentHandler(_picks, new CharacterCreationStagesAdapter(manager), _logger),
                HandlerPriority);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[FactionUI] could not register the hero-preset handler at priority {HandlerPriority}: {ex.Message}");
        }
    }

    private sealed class FactionPresetContentHandler : ICharacterCreationContentHandler
    {
        private readonly FactionPickService _picks;
        private readonly ICharacterCreationStagesAdapter _stages;
        private readonly IModLogger _logger;

        public FactionPresetContentHandler(FactionPickService picks, ICharacterCreationStagesAdapter stages, IModLogger logger)
        {
            _picks = picks;
            _stages = stages;
            _logger = logger;
        }

        public void InitializeContent(CharacterCreationManager characterCreationManager)
        {
        }

        public void AfterInitializeContent(CharacterCreationManager characterCreationManager)
        {
        }

        // NextStage has already moved its index past the culture stage and activates the stage there
        // only after every handler has run, so the list changed here decides what opens next.
        public void OnStageCompleted(CharacterCreationStageBase stage)
        {
            if (stage is not CharacterCreationCultureStage)
                return;
            try
            {
                _picks.OnCultureStageCompleted(_stages);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[FactionUI] the faction screen's pick could not be carried past the culture stage: {ex}");
            }
        }

        public void OnCharacterCreationFinalize(CharacterCreationManager characterCreationManager)
        {
            try
            {
                _picks.OnCharacterCreationFinalize();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[FactionUI] hero preset not applied at finalize: {ex}");
            }
        }
    }
}
