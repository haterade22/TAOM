namespace TAOM.Features.FiefGranting;

/// <summary>
/// The ids a TAOM fief election needs on a co-op client (#1346). Coop sends the proposer too and sets it
/// after the read; the payload carries it because the decision's constructor needs a clan. Primitives
/// only (ADR-007).
/// </summary>
public sealed class FiefClaimantWireData
{
    public FiefClaimantWireData(string settlementId, string proposerClanId, string clanToExcludeId)
    {
        SettlementId = settlementId;
        ProposerClanId = proposerClanId;
        ClanToExcludeId = clanToExcludeId;
    }

    public string SettlementId { get; }

    public string ProposerClanId { get; }

    /// <summary>Null when no clan is excluded.</summary>
    public string ClanToExcludeId { get; }
}
