using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.NamedCompanions;

public class NamedCompanionBehavior : CampaignBehaviorBase
{
    private readonly INamedCompanionService _service;
    private readonly IModLogger _logger;

    public NamedCompanionBehavior(INamedCompanionService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnNewGameCreatedPartialFollowUpEvent.AddNonSerializedListener(
            this, OnNewGamePartialFollowUp);
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(
            this, _ => _service.EnsureCompanionsPlaced());
    }

    public void OnNewGamePartialFollowUp(CampaignGameStarter starter, int index)
    {
        if (index != 1) return;
        _service.SpawnCompanions();
    }

    public override void SyncData(IDataStore dataStore) { }
}
