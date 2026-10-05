namespace TAOM.Features.CreatureSiegeRole.Domain;

/// <summary>Which side of a wall battle an agent fights for. <c>None</c> for an agent without a team.</summary>
public enum SiegeSide
{
    None,
    Attacker,
    Defender,
}

/// <summary>
/// The formation order the role rules care about. <c>Other</c> is every order that is neither a charge nor a retreat,
/// including no order at all.
/// </summary>
public enum SiegeFormationOrder
{
    Other,
    Charge,
    ChargeToTarget,
    Retreat,
}

/// <summary>
/// The formation an agent stands in, read through one adapter call. <see cref="IsPlayerCommanded"/> is true once the player
/// has given the formation an order. <c>default</c> is no formation: AI-controlled, no order (vanilla's AI case).
/// </summary>
public readonly record struct SiegeFormationState(bool IsPlayerCommanded, SiegeFormationOrder Order)
{
    /// <summary>No formation: <c>default</c>.</summary>
    public static SiegeFormationState None => default;
}

/// <summary>A point on the battlefield, in the engine's world coordinates. A TAOM value, not a <c>WorldPosition</c>.</summary>
public readonly record struct SiegePoint(float X, float Y, float Z);
