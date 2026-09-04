using System.Collections.Generic;

namespace TAOM.Features.MountDespawn;

/// <summary>
/// Decides WHEN a killed mount is retired. Deliberately knows nothing about <c>Agent</c> or
/// <c>Mission</c>: it takes agent indices and mission time, so every timing rule is testable
/// without a live mission. The MissionBehavior owns the engine handles.
/// </summary>
public interface IDeadMountDespawnService
{
    bool IsEnabled { get; }

    /// <summary>Mounts scheduled but not yet retired. Diagnostics and tests.</summary>
    int PendingCount { get; }

    void OnMountKilled(int agentIndex, float missionTime);

    /// <summary>Drop a scheduled mount, because the engine deleted it first.</summary>
    void Forget(int agentIndex);

    /// <summary>
    /// Indices whose delay has elapsed, capped at <see cref="DeadMountDespawnService.MaxFadesPerSweep"/>
    /// and removed from the schedule. The returned list is a reused buffer: valid until the next call.
    /// </summary>
    IReadOnlyList<int> CollectDue(float missionTime);

    void OnMissionEnd();
}
