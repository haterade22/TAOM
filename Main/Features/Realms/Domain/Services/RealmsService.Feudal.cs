using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Ledger;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Features.Realms.Domain.Services;

/// <summary>
/// A lord's own acts with titles: granting one to a vassal, revoking one, forming a vacant title from the lands below it,
/// and restoring a vacant title by ancestral right.
/// </summary>
public sealed partial class RealmsService
{
    /// <summary>
    /// Whether the giver may grant the title to the recipient, and how it passes. Lordships with their town or castle,
    /// baronies, counties and duchies the giver holds by right can be granted: to a member of the giver's clan with the
    /// titles below it; to a lord of another clan in the giver's realm, a duchy as the title alone, a town or castle with
    /// its fief (the giver must own it). Realms and high titles never are.
    /// </summary>
    public GrantQuote QuoteGrant(string giverId, string? giverClanId, string recipientId, string titleId, IClaimWorld world)
    {
        var title = Catalog.Get(titleId);
        if (title == null || title.Rank > TitleRank.Duchy)
            return GrantQuote.Refused(ClaimRefusal.NotGrantable);
        if (Ledger.HolderOf(titleId) != giverId || Ledger.IsPending(titleId))
            return GrantQuote.Refused(ClaimRefusal.NotHolder);
        if (recipientId == giverId)
            return GrantQuote.Refused(ClaimRefusal.Recipient);
        if (giverClanId != null && world.ClanOf(recipientId) == giverClanId)
            return new GrantQuote(ClaimRefusal.None, GrantMode.WithVassals);

        var realm = world.KingdomOfHero(giverId);
        if (realm == null || world.KingdomOfHero(recipientId) != realm)
            return GrantQuote.Refused(ClaimRefusal.Recipient);
        if (title.SettlementId == null)
            return new GrantQuote(ClaimRefusal.None, GrantMode.TitleOnly);
        if (title.Rank == TitleRank.Lordship)
            return GrantQuote.Refused(ClaimRefusal.NotGrantable);
        return world.OwnerOf(title.SettlementId) == giverId
            ? new GrantQuote(ClaimRefusal.None, GrantMode.ByFief)
            : GrantQuote.Refused(ClaimRefusal.NotOwned);
    }

    /// <summary>
    /// Grants the title when <see cref="QuoteGrant"/> allows it as <see cref="GrantMode.WithVassals"/> or
    /// <see cref="GrantMode.TitleOnly"/>. A <see cref="GrantMode.ByFief"/> grant is the game's fief gift, which moves the
    /// title through <see cref="OnFiefOwnerChanged"/>; this does nothing for it.
    /// </summary>
    public IReadOnlyList<TitleEvent> Grant(string giverId, string? giverClanId, string recipientId, string titleId, IClaimWorld world)
    {
        var events = new List<TitleEvent>();
        var quote = QuoteGrant(giverId, giverClanId, recipientId, titleId, world);
        var title = Catalog.Get(titleId);
        if (!quote.Allowed || title == null)
            return events;
        if (quote.Mode == GrantMode.WithVassals)
            Give(title, recipientId, giverId, events);
        else if (quote.Mode == GrantMode.TitleOnly)
        {
            Ledger.SetHolder(titleId, recipientId);
            events.Add(Changed(TitleEventReason.Grant, titleId, recipientId, giverId));
        }

        return events;
    }

    /// <summary>
    /// Whether the hero may take the title back from its holder, and its influence cost: the revoker holds a title above
    /// it, or the holder is in the revoker's clan (then it is free). A holder of another clan must serve the revoker's realm.
    /// </summary>
    public ClaimQuote QuoteRevoke(string revokerId, string? revokerClanId, string titleId, IClaimWorld world, FeudalConfig config)
    {
        var title = Catalog.Get(titleId);
        var cost = title != null ? config.RevokeCost(title.Rank) : null;
        if (title == null || cost == null)
            return ClaimQuote.Refused(ClaimRefusal.NotForSale);
        var holder = Ledger.HolderOf(titleId);
        if (holder == null || Ledger.IsPending(titleId))
            return ClaimQuote.Refused(ClaimRefusal.Vacant);
        if (holder == revokerId)
            return ClaimQuote.Refused(ClaimRefusal.Held);
        if (revokerClanId != null && world.ClanOf(holder) == revokerClanId)
            return new ClaimQuote(ClaimRefusal.None, 0);
        if (!Catalog.AncestorsOf(titleId).Any(a => Ledger.HolderOf(a.Id) == revokerId))
            return ClaimQuote.Refused(ClaimRefusal.NotLiege);
        var realm = world.KingdomOfHero(revokerId);
        if (realm == null || world.KingdomOfHero(holder) != realm)
            return ClaimQuote.Refused(ClaimRefusal.OutsideRealm);
        return new ClaimQuote(ClaimRefusal.None, cost.Value);
    }

    /// <summary>
    /// Takes the title back when <see cref="QuoteRevoke"/> allows it; the caller takes the influence first. The village
    /// lordships under a town or castle title that the old holder held come along. The fief itself is not taken.
    /// </summary>
    public IReadOnlyList<TitleEvent> Revoke(string revokerId, string? revokerClanId, string titleId, IClaimWorld world, FeudalConfig config)
    {
        var events = new List<TitleEvent>();
        if (!QuoteRevoke(revokerId, revokerClanId, titleId, world, config).Allowed)
            return events;
        var previous = Ledger.HolderOf(titleId);
        Ledger.SetHolder(titleId, revokerId);
        events.Add(Changed(TitleEventReason.Revocation, titleId, revokerId, previous));
        if (Catalog.Get(titleId)?.SettlementId == null)
            return events;
        foreach (var village in Catalog.ChildrenOf(titleId).Where(c => c.Rank == TitleRank.Lordship && Ledger.HolderOf(c.Id) == previous))
        {
            Ledger.RemovePending(village.Id);
            Ledger.SetHolder(village.Id, revokerId);
            events.Add(Changed(TitleEventReason.Revocation, village.Id, revokerId, previous));
        }

        return events;
    }

    /// <summary>
    /// Whether the hero may form a vacant title of duchy rank or higher, and its influence cost (the usurp cost of its
    /// rank): a duchy or realm needs more than half of the counties below it (baronies when it has none) held by the
    /// hero's side (<see cref="Allegiance"/>); a high title or union more than half of its direct vassal titles. A vacant
    /// throne (<c>throne="vacant"</c>) is only restored (<see cref="QuoteRestore"/>).
    /// </summary>
    public ClaimQuote QuoteForm(string heroId, string titleId, IClaimWorld world, ClaimsConfig config) =>
        QuoteForm(heroId, titleId, config, AllegianceOf(heroId, world));

    private ClaimQuote QuoteForm(string heroId, string titleId, ClaimsConfig config, Allegiance side)
    {
        var title = Catalog.Get(titleId);
        var cost = title != null && title.Rank >= TitleRank.Duchy ? config.UsurpCost(title.Rank) : null;
        if (title == null || cost == null)
            return ClaimQuote.Refused(ClaimRefusal.NotForSale);
        if (Ledger.HolderOf(titleId) != null || Ledger.IsPending(titleId))
            return ClaimQuote.Refused(ClaimRefusal.NotVacant);
        if (IsRestoreOnly(title))
            return ClaimQuote.Refused(ClaimRefusal.RestoreOnly);
        IReadOnlyList<TitleDefinition> lands;
        if (title.Rank >= TitleRank.Empire)
            lands = Catalog.ChildrenOf(titleId);
        else
        {
            var below = DescendantsOf(titleId).ToList();
            lands = below.Where(t => t.Rank == TitleRank.County).ToList();
            if (lands.Count == 0)
                lands = below.Where(t => t.Rank == TitleRank.Barony).ToList();
        }

        if (lands.Count == 0 || lands.Count(side.Counts) * 2 <= lands.Count)
            return ClaimQuote.Refused(ClaimRefusal.TooFewVassals);
        return new ClaimQuote(ClaimRefusal.None, cost.Value);
    }

    /// <summary>Forms the title when <see cref="QuoteForm(string, string, IClaimWorld, ClaimsConfig)"/> allows it; the caller takes the influence first.</summary>
    public IReadOnlyList<TitleEvent> Form(string heroId, string titleId, IClaimWorld world, ClaimsConfig config)
    {
        if (!QuoteForm(heroId, titleId, world, config).Allowed)
            return Array.Empty<TitleEvent>();
        Ledger.SetHolder(titleId, heroId);
        return new[] { Changed(TitleEventReason.Formation, titleId, heroId, null) };
    }

    /// <summary>
    /// Whether the hero may restore a vacant title by ancestral right, and its influence cost (the usurp cost of its
    /// rank): the hero has an ancestral claim on it and serves its realm (for a high title or union, one of the realms
    /// below it). No lands are needed: Aragorn takes up Gondor's crown as Isildur's heir.
    /// </summary>
    public ClaimQuote QuoteRestore(string heroId, string titleId, IClaimWorld world, ClaimsConfig config)
    {
        var title = Catalog.Get(titleId);
        // A coronation has its own price (Aragorn taking up Gondor's crown); any other restore costs as usurping.
        var cost = title != null ? CoronationFor(titleId, heroId)?.Influence ?? config.UsurpCost(title.Rank) : null;
        if (title == null || cost == null)
            return ClaimQuote.Refused(ClaimRefusal.NotForSale);
        if (Ledger.HolderOf(titleId) != null || Ledger.IsPending(titleId))
            return ClaimQuote.Refused(ClaimRefusal.NotVacant);
        if (!Ledger.ClaimsOn(titleId).Any(c => c.HeroId == heroId && c.Kind == ClaimKind.Ancestral))
            return ClaimQuote.Refused(ClaimRefusal.NoClaim);
        var realm = world.KingdomOfHero(heroId);
        if (realm == null || !RealmsOf(title).Contains(realm))
            return ClaimQuote.Refused(ClaimRefusal.NotServing);
        return new ClaimQuote(ClaimRefusal.None, cost.Value);
    }

    /// <summary>Restores the title when <see cref="QuoteRestore"/> allows it; the caller takes the influence first.</summary>
    public IReadOnlyList<TitleEvent> Restore(string heroId, string titleId, IClaimWorld world, ClaimsConfig config)
    {
        if (!QuoteRestore(heroId, titleId, world, config).Allowed)
            return Array.Empty<TitleEvent>();
        Ledger.SetHolder(titleId, heroId);
        return new[] { Changed(TitleEventReason.Restoration, titleId, heroId, null) };
    }

    /// <summary>The vacant titles the hero may form or restore now, with their cost.</summary>
    public IReadOnlyList<(string TitleId, int Cost, bool Restore)> FormOffers(string heroId, IClaimWorld world, ClaimsConfig config)
    {
        var offers = new List<(string, int, bool)>();
        var side = AllegianceOf(heroId, world);
        foreach (var title in Catalog.All.Where(t => t.Rank >= TitleRank.Duchy && Ledger.HolderOf(t.Id) == null))
        {
            var restore = QuoteRestore(heroId, title.Id, world, config);
            if (restore.Allowed)
            {
                offers.Add((title.Id, restore.Cost, true));
                continue;
            }

            var form = QuoteForm(heroId, title.Id, config, side);
            if (form.Allowed)
                offers.Add((title.Id, form.Cost, false));
        }

        return offers;
    }

    /// <summary>A realm title of a <c>throne="vacant"</c> kingdom: it is only ever restored by an ancestral claimant.</summary>
    public bool IsRestoreOnly(TitleDefinition title) =>
        title.Rank == TitleRank.Kingdom && title.KingdomId != null && Config.VacantThrones.Contains(title.KingdomId);

    /// <summary>The realms a title belongs to: its own, or for a title without one the realms of the realm titles below it.</summary>
    private HashSet<string> RealmsOf(TitleDefinition title)
    {
        var realms = new HashSet<string>(StringComparer.Ordinal);
        if (title.KingdomId != null)
            realms.Add(title.KingdomId);
        else
            foreach (var below in DescendantsOf(title.Id).Where(t => t.Rank == TitleRank.Kingdom && t.KingdomId != null))
                realms.Add(below.KingdomId!);
        return realms;
    }

    /// <summary>Every title below this one, depth first.</summary>
    public IEnumerable<TitleDefinition> DescendantsOf(string titleId)
    {
        foreach (var child in Catalog.ChildrenOf(titleId))
        {
            yield return child;
            foreach (var below in DescendantsOf(child.Id))
                yield return below;
        }
    }

    /// <summary>
    /// Brings a save written by an older format up to date: version 1 saves record Gondor's throne as held by its ruling
    /// Steward, so a <c>throne="vacant"</c> realm title held by the realm's current ruler falls vacant.
    /// </summary>
    public IReadOnlyList<TitleEvent> Migrate(int savedVersion, IRealmLeaders leaders)
    {
        var events = new List<TitleEvent>();
        if (savedVersion >= 2)
            return events;
        foreach (var kingdomId in Config.VacantThrones.OrderBy(k => k, StringComparer.Ordinal))
        {
            var titleId = Generation.TitleGenerator.KingdomPrefix + kingdomId;
            var holder = Ledger.HolderOf(titleId);
            if (Catalog.Get(titleId) == null || holder == null || holder != leaders.RulerOf(kingdomId))
                continue;
            Ledger.SetHolder(titleId, null);
            events.Add(Changed(TitleEventReason.Vacated, titleId, null, holder));
        }

        return events;
    }

    /// <summary>
    /// Adds the unions in the config that this campaign's titles do not have yet (a save made before they existed),
    /// putting their member titles under them. Returns how many were added.
    /// </summary>
    public int AddMissingUnions(List<string> warnings)
    {
        var definitions = Generation.TitleGenerator.WithUnions(Catalog.All, Ledger, Config, warnings);
        var added = definitions.Count - Catalog.Count;
        if (added <= 0)
            return 0;
        var known = new HashSet<string>(Catalog.All.Select(t => t.Id), StringComparer.Ordinal);
        Catalog = new TitleCatalog(definitions);
        warnings.AddRange(Catalog.Problems);
        foreach (var union in definitions.Where(d => !known.Contains(d.Id)))
            Ledger.Record(union.Id, new ChronicleEntry(Clock(), TitleEventReason.Start, Ledger.HolderOf(union.Id), null), Config.Feudal.ChronicleEntries);
        return added;
    }

    /// <summary>
    /// Gives the configured starting claims (Aragorn's on Gondor and Arnor) to heroes who are alive and neither hold nor
    /// claim the title. Safe to run on every load. Returns the claims added.
    /// </summary>
    public int ApplyStartingClaims(Func<string, bool> isAlive)
    {
        var added = 0;
        foreach (var claim in Config.StartingClaims)
            if (Catalog.Get(claim.TitleId) != null && isAlive(claim.HeroId)
                && Ledger.AddClaim(new TitleClaim(claim.TitleId, claim.HeroId, claim.Kind, Clock())))
                added++;
        return added;
    }
}
