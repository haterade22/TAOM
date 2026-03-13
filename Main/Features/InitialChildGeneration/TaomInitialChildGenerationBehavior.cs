using TaleWorlds.CampaignSystem;

namespace TAOM.Features.InitialChildGeneration;

public class TaomInitialChildGenerationBehavior : CampaignBehaviorBase
{
    private readonly IInitialChildGenerationService _service;

    public TaomInitialChildGenerationBehavior(IInitialChildGenerationService service)
    {
        _service = service;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnNewGameCreatedPartialFollowUpEvent.AddNonSerializedListener(
            this, OnNewGameCreatedPartialFollowUp);
    }

    public void OnNewGameCreatedPartialFollowUp(CampaignGameStarter starter, int index)
    {
        if (index == 0)
            _service.GenerateInitialChildren();
    }

    public override void SyncData(IDataStore dataStore) { }
}
