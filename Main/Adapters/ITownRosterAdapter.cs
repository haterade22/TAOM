using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

public interface ITownRosterAdapter
{
    string GetCurrentCultureId(Settlement settlement);
    string GetSettlementId(Settlement settlement);
    int GetRosterDistinctItemCount(Settlement settlement);
    bool AddItem(Settlement settlement, string itemId, int count);
}
