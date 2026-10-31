namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>One side's formation in one lane as the engine layer measured it this tick. Center is null with no units.</summary>
internal sealed record LaneSnapshot(MatchSide Side, int Lane, int LiveUnits, Vec3f? Center, float NearestEnemyDistance);
