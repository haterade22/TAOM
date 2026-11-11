namespace TAOM.Features.Realms.Domain.Services;

/// <summary>
/// A clan leader who may grant titles to their children, with the clan's seat (the game's home settlement) and the realm
/// they rule, if any: a ruler keeps the realm's capital as their seat even while the realm title is vacant or held by
/// someone else (Gondor's Steward keeps Minas Tirith).
/// </summary>
public sealed class ClanHead
{
    public ClanHead(string leaderId, string? seatSettlementId, string? ruledKingdomId = null)
    {
        LeaderId = leaderId;
        SeatSettlementId = string.IsNullOrEmpty(seatSettlementId) ? null : seatSettlementId;
        RuledKingdomId = string.IsNullOrEmpty(ruledKingdomId) ? null : ruledKingdomId;
    }

    public string LeaderId { get; }
    public string? SeatSettlementId { get; }
    public string? RuledKingdomId { get; }
}
