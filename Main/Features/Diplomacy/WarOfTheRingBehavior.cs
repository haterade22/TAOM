using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.Diplomacy;

public class WarOfTheRingBehavior : CampaignBehaviorBase
{
    private readonly IWarOfTheRingService _wotrService;
    private readonly IModLogger _logger;

    public WarOfTheRingBehavior(IWarOfTheRingService wotrService, IModLogger logger)
    {
        _wotrService = wotrService;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        _logger.LogInfo("[WarOfTheRing] WarOfTheRingBehavior registering events");
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnDailyTick()
    {
        var elapsedDays = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;
        _wotrService.CheckPhaseTransition(elapsedDays);
    }
}
