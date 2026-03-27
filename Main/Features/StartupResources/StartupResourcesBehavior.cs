using System;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.StartupResources;

public class StartupResourcesBehavior : CampaignBehaviorBase
{
    private readonly IStartupGoldService _goldService;
    private readonly IStartupInfluenceService _influenceService;
    private readonly IModLogger _logger;
    private bool _distributed;

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
        if (index == 1 && !_distributed)
        {
            _logger.LogInfo("[StartupResources] Distributing startup gold and influence...");
            try
            {
                _goldService.DistributeStartupGold();
                _influenceService.DistributeStartupInfluence();
                _distributed = true;
                _logger.LogInfo("[StartupResources] Distribution complete");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[StartupResources] Distribution failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    public override void SyncData(IDataStore dataStore) { }
}
