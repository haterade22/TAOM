using System.Collections.Generic;

namespace TAOM.Features.Arachnophobia;

/// <summary>A troop with at least one battle set on the Monster that was asked for.</summary>
public sealed class TroopMountRecord
{
    public TroopMountRecord(string troopId, bool isHero, IReadOnlyList<string> mountItemIds)
    {
        TroopId = troopId;
        IsHero = isHero;
        MountItemIds = mountItemIds;
    }

    public string TroopId { get; }
    public bool IsHero { get; }

    /// <summary>The distinct Horse items on that Monster, across the troop's battle sets.</summary>
    public IReadOnlyList<string> MountItemIds { get; }
}
