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
    // The settlement's CURRENT culture id (Settlement.Culture.StringId) — reflects any CultureConversion
    // override. When IsConvertedSettlement is true, recruitment resolves the culture pool for this id,
    // bypassing the settlement/clan pools (which hold the ORIGINAL culture's regional troops).
    public string SettlementCultureId { get; }
    // True when this settlement (or its bound parent) has a completed CultureConversion override.
    public bool IsConvertedSettlement { get; }

    public VolunteerContext(
        string settlementId,
        string boundSettlementId,
        string ownerClanId,
        string cultureId,
        string ownerCultureId = null,
        string settlementCultureId = null,
        bool isConvertedSettlement = false)
    {
        SettlementId = settlementId;
        BoundSettlementId = boundSettlementId;
        OwnerClanId = ownerClanId;
        CultureId = cultureId;
        OwnerCultureId = ownerCultureId;
        SettlementCultureId = settlementCultureId;
        IsConvertedSettlement = isConvertedSettlement;
    }
}
