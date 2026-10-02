using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TAOM.Core.Logging;

namespace TAOM.Features.FactionUI.Presets;

/// <summary>
/// Registers the hero-preset finalize at character-creation handler priority 1060 (#704), after TAOM's
/// own 1050, so the pick is applied over TAOM's starting kit and race as Kysaro's postfix on TAOM's
/// finalize did, and before Player Switcher's 1100 (which the faction screen turns off while it is in
/// use). Vanilla's core handler is 800, StoryMode 900, NavalDLC 1000; 1060 is free. The engine's handler
/// list is a sorted list that throws on a duplicate priority, so the registration is wrapped: a clash
/// costs the preset, never character creation. Also clears the last campaign's pick.
/// </summary>
public class FactionPresetRegistrationBehavior : CampaignBehaviorBase
{
    public const int HandlerPriority = 1060;

    private readonly FactionPresetService _presets;
    private readonly IModLogger _logger;

    public FactionPresetRegistrationBehavior(FactionPresetService presets, IModLogger logger)
    {
        _presets = presets;
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
        _presets.ResetForNewCharacterCreation();
        try
        {
            manager.RegisterCharacterCreationContentHandler(new FactionPresetContentHandler(_presets, _logger), HandlerPriority);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[FactionUI] could not register the hero-preset handler at priority {HandlerPriority}: {ex.Message}");
        }
    }

    private sealed class FactionPresetContentHandler : ICharacterCreationContentHandler
    {
        private readonly FactionPresetService _presets;
        private readonly IModLogger _logger;

        public FactionPresetContentHandler(FactionPresetService presets, IModLogger logger)
        {
            _presets = presets;
            _logger = logger;
        }

        public void InitializeContent(CharacterCreationManager characterCreationManager)
        {
        }

        public void AfterInitializeContent(CharacterCreationManager characterCreationManager)
        {
        }

        public void OnStageCompleted(CharacterCreationStageBase stage)
        {
        }

        public void OnCharacterCreationFinalize(CharacterCreationManager characterCreationManager)
        {
            try
            {
                _presets.OnCharacterCreationFinalize();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[FactionUI] hero preset not applied at finalize: {ex.Message}");
            }
        }
    }
}
