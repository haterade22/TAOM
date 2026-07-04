namespace TAOM.Features.WarOfTheRingMomentum.Domain;

/// <summary>
/// Running per-side totals shown in the popup's "Total Stats" table.
/// Unlike momentum events these never decay.
/// </summary>
public class MomentumTotalStats
{
    public int TotalKills { get; private set; }
    public int TotalVillagesRaided { get; private set; }
    public int TotalSettlementsCaptured { get; private set; }

    public void AddKills(int kills) => TotalKills += kills;
    public void AddRaid() => TotalVillagesRaided++;
    public void AddSettlementCaptured() => TotalSettlementsCaptured++;

    /// <summary>Save-load rehydration only — replaces all counters.</summary>
    public void Restore(int kills, int villagesRaided, int settlementsCaptured)
    {
        TotalKills = kills;
        TotalVillagesRaided = villagesRaided;
        TotalSettlementsCaptured = settlementsCaptured;
    }
}
