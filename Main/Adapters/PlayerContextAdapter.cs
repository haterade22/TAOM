using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

public class PlayerContextAdapter : IPlayerContextAdapter
{
    public string GetPlayerKingdomId() => Clan.PlayerClan?.Kingdom?.StringId ?? "";
    public string GetPlayerCultureId() => Hero.MainHero?.Culture?.StringId ?? "";
    public string GetPlayerClanId() => Clan.PlayerClan?.StringId ?? "";
    public bool IsUnderMercenaryService() => Clan.PlayerClan?.IsUnderMercenaryService ?? false;
}
