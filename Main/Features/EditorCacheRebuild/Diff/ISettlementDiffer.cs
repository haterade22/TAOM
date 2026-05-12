using TAOM.Adapters;

namespace TAOM.Features.EditorCacheRebuild.Diff;

public interface ISettlementDiffer
{
    SettlementDiff Compute(SettlementSnapshotFile? previous, INavigationCacheAdapter adapter, int maxChangedThreshold);
}
