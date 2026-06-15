using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat;

public class SpatialGrid
{
    public static SpatialGrid Instance { get; internal set; }

    private readonly Dictionary<(int, int, int), List<Agent>> Grid = new();
    public float CellSize = 20f;

    public void UpdateGrid(List<Agent> agents)
    {
        Grid.Clear();
        foreach (Agent agent in agents)
        {
            if (!agent.IsActive())
                continue;
            var cell = GetCell(agent.Position);
            if (!Grid.TryGetValue(cell, out List<Agent> list))
            {
                list = new List<Agent>();
                Grid[cell] = list;
            }
            list.Add(agent);
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
    /// List allocation every scan — the elephant's <c>EnemyInTrampleRangeDecorator</c> uses the equivalent
    /// <c>Mission.GetNearbyAgents(..., scratch)</c> form. The allocating overload above delegates here.
    /// </summary>
    public void GetAgentsInRadius(Vec3 center, float radius, List<Agent> buffer)
    {
        buffer.Clear();
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
            if (!Grid.TryGetValue((x, y, z), out List<Agent> cell)) continue;
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
