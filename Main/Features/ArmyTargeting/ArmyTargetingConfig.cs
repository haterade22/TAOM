using System.Collections.Generic;

namespace TAOM.Features.ArmyTargeting;

public class ArmyTargetingConfig
{
    public Dictionary<string, List<string>> FactionPriorityTargets { get; set; } = new();
}
