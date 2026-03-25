using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.Diplomacy;

public class WarOfTheRingBehavior : CampaignBehaviorBase
{
    private readonly IWarOfTheRingService _wotrService;
    private readonly IModLogger _logger;
    private double _gameStartDay = -1.0;

    public WarOfTheRingBehavior(IWarOfTheRingService wotrService, IModLogger logger)
    {
        _wotrService = wotrService;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnDailyTick()
    {
        if (_gameStartDay < 0.0)
        {
            _gameStartDay = CampaignTime.Now.ToDays;
        }

        var elapsedDays = (float)(CampaignTime.Now.ToDays - _gameStartDay);
        _wotrService.CheckPhaseTransition(elapsedDays);
    }
}
