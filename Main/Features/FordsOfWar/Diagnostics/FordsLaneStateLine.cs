using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>
/// What the director knows about one stream (one side's units in one lane) when it writes a <c>[LaneState]</c> line:
/// <see cref="Decision"/> is what the lane rules decided on the last tick, with the numbers they decided from, and
/// <see cref="Sent"/> is the last command the director actually sent the engine, which differs from the decision when a refused
/// move fell back to the nearest waypoint or a tower that fell was walked to. Null before the stream was sent anything.
/// </summary>
internal sealed record FordsLaneStateFacts(LaneCommand? Sent, LaneDecision Decision);

/// <summary>The body of a <c>[LaneState]</c> line: pure text from <see cref="FordsLaneStateFacts"/>, invariant culture, no engine.</summary>
internal static class FordsLaneStateLine
{
    internal static string Format(FordsLaneStateFacts facts) =>
        facts.Decision.ToLogBody() + " cmd=" + (facts.Sent?.Kind ?? LaneCommandKind.None) + " sent=" + TargetOf(facts.Sent);

    private static string TargetOf(LaneCommand? command)
    {
        switch (command?.Kind)
        {
            case LaneCommandKind.MoveTo:
            case LaneCommandKind.HoldAt:
                return FordsLogLine.Point(command!.Target);
            case LaneCommandKind.AttackTower:
                return command!.TowerId ?? "-";
            default:
                return "-";
        }
    }
}
