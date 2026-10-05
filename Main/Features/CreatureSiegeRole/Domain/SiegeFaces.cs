using System.Collections.Generic;

namespace TAOM.Features.CreatureSiegeRole.Domain;

/// <summary>
/// A siege tower's navmesh ids, as the mission adapter read them. <see cref="DynamicStart"/> is the tower's
/// <c>DynamicNavmeshIdStart</c> (0 when its prefab has no navmesh, a value from 1,000,050 up otherwise); its ground
/// entrance is <c>DynamicStart + 2</c>. <see cref="BridgeId"/> is <c>GetGateNavMeshId()</c>: the bridge to the wall top,
/// a scene-authored id when the tower sets one and <c>DynamicStart + 3</c> otherwise (0 when neither exists).
/// </summary>
public readonly record struct TowerFaces(int DynamicStart, int BridgeId);

/// <summary>Where an exclusion id came from. The order is the priority order: a full cap drops the later tiers first.</summary>
public enum ExclusionTier
{
    TowerEntrance,
    Ladder,
    TowerBridge,
}

/// <summary>An id that was left out because it is not a usable face id (0 or less), and the tier that offered it.</summary>
public readonly record struct SkippedFace(ExclusionTier Tier, int Value);

/// <summary>
/// The navmesh face ids every creature is excluded from, in the one fixed order the engine's set registry needs, with
/// what was left out. <see cref="Ids"/> never holds more than the cap.
/// </summary>
/// <param name="Ids">The ids to exclude, distinct, in order.</param>
/// <param name="Skipped">Ids of 0 or less that a tier offered.</param>
/// <param name="Dropped">Valid ids that did not fit under the cap, in the order they were offered. Bridges come last, so the
/// last <paramref name="DroppedBridges"/> entries are bridges.</param>
/// <param name="DroppedBridges">How many of <paramref name="Dropped"/> are tower bridges, which go quietly: a bridge only
/// matters to a creature already on a tower's wall walk.</param>
public sealed record ExclusionPlan(IReadOnlyList<int> Ids, IReadOnlyList<SkippedFace> Skipped, IReadOnlyList<int> Dropped,
    int DroppedBridges = 0)
{
    /// <summary>True when the cap cut off a tower entrance or a ladder, which is worth a warning.</summary>
    public bool WasTruncated => Dropped.Count > DroppedBridges;
}
