namespace TAOM.Features.SiegePropDiagnostics.Models;

/// <summary>
/// A primitives-only reading of one resupply prop plus the player's probe against it, captured at
/// the mission boundary. Nothing sealed crosses into the service (ADR-007), so the classifier is
/// fully unit-testable without a live Mission.
///
/// Distances are squared where the engine compares them squared, and are nullable because the
/// boundary cannot always compute them (no points, no player).
/// </summary>
public class SiegePropSnapshot
{
    public int Id { get; set; }
    public SiegePropKind Kind { get; set; }

    /// <summary>Runtime type name of the script — StonePile, ArrowBarrel, JavelinBarrel, ...</summary>
    public string ScriptType { get; set; } = string.Empty;

    /// <summary>Scene entity name, for pointing a map author at the right prop.</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary><c>StonePile.GivenItemID</c>. Null/empty for barrels, which hand out no item.</summary>
    public string? GivenItemId { get; set; }

    /// <summary>Whether <c>MBObjectManager.GetObject&lt;ItemObject&gt;(GivenItemId)</c> returned non-null.</summary>
    public bool GivenItemResolves { get; set; }

    public int AmmoCount { get; set; }
    public int StartingAmmoCount { get; set; }

    public bool MachineIsDisabled { get; set; }
    public bool MachineIsDeactivated { get; set; }

    public int StandingPointCount { get; set; }

    /// <summary>Standing points carrying the machine's <c>AmmoPickUpTag</c>. Recomputed at the boundary — the engine's own list is protected internal.</summary>
    public int AmmoPickupPointCount { get; set; }

    public int DeactivatedPointCount { get; set; }

    /// <summary>Points reporting <c>IsDisabledForAgent(player)</c>.</summary>
    public int DisabledForPlayerPointCount { get; set; }

    /// <summary>Points already in use by another agent.</summary>
    public int OccupiedPointCount { get; set; }

    /// <summary>Result of <c>GetValidVacantReachableStandingPointForAgent(Agent.Main).IsValid</c> — the decisive engine verdict.</summary>
    public bool PlayerProbeValid { get; set; }

    public bool PlayerIsMounted { get; set; }

    /// <summary>Distance from the player to the nearest point, squared, matching the engine's own comparison.</summary>
    public float? NearestPointDistanceSquared { get; set; }

    /// <summary>The engine's interaction distance for this player/point pair, squared. 2m for a player on a standing point.</summary>
    public float? InteractionDistanceSquared { get; set; }

    /// <summary>|point ground z - player z| for the nearest point. The engine requires &lt; 1.5.</summary>
    public float? NearestGroundHeightDelta { get; set; }
}
