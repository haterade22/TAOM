using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// The live map events, one <see cref="IStuckBattleEventAdapter"/> each, for the stuck-battle guard (#748,
/// docs/features/map-event-guard.md "Stuck AI battles"). No engine type leaves the adapter
/// (ADR-007), so each event is handed out wrapped.
/// </summary>
public interface IStuckBattleAdapter
{
    /// <summary>A wrapper per map event the manager holds now; empty with no campaign.</summary>
    IReadOnlyList<IStuckBattleEventAdapter> LiveBattles();
}
