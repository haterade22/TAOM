using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// One side's formation in one lane as the engine layer measured it this tick. Center is null with no units.
/// UnitPositions are the live units' positions, from which the match measures the lane's lead group; a snapshot without
/// positions has no lead, and the rules use the Center in its place. UnitKeys, when given, lists the agent key of each
/// position in the same order, so the match can weigh the units near a point by their recorded value.
/// </summary>
internal sealed record LaneSnapshot(MatchSide Side, int Lane, int LiveUnits, Vec3f? Center, float NearestEnemyDistance,
    IReadOnlyList<Vec3f>? UnitPositions = null, IReadOnlyList<AgentKey>? UnitKeys = null);
