using System.Collections.Generic;
using TAOM.Adapters;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider;

public interface ISpiderSpawnerService
{
    /// <summary>
    /// Spawns <paramref name="count"/> spider agents on the given team, distributed
    /// in a circle of <paramref name="radius"/> around <paramref name="referencePosition"/>.
    /// Returns adapters for each successfully-spawned spider.
    /// </summary>
    IReadOnlyList<IAgentAdapter> SpawnSpiders(Team team, Vec3 referencePosition, int count, float radius);
}
