using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

public interface IBannerHeroAdapter
{
    ClanColorInfo? GetClanColorInfo(CharacterObject characterObject);
    ClanColorInfo? GetClanColorInfoFromHero(Hero hero);
    void SyncKingdomColors(Clan clan);
}
