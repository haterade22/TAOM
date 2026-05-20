namespace TAOM.Features.CultureMarketplace.Domain;

public sealed class MarketplaceTuning
{
    public int ItemsPerTownPerDay { get; }

    // Total distinct-item roster size at which we stop injecting more for the day.
    // Codex review 2026-05-20 (C1): cap is checked against the WHOLE roster (vanilla seeds
    // ~25 village-production passes per town in DistributeInitialItemsToTowns, so towns
    // often start at 30-80 distinct items). 200 leaves ample headroom for our daily K=6
    // injection while still bounding unbounded growth from edge cases.
    public int PerTownTotalRosterCap { get; }

    public MarketplaceTuning(int itemsPerTownPerDay, int perTownTotalRosterCap)
    {
        ItemsPerTownPerDay = itemsPerTownPerDay;
        PerTownTotalRosterCap = perTownTotalRosterCap;
    }

    public static MarketplaceTuning Default => new(itemsPerTownPerDay: 6, perTownTotalRosterCap: 200);
}
