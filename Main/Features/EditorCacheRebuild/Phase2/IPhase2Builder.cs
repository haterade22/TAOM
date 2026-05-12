using System.Threading;
using TAOM.Adapters;

namespace TAOM.Features.EditorCacheRebuild.Phase2;

public interface IPhase2Builder
{
    Phase2Result Run(INavigationCacheAdapter adapter, CancellationToken ct);
}

public readonly struct Phase2Result
{
    public readonly int FortificationsConsidered;
    public readonly int NeighborPairsAdded;
    public readonly double ElapsedSeconds;

    public Phase2Result(int fortificationsConsidered, int neighborPairsAdded, double elapsedSeconds)
    {
        FortificationsConsidered = fortificationsConsidered;
        NeighborPairsAdded = neighborPairsAdded;
        ElapsedSeconds = elapsedSeconds;
    }
}
