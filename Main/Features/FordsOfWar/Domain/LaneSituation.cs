namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// What one side's formation in one lane can see, as the engine layer measures it each tick.
/// NearestEnemyDistance is float.MaxValue when no enemy is around; NextEnemyTowerId is null when none is left.
/// </summary>
internal sealed record LaneSituation(MatchSide Side, int Lane, LaneOrder Order, int LiveUnits, Vec3f? Center,
    float NearestEnemyDistance, string? NextEnemyTowerId, float NextEnemyTowerDistance, bool NextEnemyTowerLocked);
