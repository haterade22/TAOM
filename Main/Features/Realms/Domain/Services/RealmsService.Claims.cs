using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Ledger;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Features.Realms.Domain.Services;

/// <summary>
/// Claims beyond conquest: buying one on a title that borders the buyer's lands, pressing one to take the title
/// (usurping), and a claimant taking up a title that has no heir.
/// </summary>
public sealed partial class RealmsService
{
    /// <summary>
    /// Who takes up a title that has no heir: conquest claims before ancestral before fabricated ones, then the oldest
    /// claim, then hero id. Null when nobody claims it.
    /// </summary>
    public string? StrongestClaimant(string titleId) =>
        Ledger.ClaimsOn(titleId)
            .OrderBy(c => c.Kind == ClaimKind.Conquest ? 0 : c.Kind == ClaimKind.Ancestral ? 1 : 2)
            .ThenBy(c => c.CampaignDay).ThenBy(c => c.HeroId, StringComparer.Ordinal)
            .FirstOrDefault()?.HeroId;

    /// <summary>
    /// The titles that border the buyer: those of the buyer's clan's towns and castles and of their nearest others
    /// (<see cref="FiefNeighbors"/>), with every title above them. Village lordships are not counted.
    /// </summary>
    public IReadOnlyList<TitleDefinition> BorderingTitles(ClaimBuyer buyer, FiefNeighbors neighbors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<TitleDefinition>();
        foreach (var fief in buyer.FortificationIds.OrderBy(f => f, StringComparer.Ordinal))
            foreach (var settlement in new[] { fief }.Concat(neighbors.Of(fief)))
            {
                var title = Catalog.ForSettlement(settlement);
                if (title == null)
                    continue;
                foreach (var t in new[] { title }.Concat(Catalog.AncestorsOf(title.Id)))
                    if (seen.Add(t.Id))
                        result.Add(t);
            }

        return result;
    }

    /// <summary>
    /// Whether the buyer may buy a claim on the title, and its price in their special resource: the rank has a price,
    /// the buyer neither holds nor claims it, no member of their clan holds it, it is not in or held from their realm,
    /// and it borders their clan's lands (<see cref="BorderingTitles"/>).
    /// </summary>
    public ClaimQuote QuoteClaim(ClaimBuyer buyer, string titleId, IClaimWorld world, FiefNeighbors neighbors, ClaimsConfig config) =>
        QuoteClaim(buyer, Catalog.Get(titleId), world, config, () => BorderingTitles(buyer, neighbors).Any(t => t.Id == titleId));

    /// <summary>Every claim the buyer may buy now, with its price, cheapest first.</summary>
    public IReadOnlyList<(TitleDefinition Title, int Price)> ClaimOffers(ClaimBuyer buyer, IClaimWorld world, FiefNeighbors neighbors, ClaimsConfig config) =>
        BorderingTitles(buyer, neighbors)
            .Select(t => (Title: t, Quote: QuoteClaim(buyer, t, world, config, () => true)))
            .Where(o => o.Quote.Allowed)
            .OrderBy(o => o.Quote.Cost).ThenBy(o => o.Title.Id, StringComparer.Ordinal)
            .Select(o => (o.Title, o.Quote.Cost)).ToList();

    private ClaimQuote QuoteClaim(ClaimBuyer buyer, TitleDefinition? title, IClaimWorld world, ClaimsConfig config, Func<bool> borders)
    {
        var price = title != null ? config.BuyPrice(title.Rank) : null;
        if (title == null || price == null)
            return ClaimQuote.Refused(ClaimRefusal.NotForSale);
        var holder = Ledger.HolderOf(title.Id);
        if (holder == buyer.HeroId)
            return ClaimQuote.Refused(ClaimRefusal.Held);
        if (Ledger.ClaimsOn(title.Id).Any(c => c.HeroId == buyer.HeroId))
            return ClaimQuote.Refused(ClaimRefusal.Claimed);
        if (holder != null && buyer.ClanId != null && world.ClanOf(holder) == buyer.ClanId)
            return ClaimQuote.Refused(ClaimRefusal.OwnClan);
        if (InRealm(title, holder, buyer.KingdomId, world))
            return ClaimQuote.Refused(ClaimRefusal.OwnRealm);
        if (!borders())
            return ClaimQuote.Refused(ClaimRefusal.TooFar);
        return new ClaimQuote(ClaimRefusal.None, price.Value);
    }

    /// <summary>
    /// A title is in the realm when its holder serves the realm, its fief belongs to the realm now, or (a vacant title
    /// without a fief) it was made in the realm.
    /// </summary>
    private static bool InRealm(TitleDefinition title, string? holder, string? kingdomId, IClaimWorld world)
    {
        if (kingdomId == null)
            return false;
        if (holder != null && world.KingdomOfHero(holder) == kingdomId)
            return true;
        return title.SettlementId != null
            ? world.KingdomOfSettlement(title.SettlementId) == kingdomId
            : holder == null && title.KingdomId == kingdomId;
    }

    /// <summary>
    /// Records a bought (fabricated) claim when <see cref="QuoteClaim(ClaimBuyer, string, IClaimWorld, FiefNeighbors, ClaimsConfig)"/>
    /// allows it. The caller takes the price first. The event's previous holder is the title's holder.
    /// </summary>
    public IReadOnlyList<TitleEvent> BuyClaim(ClaimBuyer buyer, string titleId, double day, IClaimWorld world, FiefNeighbors neighbors, ClaimsConfig config)
    {
        if (!QuoteClaim(buyer, titleId, world, neighbors, config).Allowed
            || !Ledger.AddClaim(new TitleClaim(titleId, buyer.HeroId, ClaimKind.Fabricated, day)))
            return Array.Empty<TitleEvent>();
        return new[] { new TitleEvent(TitleEventKind.ClaimGained, TitleEventReason.Purchase, titleId, buyer.HeroId, Ledger.HolderOf(titleId)) };
    }

    /// <summary>
    /// Whether the hero may press their claim on the title and its influence cost: the rank can be usurped, the hero
    /// claims it, and their clan owns its fief (a town, castle or village title) or their side (<see cref="Allegiance"/>:
    /// themselves, their clan and their vassals) holds more than half of its vassal titles (a duchy or higher).
    /// </summary>
    public ClaimQuote QuoteUsurp(string heroId, string? clanId, string titleId, IClaimWorld world, ClaimsConfig config) =>
        QuoteUsurp(heroId, clanId, titleId, world, config, null);

    /// <param name="side">The hero's side when an offer list has it already; built here when a duchy or higher needs it.</param>
    private ClaimQuote QuoteUsurp(string heroId, string? clanId, string titleId, IClaimWorld world, ClaimsConfig config, Allegiance? side)
    {
        var title = Catalog.Get(titleId);
        var cost = title != null ? config.UsurpCost(title.Rank) : null;
        if (title == null || cost == null)
            return ClaimQuote.Refused(ClaimRefusal.NotForSale);
        var holder = Ledger.HolderOf(titleId);
        if (holder == heroId)
            return ClaimQuote.Refused(ClaimRefusal.Held);
        if (holder != null && clanId != null && world.ClanOf(holder) == clanId)
            return ClaimQuote.Refused(ClaimRefusal.OwnClan);
        if (!Ledger.ClaimsOn(titleId).Any(c => c.HeroId == heroId))
            return ClaimQuote.Refused(ClaimRefusal.NoClaim);
        if (title.SettlementId != null)
        {
            if (!OwnedByClan(title.SettlementId, heroId, clanId, world))
                return ClaimQuote.Refused(ClaimRefusal.NotOwned);
        }
        else
        {
            side ??= AllegianceOf(heroId, world);
            // A titular realm (Khazad-dûm) has no vassals of its own: its claimant needs the lands it lies over.
            var titular = TitularRealmOf(title);
            if (titular != null && !titular.Allows(side.CultureId))
                return ClaimQuote.Refused(ClaimRefusal.WrongCulture);
            var vassals = titular != null ? TitularCounties(titular) : Catalog.ChildrenOf(titleId);
            if (vassals.Count == 0 || vassals.Count(side.Counts) * 2 <= vassals.Count)
                return ClaimQuote.Refused(ClaimRefusal.TooFewVassals);
        }

        return new ClaimQuote(ClaimRefusal.None, cost.Value);
    }

    /// <summary>
    /// Takes the title by right when <see cref="QuoteUsurp"/> allows it; the caller takes the influence first. Village
    /// lordships under it that the old holder held and the usurper's clan owns go along.
    /// </summary>
    public IReadOnlyList<TitleEvent> Usurp(string heroId, string? clanId, string titleId, IClaimWorld world, ClaimsConfig config)
    {
        var events = new List<TitleEvent>();
        if (!QuoteUsurp(heroId, clanId, titleId, world, config).Allowed)
            return events;
        var previous = Ledger.HolderOf(titleId);
        Ledger.RemovePending(titleId);
        Ledger.SetHolder(titleId, heroId);
        events.Add(Changed(TitleEventReason.Usurpation, titleId, heroId, previous));
        if (previous == null || Catalog.Get(titleId)?.SettlementId == null)
            return events;

        foreach (var village in Catalog.ChildrenOf(titleId).Where(c => c.Rank == TitleRank.Lordship && c.SettlementId != null
                     && Ledger.HolderOf(c.Id) == previous && OwnedByClan(c.SettlementId!, heroId, clanId, world)))
        {
            Ledger.RemovePending(village.Id);
            Ledger.SetHolder(village.Id, heroId);
            events.Add(Changed(TitleEventReason.Usurpation, village.Id, heroId, previous));
        }

        return events;
    }

    /// <summary>The hero's claims they may press now, with their influence cost. One side serves every quote of the list.</summary>
    public IReadOnlyList<(string TitleId, int Cost)> UsurpOffers(string heroId, string? clanId, IClaimWorld world, ClaimsConfig config)
    {
        var claims = Ledger.ClaimsOf(heroId);
        if (claims.Count == 0)
            return Array.Empty<(string, int)>();
        var side = AllegianceOf(heroId, world);
        return claims
            .Select(c => (c.TitleId, Quote: QuoteUsurp(heroId, clanId, c.TitleId, world, config, side)))
            .Where(o => o.Quote.Allowed)
            .Select(o => (o.TitleId, o.Quote.Cost)).ToList();
    }

    /// <summary>
    /// The least influence any usurpation can cost; null when no rank can be usurped. Every offer of
    /// <see cref="UsurpOffers"/> costs at least this, so a clan with less need not be quoted at all.
    /// </summary>
    public static int? CheapestUsurpCost(ClaimsConfig config) =>
        Enum.GetValues(typeof(TitleRank)).Cast<TitleRank>().Select(config.UsurpCost).Min();

    private static bool OwnedByClan(string settlementId, string heroId, string? clanId, IClaimWorld world)
    {
        var owner = world.OwnerOf(settlementId);
        return owner != null && (owner == heroId || (clanId != null && world.ClanOf(owner) == clanId));
    }
}
