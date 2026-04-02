using System.Collections.Generic;

namespace TAOM.Features.Siege.Models;

public class SiegeDefenseConfig
{
    public List<string> WatchedFactionIds { get; set; } = new List<string>();
    public List<string> WatchedSettlementIds { get; set; } = new List<string>();
    public int RelationshipThreshold { get; set; } = -20;
    public int ResponseWindowDays { get; set; } = 3;
    public int RewardRelation { get; set; } = 5;
    public int RewardInfluence { get; set; } = 10;
}
