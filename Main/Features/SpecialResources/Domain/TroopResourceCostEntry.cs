namespace TAOM.Features.SpecialResources.Domain;

public sealed class TroopResourceCostEntry
{
    public string TroopId { get; }
    public string ResourceId { get; }
    public int UpgradeCost { get; }
    public float DailyUpkeep { get; }

    public TroopResourceCostEntry(string troopId, string resourceId, int upgradeCost, float dailyUpkeep)
    {
        TroopId = troopId;
        ResourceId = resourceId;
        UpgradeCost = upgradeCost;
        DailyUpkeep = dailyUpkeep;
    }
}
