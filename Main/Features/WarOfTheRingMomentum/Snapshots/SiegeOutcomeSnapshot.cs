namespace TAOM.Features.WarOfTheRingMomentum.Snapshots;

/// <summary>Flat DTO of a won siege (SiegeCompletedEvent with isWin=true).</summary>
public class SiegeOutcomeSnapshot
{
    /// <summary>Captor party MapFaction StringId; null when unresolvable.</summary>
    public string CaptorFactionId { get; set; }

    /// <summary>True when the captor party is the player's main party.</summary>
    public bool PlayerInvolved { get; set; }

    public string CaptorFactionName { get; set; }
    public string CaptorLeaderName { get; set; }
    public string SettlementName { get; set; }
}
