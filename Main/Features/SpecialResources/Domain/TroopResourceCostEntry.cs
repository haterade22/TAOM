namespace TAOM.Features.SpecialResources.Domain;

public sealed class TroopResourceCostEntry
{
    public string TroopId { get; }
    public string ResourceId { get; }
    public int UpgradeCost { get; }
    public float DailyUpkeep { get; }

    // One-time cost charged when this troop is RECRUITED as a volunteer (not upgraded into).
    // Distinct from UpgradeCost so a troop that is both an upgrade target and a recruitable
    // volunteer can't be double-charged. Consumed by the RecruitmentVM gate + OnUnitRecruited
    // deduction; 0 means "no recruit cost". The recruited resource is the player's resolved
    // resource, not ResourceId (which is documentation only).
    public int RecruitCost { get; }

    // One-time cost charged when the Elite Emissary feature SELLS this troop from a faction's key
    // settlement. Deliberately distinct from RecruitCost so an elite that is also a recruitable
    // volunteer is never double-charged across the two economies — the volunteer gate reads
    // RecruitCost, the emissary reads MerchantCost. 0 means "not offered by the emissary". The
    // charged resource is the SETTLEMENT OWNER's resolved resource (passed by the caller), not
    // ResourceId (documentation only). See docs/features/elite-emissary.md.
    public int MerchantCost { get; }

    public TroopResourceCostEntry(string troopId, string resourceId, int upgradeCost, float dailyUpkeep, int recruitCost = 0, int merchantCost = 0)
    {
        TroopId = troopId;
        ResourceId = resourceId;
        UpgradeCost = upgradeCost;
        DailyUpkeep = dailyUpkeep;
        RecruitCost = recruitCost;
        MerchantCost = merchantCost;
    }
}
