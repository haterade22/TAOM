using System;
using System.Collections.Generic;
using BehaviorTreeWrapper;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat;

/// <summary>
/// Cells of live agents for the creature trees' range scans, rebuilt every two seconds from the
/// mission tick. Until #592 the trees read it from the engine's asynchronous agent tick while this
/// tick rebuilt it; the third player freeze of 2026-09-13 had nothing but four warg trees, each
/// scanning here on every evaluation. Every reader and the rebuild are on the mission tick now, the
/// rebuild replaces the map instead of clearing it in place, and the tripwire reports any thread
/// that comes back (#595).
/// </summary>
public class SpatialGrid
{
    public static SpatialGrid Instance { get; internal set; }

    private Dictionary<(int, int, int), List<Agent>> _grid = new();
    public float CellSize = 20f;

    private static readonly Action<string> ReportOffThread =
        message => Debug.Print(message, 0, Debug.DebugColor.Red);

    // Removals raised off the main thread (OnAgentDeleted is native's to place, #634), applied from the
    // mission tick by ApplyPendingRemovals so only that tick ever edits a cell list.
    private static readonly Action<string> ReportParkedRemoval =
        message => Debug.Print(message, 0, Debug.DebugColor.Yellow);
    private readonly DeferredCallbackQueue _pendingRemovals = new(ReportParkedRemoval);

    public void UpdateGrid(List<Agent> agents)
    {
        MissionThreadGuard.NoteCall("SpatialGrid.UpdateGrid", ReportOffThread);
        // A fresh map, published by one reference write: a reader that still holds the old one walks a
        // finished structure rather than a map being cleared under it.
        var grid = new Dictionary<(int, int, int), List<Agent>>();
        foreach (Agent agent in agents)
        {
            if (!agent.IsActive())
                continue;
            var cell = GetCell(agent.Position);
            if (!grid.TryGetValue(cell, out List<Agent> list))
            {
                list = new List<Agent>();
                grid[cell] = list;
            }
            list.Add(agent);
        }
        _grid = grid;
    }

    /// <summary>
    /// Drop a deleted agent. Its managed handle would otherwise sit here until the next rebuild,
    /// reading position from the engine slot its index now belongs to (#595).
    /// </summary>
    public void Remove(Agent agent)
    {
        if (agent == null) return;
        _pendingRemovals.RunOrDefer("SpatialGrid.Remove", () => RemoveNow(agent));
    }

    /// <summary>Applies removals parked off the main thread. Call from the mission tick.</summary>
    public void ApplyPendingRemovals() => _pendingRemovals.Drain();

    internal int PendingRemovalCount => _pendingRemovals.Count;

    private void RemoveNow(Agent agent)
    {
        foreach (List<Agent> cell in _grid.Values)
        {
            if (cell.Remove(agent)) return;
        }
    }

    private (int, int, int) GetCell(Vec3 pos)
    {
        return (
            (int)Math.Floor(pos.x / CellSize),
            (int)Math.Floor(pos.y / CellSize),
            (int)Math.Floor(pos.z / CellSize)
        );
    }

    public List<Agent> GetAgentsInRadius(Vec3 center, float radius)
    {
        List<Agent> agents = new();
        GetAgentsInRadius(center, radius, agents);
        return agents;
    }

    /// <summary>
    /// Zero-alloc overload: clears <paramref name="buffer"/> and fills it with the agents in radius. Use from
    /// per-eval BT hot paths (e.g. the creature engage decorators) with a reusable field buffer to avoid a fresh
    /// List allocation every scan — the shared <c>ElephantLikeEngageDecorator</c> uses the equivalent
    /// <c>Mission.GetNearbyAgents(..., scratch)</c> form. The allocating overload above delegates here.
    /// </summary>
    public void GetAgentsInRadius(Vec3 center, float radius, List<Agent> buffer)
    {
        MissionThreadGuard.NoteCall("SpatialGrid.GetAgentsInRadius", ReportOffThread);
        buffer.Clear();
        var grid = _grid;
        float radiusSquared = radius * radius;
        int minX = (int)Math.Floor((center.x - radius) / CellSize);
        int maxX = (int)Math.Floor((center.x + radius) / CellSize);
        int minY = (int)Math.Floor((center.y - radius) / CellSize);
        int maxY = (int)Math.Floor((center.y + radius) / CellSize);
        int minZ = (int)Math.Floor((center.z - radius) / CellSize);
        int maxZ = (int)Math.Floor((center.z + radius) / CellSize);

        // Enumerate ONLY the cells in the radius bounding box (TryGetValue per cell) rather than scanning every
        // occupied cell in the grid and filtering by key — the bbox is tiny for the creature scan ranges (≤~27
        // cells at CellSize 20) while the grid can hold hundreds of cells in a full battle (deep-review 2026-06-15).
        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        for (int z = minZ; z <= maxZ; z++)
        {
            if (!grid.TryGetValue((x, y, z), out List<Agent> cell)) continue;
            foreach (Agent agent in cell)
            {
                float dx = agent.Position.x - center.x;
                float dy = agent.Position.y - center.y;
                float dz = agent.Position.z - center.z;
                if (dx * dx + dy * dy + dz * dz <= radiusSquared)
                    buffer.Add(agent);
            }
        }
    }

    public List<Agent> GetNearAliveAgentsInRange(float range, Agent target)
    {
        return GetAgentsInRadius(target.Position, range);
    }

    public List<Agent> GetNearAliveAgentsInRange(float range, Vec3 targetPos)
    {
        return GetAgentsInRadius(targetPos, range);
    }

    /// <summary>Zero-alloc overload — fills <paramref name="buffer"/> with the alive agents in range of <paramref name="target"/>.</summary>
    public void GetNearAliveAgentsInRange(float range, Agent target, List<Agent> buffer)
    {
        GetAgentsInRadius(target.Position, range, buffer);
    }
}
