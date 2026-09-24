using System.Collections.Generic;

namespace TAOM.Features.Siege.Models;

public class SiegeDefenseConfig
{
    public List<string> WatchedSettlementIds { get; set; } = new List<string>();
    // A kingdom mapped to JSON null deserializes as a null value; SiegeDefenseService.GetMessages
    // falls back to its defaults for it.
    public Dictionary<string, KingdomSiegeMessages?> KingdomMessages { get; set; } = new Dictionary<string, KingdomSiegeMessages?>();
    public int RelationshipThreshold { get; set; } = -20;
    public int ResponseWindowDays { get; set; } = 3;
    public int RewardRelation { get; set; } = 5;
    public int RewardInfluence { get; set; } = 10;
}
