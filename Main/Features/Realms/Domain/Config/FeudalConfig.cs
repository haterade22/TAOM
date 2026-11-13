using System.Collections.Generic;
using TAOM.Features.Realms.Domain.Ledger;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Features.Realms.Domain.Config;

/// <summary>
/// Grants, revocations, the chronicle and the weekly effects of titles, from &lt;Grants&gt;, &lt;Chronicle&gt; and
/// &lt;Consequences&gt; in realms_titles.xml. Every effect runs on the weekly tick or on a campaign event, never per tick.
/// </summary>
public sealed class FeudalConfig
{
    /// <summary>Relation change of the recipient with the giver when a title is granted.</summary>
    public int GrantRelation { get; set; } = 10;

    /// <summary>Relation change of the old holder with the revoker.</summary>
    public int RevokeRelation { get; set; } = -30;

    /// <summary>Influence it costs to revoke a title from a holder outside the revoker's clan. A rank without a cost cannot be revoked.</summary>
    public Dictionary<TitleRank, int> RevokeInfluence { get; } = new()
    {
        [TitleRank.Lordship] = 5,
        [TitleRank.Barony] = 25,
        [TitleRank.County] = 50,
        [TitleRank.Duchy] = 100,
        [TitleRank.Kingdom] = 200,
    };

    /// <summary>How many changes of holder each title and office remembers.</summary>
    public int ChronicleEntries { get; set; } = 20;

    /// <summary>Weekly relation change of a holder by right with the lord of another clan who owns the title's town or castle.</summary>
    public int ResentmentRelation { get; set; } = -1;

    /// <summary>Weekly loyalty change of a town held by a lord without its title while the holder lives in another clan.</summary>
    public int ResentmentLoyalty { get; set; } = -1;

    /// <summary>Weekly relation change of a vassal of another clan with the holder of their title's direct liege title.</summary>
    public int LiegeRelation { get; set; } = 1;

    /// <summary>The liege's pull stops raising relation once it is this high.</summary>
    public int LiegeRelationCap { get; set; } = 30;

    /// <summary>The lowest rank of liege title that pulls its vassals.</summary>
    public TitleRank LiegeMinRank { get; set; } = TitleRank.Duchy;

    /// <summary>Share of the influence cost a liege saves when calling a vassal (by de jure title) to their army.</summary>
    public float ArmyDiscount { get; set; } = 0.5f;

    /// <summary>
    /// The lowest rank of title whose holder owes tribute to the holder of its direct liege title (tribute_rank). Below it,
    /// a lordship owes nothing.
    /// </summary>
    public TitleRank TributeMinRank { get; set; } = TitleRank.Barony;

    /// <summary>
    /// The percentage of a vassal clan's daily income from fiefs its liege clans receive, split among them (the MCM's
    /// Tribute From Vassals). 0 when receiving is off.
    /// </summary>
    public int TributeReceivePercent { get; set; } = 5;

    /// <summary>
    /// The percentage of a clan's daily income from fiefs it pays its liege clans, split among them (the MCM's Tribute To
    /// Liege). 0 when paying is off.
    /// </summary>
    public int TributePayPercent { get; set; } = 5;

    /// <summary>Revoke costs as a percentage of <see cref="RevokeInfluence"/> (the MCM's Title Influence Cost).</summary>
    public int InfluencePercent { get; set; } = 100;

    public int? RevokeCost(TitleRank rank) =>
        RevokeInfluence.TryGetValue(rank, out var cost) && cost >= 0 ? RealmsPercent.Of(cost, InfluencePercent) : null;
}

/// <summary>A union title (rank <see cref="TitleRank.Union"/>) over existing realm and high titles, formed by holding all of them.</summary>
public sealed class UnionConfig
{
    public UnionConfig(string id, string name, IReadOnlyList<string> memberTitleIds)
    {
        Id = id;
        Name = name;
        MemberTitleIds = memberTitleIds;
    }

    public string Id { get; }
    public string Name { get; }

    /// <summary>Title ids, for example k_empire_w and e_arnor.</summary>
    public IReadOnlyList<string> MemberTitleIds { get; }
}

/// <summary>A claim a hero has from the start of the campaign (Aragorn's on Gondor and Arnor).</summary>
public sealed class StartingClaimConfig
{
    public StartingClaimConfig(string heroId, string titleId, ClaimKind kind)
    {
        HeroId = heroId;
        TitleId = titleId;
        Kind = kind;
    }

    public string HeroId { get; }
    public string TitleId { get; }
    public ClaimKind Kind { get; }
}

/// <summary>
/// What follows when the hero restores the realm title (Aragorn taking up the crown of Gondor): he leaves the player's
/// clan, founds his own house with the banner of <see cref="BannerOfClanId"/>, becomes the realm's ruling clan and takes
/// <see cref="FiefIds"/> from the house that ruled before, with their titles and any <see cref="TitleIds"/> it held.
/// The former ruler keeps or gets <see cref="OfficeId"/>.
/// </summary>
public sealed class CoronationConfig
{
    public CoronationConfig(string titleId, string heroId, string clanName, string? informalName, string? bannerOfClanId,
        IReadOnlyList<string> fiefIds, IReadOnlyList<string> titleIds, string? officeId, int tier, int troops, int gold, int relation,
        int? influence = null, string? formerBanner = null, CoronationEvent? coronationEvent = null, string? banner = null)
    {
        Influence = influence;
        FormerBanner = string.IsNullOrWhiteSpace(formerBanner) ? null : formerBanner;
        Banner = string.IsNullOrWhiteSpace(banner) ? null : banner;
        Event = coronationEvent ?? CoronationEvent.Default;
        TitleId = titleId;
        HeroId = heroId;
        ClanName = clanName;
        InformalName = informalName;
        BannerOfClanId = bannerOfClanId;
        FiefIds = fiefIds;
        TitleIds = titleIds;
        OfficeId = officeId;
        Tier = tier;
        Troops = troops;
        Gold = gold;
        Relation = relation;
    }

    public string TitleId { get; }
    public string HeroId { get; }

    /// <summary>The new house's name, a text id ("{=realms_clan_telcontar}House of Telcontar").</summary>
    public string ClanName { get; }

    public string? InformalName { get; }

    /// <summary>The clan whose banner the new house copies, and whose culture it takes.</summary>
    public string? BannerOfClanId { get; }

    /// <summary>Settlements the former ruling house gives the new king (his seat first).</summary>
    public IReadOnlyList<string> FiefIds { get; }

    /// <summary>Further titles (a duchy) the former ruling house gives up to him.</summary>
    public IReadOnlyList<string> TitleIds { get; }

    public string? OfficeId { get; }
    public int Tier { get; }

    /// <summary>The size his party is filled to.</summary>
    public int Troops { get; }

    /// <summary>Gold the new house starts with; it is not taken from the player.</summary>
    public int Gold { get; }

    /// <summary>His relation with the player rises by this.</summary>
    public int Relation { get; }

    /// <summary>The influence the restore costs instead of the usurp price of its rank; null keeps that price.</summary>
    public int? Influence { get; }

    /// <summary>
    /// The banner code the former ruling house takes once it no longer rules the realm (as ruling house it showed the
    /// realm's banner, which the new house keeps); null leaves its banner alone.
    /// </summary>
    public string? FormerBanner { get; }

    /// <summary>
    /// The new house's own banner code. As the realm's ruling clan it shows the realm's banner, so the realm takes it
    /// too. Null keeps the banner it takes from <see cref="BannerOfClanId"/>.
    /// </summary>
    public string? Banner { get; }

    /// <summary>The coronation event window: heading, story text (text ids), the illustration's sprite and the button.</summary>
    public CoronationEvent Event { get; }
}

/// <summary>
/// The window shown when the coronation happens. Each part has a default in the game layer when left out; without an
/// image the window shows the new house's banner in the illustration's place.
/// </summary>
public sealed class CoronationEvent
{
    public static readonly CoronationEvent Default = new(null, null, null, null);

    public CoronationEvent(string? heading, string? text, string? image, string? button)
    {
        Heading = Empty(heading);
        Text = Empty(text);
        Image = Empty(image);
        Button = Empty(button);
    }

    public string? Heading { get; }
    public string? Text { get; }

    /// <summary>A sprite name for the illustration.</summary>
    public string? Image { get; }

    public string? Button { get; }

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>
/// An office of a realm, held by one hero at a time: the Steward of Gondor, a Marshal of the Mark. It styles its holder
/// ("Steward {NAME}" known as "of Gondor") ahead of any title of its rank or lower, and pays influence every week.
/// </summary>
public sealed class OfficeConfig
{
    public OfficeConfig(string id, string kingdomId, string name, string? holderMale, string? holderFemale, string? of,
        TitleRank rank, bool followsRuler, int influence, string? initialHolderId)
    {
        Id = id;
        KingdomId = kingdomId;
        Name = name;
        HolderMale = string.IsNullOrEmpty(holderMale) ? null : holderMale;
        HolderFemale = string.IsNullOrEmpty(holderFemale) ? null : holderFemale;
        Of = string.IsNullOrEmpty(of) ? null : of;
        Rank = rank;
        FollowsRuler = followsRuler;
        Influence = influence;
        InitialHolderId = string.IsNullOrEmpty(initialHolderId) ? null : initialHolderId;
    }

    public string Id { get; }
    public string KingdomId { get; }

    /// <summary>The office's name, for example "{=...}Steward of Gondor".</summary>
    public string Name { get; }

    /// <summary>How its holder is styled, {NAME} being the hero's name; null for the plain name.</summary>
    public string? HolderMale { get; }

    public string? HolderFemale { get; }

    /// <summary>What follows "of" in the holder's full name ("Steward Denethor of Gondor"); null to add nothing.</summary>
    public string? Of { get; }

    /// <summary>The office styles its holder ahead of their titles of this rank or lower.</summary>
    public TitleRank Rank { get; }

    /// <summary>
    /// The realm's ruler holds it while they do not hold the realm title (a Steward rules for an absent king); once the
    /// ruler holds the realm title, the ruler appoints someone else to it.
    /// </summary>
    public bool FollowsRuler { get; }

    /// <summary>Influence its holder's clan gains each week.</summary>
    public int Influence { get; }

    /// <summary>Appointed when the office is made, if alive and in the realm.</summary>
    public string? InitialHolderId { get; }
}
