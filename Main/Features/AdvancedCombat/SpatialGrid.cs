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
        float radiusSquared = radius * radius;
        int minX = (int)Math.Floor((center.x - radius) / CellSize);
        int maxX = (int)Math.Floor((center.x + radius) / CellSize);
        int minY = (int)Math.Floor((center.y - radius) / CellSize);
        int maxY = (int)Math.Floor((center.y + radius) / CellSize);
        int minZ = (int)Math.Floor((center.z - radius) / CellSize);
        int maxZ = (int)Math.Floor((center.z + radius) / CellSize);

        foreach (var kvp in Grid)
        {
            var key = kvp.Key;
            if (key.Item1 >= minX && key.Item1 <= maxX &&
                key.Item2 >= minY && key.Item2 <= maxY &&
                key.Item3 >= minZ && key.Item3 <= maxZ)
            {
                foreach (Agent agent in kvp.Value)
                {
                    float dx = agent.Position.x - center.x;
                    float dy = agent.Position.y - center.y;
                    float dz = agent.Position.z - center.z;
                    if (dx * dx + dy * dy + dz * dz <= radiusSquared)
                    {
                        agents.Add(agent);
                    }
                }
            }
        }
        return agents;
    }

    public List<Agent> GetNearAliveAgentsInRange(float range, Agent target)
    {
        return GetAgentsInRadius(target.Position, range);
    }

    public List<Agent> GetNearAliveAgentsInRange(float range, Vec3 targetPos)
    {
        return GetAgentsInRadius(targetPos, range);
    }
}
