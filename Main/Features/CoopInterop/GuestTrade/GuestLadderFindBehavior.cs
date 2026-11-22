using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.CoopInterop.GuestTrade;

/// <summary>
/// Thin boundary for lord's materials found by co-op players (ADR-002): a map event's end hands the winning side's leaders
/// to <see cref="GuestLadderFindService"/>, and each hourly tick lets it hand held finds over. Every decision is the
/// service's. The winners are read by the special resources' <see cref="ISpecialResourceEventAdapter.SnapshotVictory"/>,
/// which lists them only on the authority of a live session, each with its player control from Coop's registry.
/// </summary>
public sealed class GuestLadderFindBehavior : CampaignBehaviorBase
{
    private readonly GuestLadderFindService _finds;
    private readonly ISpecialResourceEventAdapter _events;
    private readonly IModLogger _logger;
    private readonly Random _rng = new();

    public GuestLadderFindBehavior(GuestLadderFindService finds, ISpecialResourceEventAdapter events, IModLogger logger)
    {
        _finds = finds;
        _events = events;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnMapEventEnded(MapEvent mapEvent)
    {
        if (mapEvent == null || !_finds.RollsFinds)
            return;
        try
        {
            if (_events.SnapshotVictory(mapEvent) is { WinningSide: not BattleSideEnum.None } snapshot)
                _finds.OnBattleWon(snapshot.WinnerCandidates, _rng);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[ArmourAcquisition] Lord's material roll for co-op players failed: {ex.Message}");
        }
    }

    private void OnHourlyTick()
    {
        try
        {
            _finds.HandOver();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[ArmourAcquisition] Lord's material hand-over to co-op players failed: {ex.Message}");
        }
    }
}
