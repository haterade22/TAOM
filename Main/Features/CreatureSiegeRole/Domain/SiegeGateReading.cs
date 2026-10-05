namespace TAOM.Features.CreatureSiegeRole.Domain;

/// <summary>
/// One castle gate as the mission adapter read it. <see cref="Handle"/> is the gate object itself, opaque to everything
/// but the adapter that made it (ADR-007): the role service only passes it back for a state read or a strike target.
/// <see cref="DestructionComponent"/> is the same kind of handle, for the damage hook's reference compare.
/// </summary>
/// <param name="Handle">The <c>CastleGate</c>, as an object.</param>
/// <param name="Name">The gate entity's name, for the log.</param>
/// <param name="Visible">The entity is valid and visible including its parents (a hidden gate is a scene author's stub).</param>
/// <param name="Disabled">The mission object is disabled.</param>
/// <param name="OriginX">The gate frame's origin.</param>
/// <param name="OriginY">The gate frame's origin.</param>
/// <param name="OriginZ">The gate frame's origin: the gate's base height.</param>
/// <param name="ForwardX">The gate frame's forward axis as the engine reports it, not normalised. Forward is the attacker side.</param>
/// <param name="ForwardY">The gate frame's forward axis as the engine reports it, not normalised.</param>
/// <param name="HasMiddle">The gate carries a middle position, or falls back to its own frame.</param>
/// <param name="MiddleX">The gate's <c>middle_pos</c> on the ground.</param>
/// <param name="MiddleY">The gate's <c>middle_pos</c> on the ground.</param>
/// <param name="MiddleZ">The ground height at <c>middle_pos</c>; NaN when the engine could not give one.</param>
/// <param name="DestructionComponent">The gate's <c>DestructableComponent</c>, or null.</param>
public sealed record SiegeGateReading(
    object Handle,
    string Name,
    bool Visible,
    bool Disabled,
    float OriginX,
    float OriginY,
    float OriginZ,
    float ForwardX,
    float ForwardY,
    bool HasMiddle,
    float MiddleX,
    float MiddleY,
    float MiddleZ,
    object? DestructionComponent)
{
    /// <summary>A gate the role may use: visible and not disabled.</summary>
    public bool IsUsable => Visible && !Disabled;
}

/// <summary>A gate's state this pass.</summary>
/// <param name="Destroyed">The gate's hit points are gone.</param>
/// <param name="Open">The gate's door is in the open state (a destroyed gate is not "open").</param>
/// <param name="HitPoint">The gate's hit points now; NaN when it is not destructible.</param>
public readonly record struct GateLiveState(bool Destroyed, bool Open, float HitPoint);

/// <summary>
/// What a gate has been doing, kept between passes. <see cref="Destroyed"/> is latched for good. <see cref="OpenSince"/>
/// is the time the door was first seen open in the current unbroken stretch, NaN while it is shut.
/// </summary>
public readonly record struct GateTrack(bool Destroyed, float OpenSince)
{
    /// <summary>A gate not seen open: shut and intact.</summary>
    public static readonly GateTrack Closed = new(false, float.NaN);
}

/// <summary>A battering ram's state, for <see cref="CreatureSiegeRules.RamWorking"/>.</summary>
/// <param name="Present">The siege has a battering ram.</param>
/// <param name="Deactivated">The ram is spent: its gate is destroyed or open and the ram has arrived, or it is disabled.</param>
/// <param name="UserCount">The agents standing at its points now, struck ones included.</param>
/// <param name="IsUsed">An attacking formation is assigned to it.</param>
public readonly record struct RamReading(bool Present, bool Deactivated, int UserCount, bool IsUsed);
