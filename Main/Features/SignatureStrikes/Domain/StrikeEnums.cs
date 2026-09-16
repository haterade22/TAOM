namespace TAOM.Features.SignatureStrikes.Domain;

/// <summary>The swing the engine animated, read off <c>AttackCollisionData.AttackDirection</c>
/// at the boundary. A signature hero's effects key on this, never on the weapon.</summary>
public enum StrikeDirection
{
    None,
    Overhead,
    Thrust,
    Left,
    Right,
}

/// <summary>What a direction's profile does. Slam: a boulder-style ring around the impact.
/// Sweep: a stagger ring in front of the attacker. The cooldown is per kind, so a slam and a
/// sweep never gate each other.</summary>
public enum StrikeKind
{
    Slam,
    Sweep,
}

/// <summary>
/// Mirror of the engine's <c>CombatCollisionResult</c>, by NAME (SignatureStrikesBindingTests pins
/// the two enums against each other), so the service never sees an engine type and the boundary
/// switch cannot silently map a renamed member onto the wrong branch.
/// </summary>
public enum StrikeCollision
{
    None,
    StrikeAgent,
    HitWorld,
    Blocked,
    Parried,
    ChamberBlocked,
}
