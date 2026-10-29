namespace TAOM.Features.StagedSiege.Domain;

/// <summary>What a team's AI does at a stage.</summary>
public enum TacticMode
{
    /// <summary>Vanilla's own siege tactics run.</summary>
    Vanilla,

    /// <summary>Move every eligible formation onto the current point.</summary>
    TakePoint,

    /// <summary>Hold the current point: melee on it, ranged at the recorded hold frame.</summary>
    HoldPoint,
}

/// <summary>
/// The stage table. The staged tactic outweighs vanilla's siege tactics (which weigh about 10) while it has a job and weighs
/// nothing in stage B, so vanilla's own assault and defence of the gatehouse take over there.
/// </summary>
public static class StagePlan
{
    /// <summary>The weight of the staged tactic while it is the active choice.</summary>
    public const float ActiveWeight = 1000f;

    public static TacticMode ModeFor(StageId stage, StagedSide team)
    {
        switch (stage)
        {
            case StageId.A:
                return team == StagedSide.Attacker ? TacticMode.TakePoint : TacticMode.Vanilla;
            case StageId.B:
                return TacticMode.Vanilla;
            default:
                return team == StagedSide.Attacker ? TacticMode.TakePoint : TacticMode.HoldPoint;
        }
    }

    public static float TacticWeight(StageId stage, StagedSide team) =>
        ModeFor(stage, team) == TacticMode.Vanilla ? 0f : ActiveWeight;

    /// <summary>
    /// An AI-led attacker team leaves its siege machines idle while it takes the outer point. The tactic never applies this to a
    /// player general's team: his engines stay under his own orders.
    /// </summary>
    public static bool StopsMachines(StageId stage, StagedSide team) => stage == StageId.A && team == StagedSide.Attacker;

    /// <summary>
    /// One bit for a formation index, 0 outside 0 to 30. The tactic ORs these over its eligible formations so a swap (one empties,
    /// another fills) changes the mask where a count would stay equal.
    /// </summary>
    public static int FormationBit(int formationIndex) => formationIndex >= 0 && formationIndex < 31 ? 1 << formationIndex : 0;

    /// <summary>Stage B belongs to vanilla, so no formation needs the point-moving behavior there.</summary>
    public static bool NeedsDefendBehavior(StageId stage) => stage != StageId.B;
}
