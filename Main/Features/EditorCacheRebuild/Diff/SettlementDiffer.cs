using System;
using System.Collections.Generic;
using TAOM.Adapters;

namespace TAOM.Features.EditorCacheRebuild.Diff;

public class SettlementDiffer : ISettlementDiffer
{
    private const float PositionTolerance = 1e-3f;

    public SettlementDiff Compute(SettlementSnapshotFile? previous, INavigationCacheAdapter adapter, int maxChangedThreshold)
    {
        if (previous == null)
        {
            return new SettlementDiff
            {
                ForcedFullRebuild = true,
                Reason = "no previous snapshot",
            };
        }

        var (sceneCrc, navMeshCrc) = adapter.GetSceneCrcValues();
        if (previous.SceneCrc != sceneCrc || previous.NavMeshCrc != navMeshCrc)
        {
            return new SettlementDiff
            {
                ForcedFullRebuild = true,
                Reason = $"CRC mismatch (scene {previous.SceneCrc:X8}/{sceneCrc:X8}, navmesh {previous.NavMeshCrc:X8}/{navMeshCrc:X8})",
            };
        }

        if (previous.NavigationType != adapter.NavigationType.ToString())
        {
            return new SettlementDiff
            {
                ForcedFullRebuild = true,
                Reason = $"nav type mismatch ({previous.NavigationType} vs {adapter.NavigationType})",
            };
        }

        var previousById = new Dictionary<string, SettlementSnapshot>(StringComparer.Ordinal);
        foreach (var s in previous.Settlements)
            previousById[s.Id] = s;

        var currentSettlements = adapter.GetAllRegisteredSettlements();
        var added = new HashSet<string>(StringComparer.Ordinal);
        var moved = new HashSet<string>(StringComparer.Ordinal);
        var currentIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var s in currentSettlements)
        {
            currentIds.Add(s.StringId);
            if (!previousById.TryGetValue(s.StringId, out var prev))
            {
                added.Add(s.StringId);
                continue;
            }

            if (HasMoved(prev, s) || prev.HasPort != s.HasPort || prev.IsFortification != s.IsFortification)
                moved.Add(s.StringId);
        }

        var removed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var prevId in previousById.Keys)
        {
            if (!currentIds.Contains(prevId))
                removed.Add(prevId);
        }

        var totalChanged = added.Count + moved.Count + removed.Count;
        if (totalChanged > maxChangedThreshold)
        {
            return new SettlementDiff
            {
                ForcedFullRebuild = true,
                Reason = $"too many changes ({totalChanged} > {maxChangedThreshold})",
                Added = added,
                Moved = moved,
                Removed = removed,
            };
        }

        return new SettlementDiff
        {
            Added = added,
            Moved = moved,
            Removed = removed,
            ForcedFullRebuild = false,
        };
    }

    private static bool HasMoved(SettlementSnapshot prev, TaleWorlds.CampaignSystem.Map.DistanceCache.ISettlementDataHolder current)
    {
        // Compare position scalars only — face index is no longer stored (Codex Finding 4).
        // ToVec2() does not touch Campaign.Current.MapSceneWrapper.
        var gateVec = current.GatePosition.ToVec2();
        if (Math.Abs(prev.GateX - gateVec.x) > PositionTolerance) return true;
        if (Math.Abs(prev.GateY - gateVec.y) > PositionTolerance) return true;

        if (current.HasPort)
        {
            var portVec = current.PortPosition.ToVec2();
            if (Math.Abs(prev.PortX - portVec.x) > PositionTolerance) return true;
            if (Math.Abs(prev.PortY - portVec.y) > PositionTolerance) return true;
        }
        return false;
    }
}
