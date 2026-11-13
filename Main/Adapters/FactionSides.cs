using TaleWorlds.CampaignSystem;
using TAOM.Features.Execution;

namespace TAOM.Adapters;

/// <summary>
/// The one place a <see cref="SideIds"/> pair is built from engine objects. A pair is the id and the
/// culture of the SAME entity, because <see cref="IAlignmentService.ResolveSide(string, string)"/> falls
/// back from an unlisted or Neutral-listed id to that culture. A kingdom id zipped with a hero's own
/// culture sides a Neutral realm's lord by a culture the realm does not have (#957).
/// </summary>
internal static class FactionSides
{
    /// <summary>The faction's id and the faction's own culture; <see cref="SideIds.None"/> for null.</summary>
    public static SideIds Of(IFaction? faction)
    {
        if (faction == null)
            return SideIds.None;
        return new SideIds(faction.StringId ?? string.Empty, faction.Culture?.StringId ?? string.Empty);
    }

    /// <summary>
    /// A hero in a clan is the clan's map faction (the kingdom, or the clan itself) with that faction's
    /// culture. A clanless hero (a wanderer) is its own entity: no faction id and its own culture, because
    /// <c>Hero.MapFaction</c> would give it the home town's realm (Hero.cs:569-591).
    /// </summary>
    public static SideIds OfHero(Hero? hero)
    {
        if (hero == null)
            return SideIds.None;
        var clan = hero.Clan;
        if (clan != null)
            return Of(clan.MapFaction);
        return new SideIds(string.Empty, hero.Culture?.StringId ?? string.Empty);
    }

    /// <summary>
    /// A lord by their own people: the hero's clan with the clan's culture, never the clan's realm, so an orc clan serving Gondor
    /// still reads Evil. A clanless hero reads their own culture.
    /// </summary>
    public static SideIds OfOwnClan(Hero? hero)
    {
        if (hero == null)
            return SideIds.None;
        var clan = hero.Clan;
        if (clan != null)
            return Of(clan);
        return new SideIds(string.Empty, hero.Culture?.StringId ?? string.Empty);
    }

    /// <summary>A hero character reads <see cref="OfHero"/>; a troop has no faction and reads its own culture.</summary>
    public static SideIds OfCharacter(CharacterObject? character)
    {
        if (character == null)
            return SideIds.None;
        if (character.IsHero)
            return OfHero(character.HeroObject);
        return new SideIds(string.Empty, character.Culture?.StringId ?? string.Empty);
    }

    /// <summary>
    /// The player's clan map faction. <c>Clan.PlayerClan</c> dereferences <c>Campaign.Current</c>
    /// (Clan.cs:284), so there is no pair outside a campaign.
    /// </summary>
    public static SideIds OfPlayer()
    {
        if (Campaign.Current == null)
            return SideIds.None;
        return Of(Clan.PlayerClan?.MapFaction);
    }
}
