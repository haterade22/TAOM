using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Adapters;

namespace TAOM.Features.SmartCavalryAI;

/// <summary>
/// Pure-function reroute math: given a cavalry formation's current position, the
/// charge target's position, and the friendly formations on the field, decides whether
/// a friendly non-cavalry formation blocks the charge line and (if so) returns a
/// sidestep waypoint that goes around the closest such blocker.
/// </summary>
public interface ICavalryPathPlanner
{
    /// <param name="cavPosition">Current 2D position of the cavalry formation.</param>
    /// <param name="targetPosition">2D position of the charge target.</param>
    /// <param name="friendlyFormations">Adapters for every other friendly formation on
    /// the field. Cavalry formations and empty formations are filtered internally.</param>
    /// <param name="waypoint">Sidestep waypoint when the method returns true; <c>Vec2.Zero</c>
    /// otherwise.</param>
    /// <returns>True iff a reroute is required.</returns>
    bool TryGetReroutePoint(
        Vec2 cavPosition,
        Vec2 targetPosition,
        IReadOnlyList<IFormationAdapter> friendlyFormations,
        out Vec2 waypoint);
}
