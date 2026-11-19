using TaleWorlds.CampaignSystem.MapEvents;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <inheritdoc cref="IOrphanedBattleEventAdapter"/>
public sealed class OrphanedBattleEventAdapter : IOrphanedBattleEventAdapter
{
    private readonly MapEvent _mapEvent;
    private readonly IModLogger _logger;

    public OrphanedBattleEventAdapter(MapEvent mapEvent, IModLogger logger)
    {
        _mapEvent = mapEvent;
        _logger = logger;
    }

    public string Label() => new StuckBattleEventAdapter(_mapEvent, _logger).Label();

    public HalfFinishedEncounterSnapshot Read() => HalfFinishedEncounterAdapter.Read(_mapEvent);

    public void RestoreBackReference() => HalfFinishedEncounterAdapter.RestoreBackReference(_mapEvent);

    public int EndSide(int sideIndex) => HalfFinishedEncounterAdapter.EndSide(_mapEvent, sideIndex);

    public void FinishEvent() => HalfFinishedEncounterAdapter.FinishEvent(_mapEvent);
}
