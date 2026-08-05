namespace TAOM.Features.FieldCommission.Domain;

/// <summary>
/// A queued-but-not-yet-shown promotion offer. Carries the troop id/name only — never persisted
/// (SyncData round-trips merits + promoted-hero ids, not the offer queue; see
/// <c>docs/features/field-commission.md</c> Save section).
/// </summary>
public sealed class PendingPromotionOffer
{
    public PendingPromotionOffer(string troopId, string troopName)
    {
        TroopId = troopId;
        TroopName = troopName;
    }

    public string TroopId { get; }

    public string TroopName { get; }
}
