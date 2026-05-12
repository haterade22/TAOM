using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Map.DistanceCache;

namespace TAOM.Features.EditorCacheRebuild.Phase1;

public interface IPhase1Filter
{
    bool ShouldComputePair(ISettlementDataHolder s1, ISettlementDataHolder s2);
}

public sealed class ChangedSettlementsFilter : IPhase1Filter
{
    private readonly HashSet<string> _changedIds;

    public ChangedSettlementsFilter(HashSet<string> changedIds)
    {
        _changedIds = changedIds;
    }

    public bool ShouldComputePair(ISettlementDataHolder s1, ISettlementDataHolder s2) =>
        _changedIds.Contains(s1.StringId) || _changedIds.Contains(s2.StringId);
}
