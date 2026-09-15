using System;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// Runs the clan-leadership repair once per session launch (#550).
/// </summary>
/// <remarks>
/// Session launch rather than game load, and no new-versus-loaded distinction: on a new campaign
/// the created character leads <c>player_faction</c> at this point and character creation has not
/// run yet, so the service's own state table declines. Only a loaded takeover save is ever in the
/// state it repairs.
/// </remarks>
public class PlayerClanLeadershipRepairBehavior : CampaignBehaviorBase
{
    private readonly IPlayerClanLeadershipService _leadership;
    private readonly IModLogger _logger;

    public PlayerClanLeadershipRepairBehavior(IPlayerClanLeadershipService leadership, IModLogger logger)
    {
        _leadership = leadership;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => OnSessionLaunched());
    }

    public override void SyncData(IDataStore dataStore)
    {
        // Nothing persists. The repaired state lives in the engine's own clan leader field.
    }

    internal void OnSessionLaunched()
    {
        try
        {
            _leadership.RepairIfNeeded();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Player Switcher: clan leadership repair threw at session launch: {ex}");
        }
    }
}
