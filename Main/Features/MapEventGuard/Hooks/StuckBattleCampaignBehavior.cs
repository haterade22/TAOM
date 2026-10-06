using System;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.MapEventGuard.Hooks;

/// <summary>
/// Ends AI map battles the engine can never finish, once per in-game hour (#748, docs/features/map-event-guard.md
/// "Stuck AI battles"). Saves nothing. The co-op stand-down lives in <see cref="StuckBattleService.Resolve"/>.
/// </summary>
public sealed class StuckBattleCampaignBehavior : CampaignBehaviorBase
{
    private readonly StuckBattleService _service;
    private readonly IModLogger _logger;

    public StuckBattleCampaignBehavior(StuckBattleService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnHourlyTick()
    {
        try
        {
            _service.Resolve();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[StuckBattle] hourly sweep failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
