namespace TAOM.Features.Realms;

/// <summary>The banner a realm flies (the "Your Realm's Banner" MCM option, in its dropdown order).</summary>
public enum RealmBanner
{
    /// <summary>The banner of its ruler's primary title when it has one, else its own (<see cref="KingdomBanners"/>).</summary>
    Title,

    /// <summary>The banner it has: its starting banner, or the founder's for a realm a player founded.</summary>
    Own,

    /// <summary>The ruling clan's own banner (<c>Clan.ClanOriginalBanner</c>).</summary>
    House,
}
