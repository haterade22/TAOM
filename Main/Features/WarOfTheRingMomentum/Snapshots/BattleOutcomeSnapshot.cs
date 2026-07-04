namespace TAOM.Features.WarOfTheRingMomentum.Snapshots;

/// <summary>
/// Flat DTO of a finished MapEvent, built by IWarEventSnapshotAdapter at the behavior
/// boundary (ADR-007 — no MapEvent crosses into services). Casualty fields come from
/// MapEventSide.TroopCasualties (1.4.6 rename of Casualties).
/// </summary>
public class BattleOutcomeSnapshot
{
    public bool HasWinner { get; set; }
    public bool AttackerWon { get; set; }

    /// <summary>Leader party MapFaction StringId; null when the side has no faction.</summary>
    public string AttackerFactionId { get; set; }
    public string DefenderFactionId { get; set; }

    public bool AttackerIsKingdomFaction { get; set; }
    public bool DefenderIsKingdomFaction { get; set; }
    public bool DefenderIsMobileParty { get; set; }

    public int AttackerCasualties { get; set; }
    public int DefenderCasualties { get; set; }

    /// <summary>FieldBattle / Raid / SiegeOutside (LOTRAOM's valid set) — computed at the boundary.</summary>
    public bool IsValidBattleType { get; set; }

    /// <summary>Player map event, main party on either side, or player-kingdom leader party.</summary>
    public bool PlayerInvolved { get; set; }

    public string AttackerFactionName { get; set; }
    public string DefenderFactionName { get; set; }
    public string AttackerLeaderName { get; set; }
    public string DefenderLeaderName { get; set; }
}
