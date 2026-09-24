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

    private Dictionary<(int, int), List<Agent>> _grid = new();
    public float CellSize = 20f;

    private static readonly Action<string> ReportOffThread =
        message => Debug.Print(message, 0, Debug.DebugColor.Red);

    // Removals raised off the main thread (OnAgentDeleted is native's to place, #634), applied from the
    // mission tick by ApplyPendingRemovals so only that tick ever edits a cell list.
    private static readonly Action<string> ReportParkedRemoval =
        message => Debug.Print(message, 0, Debug.DebugColor.Yellow);
    private readonly DeferredCallbackQueue _pendingRemovals = new(ReportParkedRemoval);

    // The Agent-typed API delegates to the generic helpers below, which never touch an Agent, so
    // tests can run the exact query on plain points. One position read per agent per query.
    private static readonly Func<Agent, Vec3> AgentPosition = agent => agent.Position;
    private static readonly Func<Agent, bool> IsLiveAgent = agent => agent.IsActive();

    public void UpdateGrid(List<Agent> agents)
    {
        MissionThreadGuard.NoteCall("SpatialGrid.UpdateGrid", ReportOffThread);
        // A fresh map, published by one reference write: a reader that still holds the old one walks a
        // finished structure rather than a map being cleared under it.
        _grid = BuildCells(agents, IsLiveAgent, AgentPosition, CellSize);
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

    /// <summary>Buckets every included item by the cell of its position. Pure; tests drive it with plain points.</summary>
    internal static Dictionary<(int, int), List<T>> BuildCells<T>(List<T> items, Func<T, bool> include, Func<T, Vec3> positionOf, float cellSize)
    {
        var cells = new Dictionary<(int, int), List<T>>();
        foreach (T item in items)
        {
            if (!include(item))
                continue;
            Vec3 pos = positionOf(item);
            var key = ((int)Math.Floor(pos.x / cellSize), (int)Math.Floor(pos.y / cellSize));
            if (!cells.TryGetValue(key, out List<T> list))
            {
                list = new List<T>();
                cells[key] = list;
            }
            list.Add(item);
        }
        return cells;
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
        CollectInRadius(_grid, center, radius, CellSize, AgentPosition, buffer);
    }

    /// <summary>
    /// Clears <paramref name="buffer"/> and fills it with the items whose position is within
    /// <paramref name="radius"/> of <paramref name="center"/> (3D distance, inclusive), looking up only
    /// the cells in the query's bounding box. Returns how many cells it looked up. Touches no state
    /// but the caller's buffer.
    /// </summary>
    internal static int CollectInRadius<T>(Dictionary<(int, int), List<T>> cells, Vec3 center, float radius, float cellSize, Func<T, Vec3> positionOf, List<T> buffer)
    {
        buffer.Clear();
        float radiusSquared = radius * radius;
        int minX = (int)Math.Floor((center.x - radius) / cellSize);
        int maxX = (int)Math.Floor((center.x + radius) / cellSize);
        int minY = (int)Math.Floor((center.y - radius) / cellSize);
        int maxY = (int)Math.Floor((center.y + radius) / cellSize);

        // Cells are keyed on (x, y) only: a battlefield's vertical spread is a few metres, so a z axis
        // mostly added empty lookups (7 x 7 x 7 = 343 for the warg's 60 m scan, now 7 x 7 = 49). The
        // distance test below stays 3D, so on a grid built from current positions the result is the
        // same sphere. Between rebuilds (every 2 s) positions are live and cells are not: a column
        // also returns an agent that has since moved up or down into the sphere, which a z cell
        // would have missed. Within a column, items come back in build order.
        int probes = 0;
        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        {
            probes++;
            if (!cells.TryGetValue((x, y), out List<T> cell)) continue;
            foreach (T item in cell)
            {
                Vec3 pos = positionOf(item);
                float dx = pos.x - center.x;
                float dy = pos.y - center.y;
                float dz = pos.z - center.z;
                if (dx * dx + dy * dy + dz * dz <= radiusSquared)
                    buffer.Add(item);
            }
        }
        return probes;
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
