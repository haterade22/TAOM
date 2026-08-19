using System;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;

namespace TAOM.Features.StartupResources;

public class StartupResourcesBehavior : CampaignBehaviorBase
{
    // The last of the ten OnCharacterCreationIsOverEvent phases. v1.5.0's Advanced Starting Options
    // applies the chosen player start at phase 8, so the player's gold is only settled by 9.
    private const int PlayerGoldReapplyPhase = 9;

    private readonly IStartupGoldService _goldService;
    private readonly IStartupInfluenceService _influenceService;
    private readonly IPlayerStartupGoldService _playerGoldService;
    private readonly IModLogger _logger;
    private bool _goldDistributed;
    private bool _influenceDistributed;

    public StartupResourcesBehavior(
        IStartupGoldService goldService,
        IStartupInfluenceService influenceService,
        IPlayerStartupGoldService playerGoldService,
        IModLogger logger)
    {
        _goldService = goldService;
        _influenceService = influenceService;
        _playerGoldService = playerGoldService;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnNewGameCreatedPartialFollowUpEvent.AddNonSerializedListener(
            this, OnNewGameCreatedPartialFollowUp);
        CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(
            this, OnCharacterCreationIsOver);
    }

    // v1.5.0 added `Hero.MainHero.Gold = 1000;` to CharacterCreationState.FinalizeCharacterCreationState,
    // which runs immediately AFTER ApplyFinalEffects (engine CampaignSystem.cs:78947-78948). TAOM grants
    // the player's culture-keyed startup gold from inside ApplyFinalEffects, via
    // CharacterCreationContentService.OnCharacterCreationFinalize, so the engine's hard ASSIGNMENT
    // discards it and every new campaign started at a flat 1000 regardless of culture.
    //
    // Re-apply here, at the last character-creation phase. Gated on the DEFAULT start: Advanced
    // Starting Options assigns its own gold at phase 8 for king / vassal / mercenary / trader /
    // outlaw / beggar, and those are deliberate scenario choices that must not be overwritten.
    private void OnCharacterCreationIsOver(int index)
    {
        if (index != PlayerGoldReapplyPhase) return;

        var startType = Campaign.Current?.AdvancedStartData?.GetStartType() ?? string.Empty;
        if (!string.IsNullOrEmpty(startType) && startType != "default")
        {
            _logger.LogInfo($"[StartupResources] Advanced start '{startType}' set its own gold; leaving it alone");
            return;
        }

        var hero = Hero.MainHero;
        if (hero == null) return;

        try
        {
            _playerGoldService.GrantPlayerStartupGold(hero.Culture?.StringId, hero.StringId);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[StartupResources] Player startup gold re-apply failed: {ex.Message}");
        }
    }

    public void OnNewGameCreatedPartialFollowUp(CampaignGameStarter starter, int index)
    {
        if (index != 1) return;

        if (!_goldDistributed)
        {
            try
            {
                _goldService.DistributeStartupGold();
                _goldDistributed = true;
                _logger.LogInfo("[StartupResources] Gold distribution complete");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[StartupResources] Gold distribution failed: {ex.Message}");
            }
        }

        if (!_influenceDistributed)
        {
            try
            {
                _influenceService.DistributeStartupInfluence();
                _influenceDistributed = true;
                _logger.LogInfo("[StartupResources] Influence distribution complete");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[StartupResources] Influence distribution failed: {ex.Message}");
            }
        }
    }

    public override void SyncData(IDataStore dataStore) { }
}
