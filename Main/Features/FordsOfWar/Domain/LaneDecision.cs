using System.Globalization;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// What <see cref="LaneMarchRules"/> decided for one side's formation in one lane on one tick, with the numbers it decided from.
/// A lane without units has Branch None and no command. Distances are float.MaxValue when there is no enemy or tower;
/// WaypointIndex is the lane waypoint the march targets and -1 on every other branch.
/// </summary>
internal sealed record LaneDecision(MatchSide Side, int Lane, LaneBranch Branch, LaneOrder Order, int LiveUnits, Vec3f? Center,
    Vec3f? Lead, float LeadArc, int LeadCount, float NearestEnemyDistance, string? NextEnemyTowerId, float NextEnemyTowerDistance,
    float CenterTowerDistance, bool Locked, int WaypointIndex, LaneCommand Command,
    int NearAttackers = -1, int NearDefenders = -1, bool Siege = false)
{
    /// <summary>
    /// The run-log text of this decision (side and lane are the caller's prefix): invariant culture, one decimal, a dash for
    /// what is not there. Pure, so a test pins the format.
    /// </summary>
    public string ToLogBody() =>
        "order=" + Order + " live=" + LiveUnits.ToString(CultureInfo.InvariantCulture) + " branch=" + Branch
        + " center=" + Point(Center) + " lead=" + Point(Lead) + " leadArc=" + F1(LeadArc)
        + " leadCount=" + LeadCount.ToString(CultureInfo.InvariantCulture)
        + " enemy=" + Distance(NearestEnemyDistance) + " tower=" + (NextEnemyTowerId ?? "-")
        + " towerDist=" + Distance(NextEnemyTowerDistance) + " centerTowerDist=" + Distance(CenterTowerDistance)
        + " locked=" + (Locked ? "true" : "false")
        + " wp=" + (WaypointIndex < 0 ? "-" : WaypointIndex.ToString(CultureInfo.InvariantCulture))
        + " target=" + Target() + Near();

    /// <summary>The local counts at the target tower and the siege state; empty when the counts are not known, so older lines read as before.</summary>
    private string Near() => NearAttackers < 0 || NearDefenders < 0
        ? ""
        : " near=" + NearAttackers.ToString(CultureInfo.InvariantCulture) + "/" + NearDefenders.ToString(CultureInfo.InvariantCulture)
            + " siege=" + (Siege ? "true" : "false");

    /// <summary>The point a move or hold goes to, or the tower id an attack is aimed at, so target= reads the same on every line.</summary>
    private string Target() => Command.Kind switch
    {
        LaneCommandKind.MoveTo or LaneCommandKind.HoldAt => Point(Command.Target),
        LaneCommandKind.AttackTower => Command.TowerId ?? "-",
        _ => "-",
    };

    private static string Point(Vec3f? p) => p is { } v && Finite(v.X) && Finite(v.Y) ? "(" + F1(v.X) + "," + F1(v.Y) + ")" : "-";

    private static string Distance(float d) => !Finite(d) || d >= float.MaxValue ? "-" : F1(d);

    private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

    private static string F1(float v) => v.ToString("F1", CultureInfo.InvariantCulture);
}
