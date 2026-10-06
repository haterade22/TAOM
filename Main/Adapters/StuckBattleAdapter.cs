using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <inheritdoc cref="IStuckBattleAdapter"/>
public sealed class StuckBattleAdapter : IStuckBattleAdapter
{
    private readonly IModLogger _logger;

    public StuckBattleAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<IStuckBattleEventAdapter> LiveBattles()
    {
        var events = Campaign.Current?.MapEventManager?.MapEvents;
        if (events == null)
            return new List<IStuckBattleEventAdapter>();

        // A copy: ending one battle fires MapEventEnded, whose listeners can start others.
        var battles = new List<IStuckBattleEventAdapter>(events.Count);
        foreach (var mapEvent in events)
        {
            if (mapEvent != null)
                battles.Add(new StuckBattleEventAdapter(mapEvent, _logger));
        }
        return battles;
    }
}
