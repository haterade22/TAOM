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

    public TroopResourceCostEntry(string troopId, string resourceId, int upgradeCost, float dailyUpkeep, int recruitCost = 0)
    {
        TroopId = troopId;
        ResourceId = resourceId;
        UpgradeCost = upgradeCost;
        DailyUpkeep = dailyUpkeep;
        RecruitCost = recruitCost;
    }
}
