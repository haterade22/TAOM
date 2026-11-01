namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// What one side's formation in one lane can see, as the engine layer measures it each tick.
/// NearestEnemyDistance and NextEnemyTowerDistance are measured from the lead group (the Center when there is no Lead);
/// CenterTowerDistance is the tower distance from the Center, kept for the decision trace. NearestEnemyDistance is
/// float.MaxValue when no enemy is around; NextEnemyTowerId is null when none is left.
/// </summary>
internal sealed record LaneSituation(MatchSide Side, int Lane, LaneOrder Order, int LiveUnits, Vec3f? Center,
    float NearestEnemyDistance, string? NextEnemyTowerId, float NextEnemyTowerDistance, bool NextEnemyTowerLocked,
    Vec3f? Lead = null, float LeadArc = 0f, int LeadCount = 0, float CenterTowerDistance = float.MaxValue);
