using TaleWorlds.Library;

namespace TAOM.Features.SmartCavalryAI.Models;

public sealed class CavalryFormationState
{
    public CavalryState State { get; set; } = CavalryState.Idle;
    public Vec3 ChargeTarget { get; set; }
    public Vec3 ChargeStartPosition { get; set; }
    public float ChargeStartTime { get; set; }
    public bool IsCoordinatedCharge { get; set; }

    /// <summary>Opaque token for the original charge target, stored across the
    /// Rerouting branch so the service can re-issue ChargeToTarget on waypoint arrival.
    /// Concretely a <c>Formation</c> reference, but the service treats it opaquely.</summary>
    public object? OriginalChargeTargetToken { get; set; }

    public Vec2 ReroutePoint { get; set; }
}
