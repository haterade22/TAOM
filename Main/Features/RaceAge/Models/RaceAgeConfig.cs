using System.Collections.Generic;

namespace TAOM.Features.RaceAge.Models;

public class RaceAgeConfig
{
    public string DefaultRace { get; set; } = "human";
    public Dictionary<string, RaceAgeEntry> Races { get; set; } = new Dictionary<string, RaceAgeEntry>();
}

public class RaceAgeEntry
{
    public int MaxAge { get; set; } = 85;
    public int BecomeOld { get; set; } = 55;
    public int ComesOfAge { get; set; } = 18;
    public int MiddleAge { get; set; } = 35;
    public int FertilityEnd { get; set; } = 45;
    public float FertilityMod { get; set; } = 1.0f;
    public bool Immortal { get; set; }
}
