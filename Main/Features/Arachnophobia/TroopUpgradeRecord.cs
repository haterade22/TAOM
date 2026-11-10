using System.Collections.Generic;

namespace TAOM.Features.Arachnophobia;

/// <summary>A troop and the troops it upgrades into.</summary>
public sealed class TroopUpgradeRecord
{
    public TroopUpgradeRecord(string troopId, IReadOnlyList<string> targetIds)
    {
        TroopId = troopId;
        TargetIds = targetIds;
    }

    public string TroopId { get; }
    public IReadOnlyList<string> TargetIds { get; }
}
