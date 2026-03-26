using TaleWorlds.CampaignSystem;

namespace TAOM.Features.StartupResources;

public class StartupResourcesBehavior : CampaignBehaviorBase
{
    private readonly IStartupGoldService _goldService;
    private readonly IStartupInfluenceService _influenceService;
    private bool _distributed;

    public StartupResourcesBehavior(IStartupGoldService goldService, IStartupInfluenceService influenceService)
    {
        _goldService = goldService;
        _influenceService = influenceService;
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
            _goldService.DistributeStartupGold();
            _influenceService.DistributeStartupInfluence();
            _distributed = true;
        }
    }

    public override void SyncData(IDataStore dataStore) { }
}
