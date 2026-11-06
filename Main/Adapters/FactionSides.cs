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
    /// culture; a companion's <c>Hero.Clan</c> is the clan it serves (v1.4.8 Hero.cs:499-503). A clanless
    /// hero (a wanderer) is its own entity: no faction id and its own culture, because
    /// <c>Hero.MapFaction</c> would give it the home town's realm (v1.4.8 Hero.cs:567-590).
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
    /// The player's clan map faction. <c>Clan.PlayerClan</c> dereferences <c>Campaign.Current</c>
    /// (v1.4.8 Clan.cs:276), so there is no pair outside a campaign.
    /// </summary>
    public static SideIds OfPlayer()
    {
        if (Campaign.Current == null)
            return SideIds.None;
        return Of(Clan.PlayerClan?.MapFaction);
    }
}
