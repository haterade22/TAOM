namespace TAOM.Adapters;

/// <summary>Raw world facts for one army-rhythm evaluation. One fresh probe per call; called by the daily service tick, the daily duty offer and a duty request.</summary>
public sealed class ArmyRhythmProbe
{
    public bool InArmy { get; set; }
    public bool InSiege { get; set; }
    public bool AtSea { get; set; }
    public bool Blockade { get; set; }
    public bool InSettlement { get; set; }
    public bool Moving { get; set; }
    public bool EnemyPartyNearby { get; set; }
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
