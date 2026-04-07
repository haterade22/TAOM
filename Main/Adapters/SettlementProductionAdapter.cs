using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

public class SettlementProductionAdapter : ISettlementProductionAdapter
{
    private readonly Settlement _settlement;

    public SettlementProductionAdapter(Settlement settlement)
    {
        _settlement = settlement;
    }

    public string SettlementId => _settlement?.StringId;
    public string OwnerKingdomId => _settlement?.OwnerClan?.Kingdom?.StringId;
    public int ProsperityLevel => (int)(_settlement?.Town?.Prosperity ?? 0f) / 1000;
    public bool IsPlayerOwned => _settlement?.OwnerClan == Clan.PlayerClan;
}
