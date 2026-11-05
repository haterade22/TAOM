using System.Collections.Generic;
using TAOM.Features.LostFooting.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The engine side of the Lost Footing census (ADR-007): reads only, never a write. Every <c>handle</c> is the
/// opaque token a probe carried. Main thread only (OnMissionTick).
/// </summary>
public interface ILostFootingAdapter
{
    /// <summary>Scene id, mission type and the scene's global water level; read once per mission.</summary>
    SceneFacts ReadScene();

    /// <summary>How many agents the mission has now.</summary>
    int AgentCount { get; }

    /// <summary>
    /// Probes the agents at list positions <paramref name="start"/> up to <paramref name="max"/> of them, appending one
    /// probe for each agent worth counting (active, and not a mount that has a rider).
    /// </summary>
    void ReadSlice(int start, int max, List<AgentProbe> into);

    /// <summary>The fuller look at one off-mesh agent. A stale handle answers a detail that is not confirmed.</summary>
    OffMeshDetail Confirm(object handle);
}
