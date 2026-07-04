namespace TAOM.Features.WarOfTheRingMomentum.Snapshots;

/// <summary>
/// Flat DTO of a completed village raid. Deliberate deviation from LOTRAOM: the raider
/// must be an ENROLLED kingdom (LOTRAOM sided raids by culture with no enrollment check,
/// so every looter raid fed Evil +200 — a systemic bias, not a mechanic).
/// </summary>
public class RaidOutcomeSnapshot
{
    public bool AttackerVictory { get; set; }

    /// <summary>Raider leader party MapFaction StringId; null when unresolvable.</summary>
    public string AttackerFactionId { get; set; }

    public string AttackerPartyName { get; set; }
    public string AttackerFactionName { get; set; }
    public string SettlementName { get; set; }
}
