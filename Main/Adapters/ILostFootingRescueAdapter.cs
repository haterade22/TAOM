using TAOM.Features.LostFooting.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The write side of Lost Footing (#894 phase 2, ADR-007): it moves one stranded agent, or refuses with a reason and
/// moves nothing. Every <c>handle</c> is the opaque token a census probe carried; a stale handle is refused. The census
/// adapter stays read-only. Main thread only (OnMissionTick).
/// </summary>
public interface ILostFootingRescueAdapter
{
    /// <summary>
    /// Moves the agent to the nearest spot within <paramref name="maxMeters"/> that stands on the navigation mesh within
    /// <paramref name="maxStep"/> metres of its own height, and on dry land when <paramref name="needLand"/>.
    /// </summary>
    RescueResult MoveNear(object handle, float maxMeters, float maxStep, bool needLand);

    /// <summary>
    /// Moves the agent to its spot in its formation, when that spot stands on the navigation mesh (on dry land when
    /// <paramref name="needLand"/>) and the player is at least <paramref name="minPlayerMeters"/> from both ends.
    /// </summary>
    RescueResult MoveToFormation(object handle, float minPlayerMeters, bool needLand);
}
