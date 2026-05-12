using System.Threading;
using TAOM.Adapters;

namespace TAOM.Features.EditorCacheRebuild.Phase1;

public interface IPhase1Builder
{
    Phase1Result Run(INavigationCacheAdapter adapter, CancellationToken ct);

    Phase1Result RunFiltered(INavigationCacheAdapter adapter, IPhase1Filter filter, CancellationToken ct);
}

public readonly struct Phase1Result
{
    public readonly int PairsComputed;
    public readonly double ElapsedSeconds;

    public Phase1Result(int pairsComputed, double elapsedSeconds)
    {
        PairsComputed = pairsComputed;
        ElapsedSeconds = elapsedSeconds;
    }
}
