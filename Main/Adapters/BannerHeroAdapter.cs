using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

public class BannerHeroAdapter : IBannerHeroAdapter
{
    public ClanColorInfo? GetClanColorInfo(CharacterObject characterObject)
    {
        var hero = characterObject?.HeroObject;
        return hero == null ? null : GetClanColorInfoFromHero(hero);
    }

    public ClanColorInfo? GetClanColorInfoFromHero(Hero hero)
    {
        if (hero?.Clan == null) return null;
        var clan = hero.Clan;
        return new ClanColorInfo(clan.StringId, clan.Color, clan.Color2);
    }

    public void SyncKingdomColors(Clan clan)
    {
        if (clan?.Kingdom == null) return;
        var kingdom = clan.Kingdom;
        if (kingdom.RulingClan != clan) return;

        uint bgColor = clan.Color;
        uint iconColor = clan.Color2;
        if (bgColor == iconColor) iconColor = uint.MaxValue;
        if (iconColor == uint.MaxValue) return;

        kingdom.InitializeKingdom(
            kingdom.Name,
            kingdom.InformalName,
            kingdom.Culture,
            clan.Banner,
            bgColor,
            iconColor,
            kingdom.InitialHomeSettlement,
            kingdom.EncyclopediaText,
            kingdom.EncyclopediaTitle,
            kingdom.EncyclopediaRulerTitle);

        foreach (var kClan in kingdom.Clans)
        {
            foreach (var party in kClan.WarPartyComponents)
                party.MobileParty?.Party?.SetVisualAsDirty();
        }
    }
}
