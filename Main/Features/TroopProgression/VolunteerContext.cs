namespace TAOM.Features.TroopProgression;

public readonly struct VolunteerContext
{
    public string SettlementId { get; }
    public string BoundSettlementId { get; }
    public string OwnerClanId { get; }
    public string CultureId { get; }
    // Owner clan's culture (may differ from settlement's baseline CultureId when the settlement has been conquered).
    // Used by conditional pools — e.g. Ithil Guard only spawns at town_ES2 when OwnerCultureId == "gondor".
    public string OwnerCultureId { get; }

    public VolunteerContext(
        string settlementId,
        string boundSettlementId,
        string ownerClanId,
        string cultureId,
        string ownerCultureId = null)
    {
        SettlementId = settlementId;
        BoundSettlementId = boundSettlementId;
        OwnerClanId = ownerClanId;
        CultureId = cultureId;
        OwnerCultureId = ownerCultureId;
    }
}
