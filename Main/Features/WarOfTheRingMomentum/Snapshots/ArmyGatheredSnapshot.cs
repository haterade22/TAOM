namespace TAOM.Features.WarOfTheRingMomentum.Snapshots;

/// <summary>Flat DTO of an army-gathered event.</summary>
public class ArmyGatheredSnapshot
{
    /// <summary>Army leader's kingdom StringId; null when kingdomless (mid-disband).</summary>
    public string KingdomId { get; set; }

    public string ArmyLeaderName { get; set; }
}
