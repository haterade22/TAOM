using TaleWorlds.CampaignSystem;
using TAOM.Features.Execution;

namespace TAOM.Adapters;

public class PlayerContextAdapter : IPlayerContextAdapter
{
    public string GetPlayerKingdomId() => Clan.PlayerClan?.Kingdom?.StringId ?? "";
    public SideIds GetPlayerSideIds() => FactionSides.OfPlayer();
    public bool IsUnderMercenaryService() => Clan.PlayerClan?.IsUnderMercenaryService ?? false;
}
