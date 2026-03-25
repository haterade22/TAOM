using TaleWorlds.CampaignSystem;

namespace TAOM.Features.Diplomacy;

public class DiplomacyBehavior : CampaignBehaviorBase
{
    private readonly IDiplomacyService _service;

    public DiplomacyBehavior(IDiplomacyService service)
    {
        _service = service;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnNewGameCreatedPartialFollowUpEvent.AddNonSerializedListener(
            this, OnNewGameCreatedPartialFollowUp);

        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(
            this, _ => _service.EnforcePermanentAlliances());
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnNewGameCreatedPartialFollowUp(CampaignGameStarter starter, int index)
    {
        if (index == 0)
        {
            _service.EstablishInitialAlliances();
        }
    }
}
