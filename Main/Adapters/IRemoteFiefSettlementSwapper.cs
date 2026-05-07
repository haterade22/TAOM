using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

public interface IRemoteFiefSettlementSwapper
{
    bool ReflectionTargetAvailable { get; }
    Settlement Swap(Settlement target);
    void Restore(Settlement original);
}
