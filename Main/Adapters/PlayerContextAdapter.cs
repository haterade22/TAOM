using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

public class PlayerContextAdapter : IPlayerContextAdapter
{
    public string GetPlayerKingdomId() => Clan.PlayerClan?.Kingdom?.StringId ?? "";
    public bool IsUnderMercenaryService() => Clan.PlayerClan?.IsUnderMercenaryService ?? false;
}
