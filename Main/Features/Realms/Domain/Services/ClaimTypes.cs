using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.Realms.Domain.Services;

/// <summary>What the claim rules need to know of the live campaign, by string id.</summary>
public interface IClaimWorld
{
    /// <summary>The hero who owns the settlement; a village's owner is the owner of its town or castle.</summary>
    string? OwnerOf(string settlementId);

    /// <summary>The kingdom the settlement's owner clan belongs to now.</summary>
    string? KingdomOfSettlement(string settlementId);

    string? KingdomOfHero(string heroId);

    string? ClanOf(string heroId);

    /// <summary>The realm's ruler; null when the realm is gone.</summary>
    string? RulerOf(string kingdomId);
}

/// <summary>Who wants a claim: a hero, their clan and realm, and the towns and castles their clan owns.</summary>
public sealed class ClaimBuyer
{
    public ClaimBuyer(string heroId, string? clanId, string? kingdomId, IReadOnlyCollection<string> fortificationIds)
    {
        HeroId = heroId;
        ClanId = clanId;
        KingdomId = kingdomId;
        FortificationIds = fortificationIds;
    }

    public string HeroId { get; }
    public string? ClanId { get; }
    public string? KingdomId { get; }
    public IReadOnlyCollection<string> FortificationIds { get; }
}

public enum ClaimRefusal
{
    None,

    /// <summary>Claims on this rank cannot be bought, or it cannot be usurped.</summary>
    NotForSale,

    /// <summary>The hero holds the title already.</summary>
    Held,

    /// <summary>The hero claims the title already.</summary>
    Claimed,

    /// <summary>A member of the hero's own clan holds it.</summary>
    OwnClan,

    /// <summary>It lies in or is held from the hero's own realm.</summary>
    OwnRealm,

    /// <summary>None of its lands border the hero's clan's towns and castles.</summary>
    TooFar,

    /// <summary>Usurping needs a claim.</summary>
    NoClaim,

    /// <summary>Usurping a town, castle or village title needs the fief in the hero's clan.</summary>
    NotOwned,

    /// <summary>Usurping or forming a duchy or higher needs more than half of its lands held by the hero, their clan or their vassals.</summary>
    TooFewVassals,

    /// <summary>The hero does not hold the title, so cannot grant it.</summary>
    NotHolder,

    /// <summary>This rank of title cannot be granted (lordships alone, realms and high titles).</summary>
    NotGrantable,

    /// <summary>The recipient is the giver, or neither in the giver's clan nor in their realm.</summary>
    Recipient,

    /// <summary>Revoking needs a liege title above it held by the revoker, or its holder in the revoker's clan.</summary>
    NotLiege,

    /// <summary>The holder serves another realm.</summary>
    OutsideRealm,

    /// <summary>The title has no holder to revoke it from.</summary>
    Vacant,

    /// <summary>Forming or restoring needs a vacant title with no succession waiting.</summary>
    NotVacant,

    /// <summary>Restoring needs an ancestral claim and the claimant serving the title's realm.</summary>
    NotServing,

    /// <summary>This throne is only restored by an heir with an ancestral claim, never formed.</summary>
    RestoreOnly,
}

/// <summary>How a granted title passes.</summary>
public enum GrantMode
{
    None,

    /// <summary>To a member of the giver's clan, with every title below it the giver holds.</summary>
    WithVassals,

    /// <summary>A duchy to a lord of another clan in the giver's realm: the title alone; its lands stay with the giver.</summary>
    TitleOnly,

    /// <summary>A town or castle to a lord of another clan: the game gives the fief, and the title moves with it.</summary>
    ByFief,
}

/// <summary>Whether a grant is allowed and how the title passes.</summary>
public readonly struct GrantQuote
{
    public GrantQuote(ClaimRefusal refusal, GrantMode mode)
    {
        Refusal = refusal;
        Mode = mode;
    }

    public ClaimRefusal Refusal { get; }
    public GrantMode Mode { get; }
    public bool Allowed => Refusal == ClaimRefusal.None;

    public static GrantQuote Refused(ClaimRefusal refusal) => new(refusal, GrantMode.None);
}

/// <summary>A town or castle whose owner does not hold its title while its holder by right lives in another clan.</summary>
public sealed class TitleGap
{
    public TitleGap(string titleId, string settlementId, string holderId, string ownerId)
    {
        TitleId = titleId;
        SettlementId = settlementId;
        HolderId = holderId;
        OwnerId = ownerId;
    }

    public string TitleId { get; }
    public string SettlementId { get; }
    public string HolderId { get; }
    public string OwnerId { get; }
}

/// <summary>Weekly gold a vassal owes the holder of their title's liege title.</summary>
public sealed class Tribute
{
    public Tribute(string payerId, string payeeId, int gold)
    {
        PayerId = payerId;
        PayeeId = payeeId;
        Gold = gold;
    }

    public string PayerId { get; }
    public string PayeeId { get; }
    public int Gold { get; }
}

/// <summary>What the office rules need beyond <see cref="IClaimWorld"/>.</summary>
public interface IOfficeWorld : IClaimWorld
{
    bool IsAlive(string heroId);
}

/// <summary>Whether a claim action is allowed and what it costs. Cost is meaningful only when <see cref="Allowed"/>.</summary>
public readonly struct ClaimQuote
{
    public ClaimQuote(ClaimRefusal refusal, int cost)
    {
        Refusal = refusal;
        Cost = cost;
    }

    public ClaimRefusal Refusal { get; }
    public int Cost { get; }
    public bool Allowed => Refusal == ClaimRefusal.None;

    public static ClaimQuote Refused(ClaimRefusal refusal) => new(refusal, 0);
}

/// <summary>
/// For every town and castle, the nearest others on the map. Positions do not move, so the game builds this once.
/// </summary>
public sealed class FiefNeighbors
{
    public static readonly FiefNeighbors Empty = new(new Dictionary<string, string[]>());

    private readonly Dictionary<string, string[]> _neighbors;

    private FiefNeighbors(Dictionary<string, string[]> neighbors) => _neighbors = neighbors;

    /// <param name="fortifications">Towns and castles with their map position.</param>
    /// <param name="count">How many nearest others each one borders.</param>
    public static FiefNeighbors Build(IEnumerable<(string Id, float X, float Y)> fortifications, int count)
    {
        var all = fortifications.GroupBy(f => f.Id, StringComparer.Ordinal).Select(g => g.First()).ToList();
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var f in all)
            result[f.Id] = all.Where(o => o.Id != f.Id)
                .OrderBy(o => (o.X - f.X) * (o.X - f.X) + (o.Y - f.Y) * (o.Y - f.Y)).ThenBy(o => o.Id, StringComparer.Ordinal)
                .Take(count).Select(o => o.Id).ToArray();
        return new FiefNeighbors(result);
    }

    public IReadOnlyList<string> Of(string settlementId) =>
        _neighbors.TryGetValue(settlementId, out var list) ? list : Array.Empty<string>();
}
