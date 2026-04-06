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
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
    {
        // Eagerly recompute phase on load so diplomacy guards are active
        // before the first daily tick (prevents save/load peace exploit)
        var elapsedDays = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;
        _wotrService.CheckPhaseTransition(elapsedDays);
        _logger.LogInfo($"[WarOfTheRing] Session launched — phase restored at day {elapsedDays:F0}");
    }

    private void OnDailyTick()
    {
        var elapsedDays = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;
        _wotrService.CheckPhaseTransition(elapsedDays);
    }
}
