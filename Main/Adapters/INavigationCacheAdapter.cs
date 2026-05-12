using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Map.DistanceCache;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.EditorCacheRebuild;

namespace TAOM.Adapters;

public interface INavigationCacheAdapter
{
    MobileParty.NavigationType NavigationType { get; }

    (uint SceneCrc, uint NavMeshCrc) GetSceneCrcValues();

    void SerializeCache(string filePath);

    void DeserializeCache(string filePath);

    void RunClosestSettlementCache();

    /// <summary>
    /// Incremental support: remove all distance entries from the deserialized cache where either
    /// endpoint's StringId is in <paramref name="changedIds"/>. Must be called BEFORE Phase 1
    /// RunFiltered, otherwise the rebuild hits Dictionary.Add on existing keys → ArgumentException.
    /// </summary>
    void RemoveDistanceEntriesFor(System.Collections.Generic.HashSet<string> changedIds);

    /// <summary>
    /// Incremental + resume support: clear `_fortificationNeighbors` before Phase 2 reruns.
    /// Vanilla `GenerateNeighborSettlementsCache` opens with `_fortificationNeighbors.Clear()`;
    /// our parallel Phase 2 builders don't, so we must do this externally when we've deserialized
    /// stale neighbor data.
    /// </summary>
    void ClearFortificationNeighbors();

    IReadOnlyList<ISettlementDataHolder> GetAllRegisteredSettlements();

    SettlementCollection GetFortificationsForNeighborDetection();

    void AddClosestEntrancePair(
        ISettlementDataHolder s1, bool isPort1,
        ISettlementDataHolder s2, bool isPort2);

    PairComputeResult ComputeClosestEntrancePair(
        ISettlementDataHolder s1, bool isPort1,
        ISettlementDataHolder s2, bool isPort2);

    void WriteComputedPair(in PairComputeResult result);

    bool CheckBeingNeighbor(
        SettlementCollection settlements,
        ISettlementDataHolder s1,
        ISettlementDataHolder s2);

    void AddNeighbor(ISettlementDataHolder s1, ISettlementDataHolder s2);

    IEnumerable<DistancePair> EnumerateExistingDistances();

    IEnumerable<NeighborPair> EnumerateExistingNeighbors();
}

public readonly struct PairComputeResult
{
    public readonly bool IsValid;
    public readonly object? Element1;
    public readonly object? Element2;
    public readonly float Distance;
    public readonly float LandRatio;

    public PairComputeResult(object element1, object element2, float distance, float landRatio)
    {
        IsValid = true;
        Element1 = element1;
        Element2 = element2;
        Distance = distance;
        LandRatio = landRatio;
    }

    public static PairComputeResult Invalid => default;
}

public readonly struct DistancePair
{
    public readonly CacheElementKey Element1;
    public readonly CacheElementKey Element2;
    public readonly float Distance;
    public readonly float LandRatio;

    public DistancePair(CacheElementKey element1, CacheElementKey element2, float distance, float landRatio)
    {
        Element1 = element1;
        Element2 = element2;
        Distance = distance;
        LandRatio = landRatio;
    }
}

public readonly struct NeighborPair
{
    public readonly string SettlementId1;
    public readonly string SettlementId2;

    public NeighborPair(string settlementId1, string settlementId2)
    {
        SettlementId1 = settlementId1;
        SettlementId2 = settlementId2;
    }
}

public sealed class SettlementCollection
{
    public IReadOnlyList<ISettlementDataHolder> Items { get; }
    internal object UnderlyingList { get; }

    public SettlementCollection(IReadOnlyList<ISettlementDataHolder> items)
    {
        Items = items;
        UnderlyingList = items;
    }

    internal SettlementCollection(IReadOnlyList<ISettlementDataHolder> items, object underlyingList)
    {
        Items = items;
        UnderlyingList = underlyingList;
    }
}
