using System;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.MapEventGuard.Hooks;

/// <summary>
/// Once per in-game hour, finishes AI battles a battle-end crash left half finalized
/// (<see cref="OrphanedBattleService"/>). The first pass after a load repairs a broken save. Saves nothing.
/// </summary>
public sealed class OrphanedBattleCampaignBehavior : CampaignBehaviorBase
{
    private readonly OrphanedBattleService _service;
    private readonly IModLogger _logger;

    public OrphanedBattleCampaignBehavior(OrphanedBattleService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public override void RegisterEvents() =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);

    public override void SyncData(IDataStore dataStore) { }

    private void OnHourlyTick()
    {
        try
        {
            _service.RepairAll();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[MapEventGuard] orphaned battle sweep failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
