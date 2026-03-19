namespace TAOM.Features.TroopProgression;

public readonly struct VolunteerContext
{
    public string SettlementId { get; }
    public string BoundSettlementId { get; }
    public string OwnerClanId { get; }
    public string CultureId { get; }

    public VolunteerContext(
        string settlementId,
        string boundSettlementId,
        string ownerClanId,
        string cultureId)
    {
        SettlementId = settlementId;
        BoundSettlementId = boundSettlementId;
        OwnerClanId = ownerClanId;
        CultureId = cultureId;
    }
}
