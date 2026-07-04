namespace TAOM.Features.WarOfTheRingMomentum.Snapshots;

/// <summary>
/// Flat DTO of an army-gathered event. No PlayerInvolved field BY DESIGN — donor parity:
/// LOTRAOM applied the participation multiplier + victory-gate credit only to battles and
/// sieges, never raids/army-gatherings.
/// </summary>
public class ArmyGatheredSnapshot
{
    /// <summary>Army leader's kingdom StringId; null when kingdomless (mid-disband).</summary>
    public string KingdomId { get; set; }

    public string ArmyLeaderName { get; set; }
}
