using TaleWorlds.Library;
using TAOM.Adapters.Models;

namespace TAOM.Adapters;

/// <summary>
/// Read-only view onto a <c>Formation</c>'s positioning state plus its unit composition.
/// Services use this instead of <c>Formation</c> directly so the layout positioning math
/// is testable without a live mission. Vec2 is a value struct (not a sealed class), so it's
/// kept on the interface rather than wrapped further.
/// </summary>
public interface IFormationAdapter
{
    int CountOfUnits { get; }
    bool OrderPositionIsValid { get; }
    Vec2 OrderPosition { get; }
    Vec2 Direction { get; }
    float Width { get; }
    float Interval { get; }

    /// <summary>True when the formation's movement state is <c>Hold</c>.</summary>
    bool IsHolding { get; }

    /// <summary>Opaque identity for caching — typically the underlying <c>Formation</c>
    /// reference. Two adapters wrapping the same formation must return the same key.</summary>
    object FormationKey { get; }

    // -- SmartCavalryAI extensions (Patch31) -------------------------------------

    /// <summary>True when the formation's representative class is cavalry — backed by
    /// <c>FormationQuerySystem.IsCavalryFormation</c> in v1.3.15.</summary>
    bool RepresentativeIsCavalry { get; }

    /// <summary>True when the formation is in any non-Hold movement state (charge, move,
    /// retreat, charge-to-target). The complement of <c>IsHolding</c> with explicit
    /// semantics for callers that don't want to invert.</summary>
    bool IsMoving { get; }

    /// <summary>The just-applied movement order's type, or <c>Other</c> for any value
    /// outside the cavalry-feature interest set (Charge/ChargeToTarget). Reads via
    /// <c>Formation.GetReadonlyMovementOrderReference()</c> on v1.3.15 — there is no
    /// public <c>MovementOrder</c> property.</summary>
    MovementOrderType CurrentMovementOrderType { get; }

    /// <summary>The formation's current 2D world position (the centroid that the engine
    /// reports on the same tick). Distinct from <c>OrderPosition</c>, which is the
    /// commanded target.</summary>
    Vec2 CurrentPosition { get; }

    /// <summary>True when the formation's units are tightly lined up perpendicular to
    /// <see cref="Direction"/>, within a tolerance derived from
    /// <paramref name="strictness"/> (0..1 — higher means tighter alignment required).
    /// Used by the cavalry state machine to gate Forming→Charging and Reforming→Idle.</summary>
    bool IsAligned(float strictness);
}
