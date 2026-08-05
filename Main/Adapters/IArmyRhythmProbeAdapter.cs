namespace TAOM.Adapters;

/// <summary>Raw world facts for one army-rhythm evaluation. One engine read per game hour.</summary>
public sealed class ArmyRhythmProbe
{
    public bool CommanderResolved { get; set; }
    public bool InArmy { get; set; }
    public bool InSiege { get; set; }
    public bool AtSea { get; set; }
    public bool Blockade { get; set; }
    public bool InSettlement { get; set; }
    public bool Moving { get; set; }
    public int FoodAmount { get; set; }
    public float DaysOfFoodLeft { get; set; }
    public int TroopCount { get; set; }
    public int WoundedCount { get; set; }
    public int LowTierTroopCount { get; set; }
    public bool EnemyPartyNearby { get; set; }
    public bool FactionAtWar { get; set; }
}

/// <summary>
/// Probes the commander party's world state for the duty scheduler. The nearby-enemy scan
/// uses the engine's locator grid (bounded radius), never a full MobileParty.All sweep —
/// the donor ran two full scans per call from 11 call sites.
/// </summary>
public interface IArmyRhythmProbeAdapter
{
    ArmyRhythmProbe Probe(string commanderHeroId);
}
