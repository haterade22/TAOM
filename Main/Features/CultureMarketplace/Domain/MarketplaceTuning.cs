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

    // Maximum number of foreign-culture items the daily filter pass will remove per town
    // per tick. Bounds the net change per tick so a town with dozens of cross-cultural
    // items doesn't have everything ripped out in a single visible tick. Mirrors the
    // ItemsPerTownPerDay injection rate so the net flow is zero in steady state. The
    // initial new-game filter sweep IGNORES this cap (one-time cleanup of vanilla seed).
    public int MaxFilterRemovalsPerTick { get; }

    public MarketplaceTuning(int itemsPerTownPerDay, int perTownTotalRosterCap, int maxFilterRemovalsPerTick)
    {
        ItemsPerTownPerDay = itemsPerTownPerDay;
        PerTownTotalRosterCap = perTownTotalRosterCap;
        MaxFilterRemovalsPerTick = maxFilterRemovalsPerTick;
    }

    public static MarketplaceTuning Default =>
        new(itemsPerTownPerDay: 6, perTownTotalRosterCap: 200, maxFilterRemovalsPerTick: 6);
}
