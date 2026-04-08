namespace TAOM.Features.SettlementGuards.Domain;

public sealed class SettlementGuardContext
{
    public string SettlementId { get; }
    public string OwnerClanId { get; }
    public string CultureId { get; }

    public SettlementGuardContext(string settlementId, string ownerClanId, string cultureId)
    {
        SettlementId = settlementId;
        OwnerClanId = ownerClanId;
        CultureId = cultureId;
    }
}
