namespace TAOM.Features.CreatureSiegeRole.Domain;

/// <summary>
/// What a creature (an oversized troll) is doing in a wall battle. <see cref="Release"/> is also the state of a creature
/// this feature never routed: vanilla's AI is in charge of it.
/// </summary>
public enum SiegeRole
{
    /// <summary>Vanilla AI is in charge. A routed creature is released once, never again until it is routed anew.</summary>
    Release = 0,

    /// <summary>Attacker, outer gate shut and a ram at work: wait a few metres off the gate instead of in the ram's way.</summary>
    StandOff,

    /// <summary>Attacker, outer gate shut: walk to a slot at the gate and hit it.</summary>
    StrikeOuter,

    /// <summary>Attacker, outer gate passable and the inner gate shut: walk to a slot at the inner gate and hit it.</summary>
    StrikeInner,

    /// <summary>Attacker, every gate passable: hold a ground anchor in the courtyard and fight what reaches it.</summary>
    HoldCourtyard,

    /// <summary>Defender: hold a ground anchor behind the gate, never on a wall top.</summary>
    HoldGate,
}

/// <summary>Why <see cref="CreatureSiegeRules.DecideRole"/> chose a role. Carried into the role-change log line.</summary>
public enum RoleReason
{
    NotAIControlled,
    Fleeing,
    RetreatOrder,
    NoSide,
    PlayerOrder,
    PlayerOrderAfterBreach,
    PlayerDefender,
    RamWorking,
    OuterGateClosed,
    InnerGateClosed,
    BothGatesOpen,
    DefendingGate,
}

/// <summary>The outcome of <see cref="CreatureSiegeRules.DecideRole"/>.</summary>
public readonly record struct RoleDecision(SiegeRole Role, RoleReason Reason);

/// <summary>
/// Everything <see cref="CreatureSiegeRules.DecideRole"/> reads about one creature and the siege. The two gate flags
/// are the reversible status (<see cref="CreatureSiegeRules.IsPassable"/>), never a raw door state.
/// </summary>
/// <param name="IsAIControlled">The agent is AI-controlled on this peer (a player's troll or a co-op puppet is not).</param>
/// <param name="IsFleeing">The agent is running away or retreating.</param>
/// <param name="Side">Which side of the wall battle the agent fights for.</param>
/// <param name="Formation">The formation the agent stands in, and whether the player commands it.</param>
/// <param name="OuterPassable">The outer gate is destroyed or has stood open long enough.</param>
/// <param name="InnerPresent">A usable inner gate exists.</param>
/// <param name="InnerPassable">The inner gate is passable. Meaningless when there is none.</param>
/// <param name="PastOuterGate">The agent is already through the outer gate.</param>
/// <param name="RamWorking">A battering ram is at work on the gate.</param>
public readonly record struct RoleInputs(
    bool IsAIControlled,
    bool IsFleeing,
    SiegeSide Side,
    SiegeFormationState Formation,
    bool OuterPassable,
    bool InnerPresent,
    bool InnerPassable,
    bool PastOuterGate,
    bool RamWorking);
