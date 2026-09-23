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

/// <summary>What a direction's profile does, and the unit a cooldown counts in. Slam: Sauron's
/// boulder-style ring around the impact. Sweep: his stagger ring. Scream: the Nine's shriek, a
/// ring around the wraith itself (#645). The cooldown is per kind, so a slam and a sweep never
/// gate each other, while the three directions that all map to Scream share one timer. Append new
/// kinds at the end: <see cref="StrikeKindTimes"/> indexes stamps by the member's value.</summary>
public enum StrikeKind
{
    Slam,
    Sweep,
    Scream,
}

/// <summary>Where a strike's ring is centred: the point the weapon hit, or the attacker.</summary>
public enum StrikeOrigin
{
    Impact,
    Self,
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
