using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.Diplomacy;

public class DiplomacyBehavior : CampaignBehaviorBase
{
    private readonly IDiplomacyService _service;
    private readonly IModLogger _logger;

    public DiplomacyBehavior(IDiplomacyService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        _logger.LogInfo("[Diplomacy] DiplomacyBehavior registering events");
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
            _logger.LogInfo("[Diplomacy] New game created — establishing initial alliances");
            _service.EstablishInitialAlliances();
        }
    }
}
