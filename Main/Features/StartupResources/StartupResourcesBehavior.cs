using System;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.StartupResources;

public class StartupResourcesBehavior : CampaignBehaviorBase
{
    private readonly IStartupGoldService _goldService;
    private readonly IStartupInfluenceService _influenceService;
    private readonly IModLogger _logger;
    private bool _goldDistributed;
    private bool _influenceDistributed;

    public StartupResourcesBehavior(IStartupGoldService goldService, IStartupInfluenceService influenceService, IModLogger logger)
    {
        _goldService = goldService;
        _influenceService = influenceService;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnNewGameCreatedPartialFollowUpEvent.AddNonSerializedListener(
            this, OnNewGameCreatedPartialFollowUp);
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
