using TaleWorlds.Library;

namespace TAOM.Features.SmartCavalryAI.Models;

public sealed class CavalryFormationState
{
    public CavalryState State { get; set; } = CavalryState.Idle;

    /// <summary>Mission time the current state was entered. Every hold state's dwell budget
    /// counts from here, so no state can keep riders still for longer than its budget.</summary>
    public float StateEnteredTime { get; set; }

    /// <summary>Opaque token for the formation being charged. Concretely a <c>Formation</c>
    /// reference, but the service treats it opaquely; only the command adapter unwraps it.
    /// Re-pointed at the nearest live enemy when the formation it names empties.</summary>
    public object? TargetToken { get; set; }

    /// <summary>Unit direction of the charge, fixed when the line is drawn. Contact and the
    /// reform point are measured along it, so a flank charge registers when it crosses the
    /// enemy's plane rather than when it reaches the enemy's centre.</summary>
    public Vec2 ChargeDirection { get; set; }

    /// <summary>Where PassingThrough rides to; Reforming holds the line there.</summary>
    public Vec2 ReformPoint { get; set; }

    /// <summary>Where Rerouting rides to before the line charge begins.</summary>
    public Vec2 ReroutePoint { get; set; }
}
