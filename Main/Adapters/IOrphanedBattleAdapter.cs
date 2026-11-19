using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>Finds map events a battle-end crash left half finalized, through the parties still attached to them.</summary>
public interface IOrphanedBattleAdapter
{
    /// <summary>
    /// Each distinct finalized map event that a mobile party still references, except the player's encounter
    /// (Patch170 owns that one). The engine's event manager no longer lists these events.
    /// </summary>
    IReadOnlyList<IOrphanedBattleEventAdapter> AttachedFinalizedBattles();
}
