using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Generation;
using TAOM.Features.Realms.Domain.Laws;
using TAOM.Features.Realms.Domain.Ledger;
using TAOM.Features.Realms.Domain.Succession;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Features.Realms.Domain.Services;

/// <summary>
/// The title system's rules, driven by the campaign behavior with plain ids. Every method returns
/// the <see cref="TitleEvent"/>s it caused so the caller can report them.
/// </summary>
public sealed partial class RealmsService
{
    public TitleCatalog Catalog { get; private set; } = TitleCatalog.Empty;
    public TitleLedger Ledger { get; private set; } = new();
    public bool IsInitialized => Catalog.Count > 0;

    /// <summary>The campaign day, for chronicle entries and claims. The game sets it to the campaign clock.</summary>
    public System.Func<double> Clock { get; set; } = () => 0;

    /// <summary>The settings the rules read that no method takes as a parameter (chronicle length, ancestral claims, offices).</summary>
    public RealmsConfig Config { get; set; } = RealmsConfig.Defaults();

    public IReadOnlyList<string> Initialize(World.WorldSnapshot world, RealmsConfig config)
    {
        Config = config;
        var result = TitleGenerator.Generate(world, config);
        Load(result.Catalog, result.Ledger);
        var day = Clock();
        foreach (var title in Catalog.All)
            Ledger.Record(title.Id, new ChronicleEntry(day, TitleEventReason.Start, Ledger.HolderOf(title.Id), null), Config.Feudal.ChronicleEntries);
        return result.Warnings;
    }

    public void Load(TitleCatalog catalog, TitleLedger ledger)
    {
        Catalog = catalog;
        Ledger = ledger;
    }

    /// <summary>
    /// The title a hero is known by: the highest rank they hold by right, on a tie the first by id. Null when they hold none.
    /// </summary>
    public TitleDefinition? PrimaryTitleOf(string heroId) =>
        Ledger.TitlesHeldBy(heroId).Select(Catalog.Get).Where(t => t != null).Select(t => t!)
            .OrderByDescending(t => t.Rank).ThenBy(t => t.Id, System.StringComparer.Ordinal).FirstOrDefault();

    /// <summary>The title's own laws, else the nearest parent's, else the campaign default.</summary>
    public TitleLaws EffectiveLaws(string titleId)
    {
        var own = Ledger.LawsOn(titleId);
        if (own != null)
            return own;
        foreach (var ancestor in Catalog.AncestorsOf(titleId))
        {
            var inherited = Ledger.LawsOn(ancestor.Id);
            if (inherited != null)
                return inherited;
        }

        return Ledger.DefaultLaws;
    }

    /// <param name="world">The live campaign, so a bought claim on a vacated duchy or higher is weighed (<see cref="StrongestClaimant"/>).</param>
    public IReadOnlyList<TitleEvent> OnHeroDied(string heroId, FamilySnapshot family, IClaimWorld? world = null)
    {
        var events = new List<TitleEvent>();
        var ancestral = Ledger.ClaimsOf(heroId).Where(c => c.Kind == ClaimKind.Ancestral).Select(c => c.TitleId).ToList();
        Ledger.RemoveAllClaimsOf(heroId);
        VacateOfficesOf(heroId, events);
        var held = Ledger.TitlesHeldBy(heroId).Select(Catalog.Get).Where(t => t != null).Select(t => t!)
            .OrderByDescending(t => t.Rank).ThenBy(t => t.Id).ToList();
        var clanId = family.Get(heroId)?.ClanId;
        foreach (var title in held)
        {
            var outcome = SuccessionResolver.Resolve(heroId, family, EffectiveLaws(title.Id), title.Rank >= TitleRank.Kingdom);
            if (outcome.Route == SuccessionRoute.DeferredToClan && clanId != null)
                Ledger.AddPending(new PendingSuccession(title.Id, PendingKind.ClanLeader, clanId, heroId));
            else if (outcome.Route == SuccessionRoute.DeferredToRealm && title.KingdomId != null)
                Ledger.AddPending(new PendingSuccession(title.Id, PendingKind.RealmRuler, title.KingdomId, heroId));
            else if (outcome.HeirId != null)
            {
                Ledger.SetHolder(title.Id, outcome.HeirId);
                events.Add(Changed(TitleEventReason.Inheritance, title.Id, outcome.HeirId, heroId));
            }
            else
                VacateOrClaim(title.Id, heroId, world, events);
        }

        ancestral.AddRange(held.Where(t => t.Rank >= Config.Claims.AncestralMinRank).Select(t => t.Id));
        GiveAncestralClaims(family.Get(heroId), ancestral.Distinct().ToList(), family, events);
        return events;
    }

    /// <summary>
    /// Blood claims: each living child of the dead hero gains an ancestral claim on the given titles unless they hold it
    /// (the heir's is spent at once). Claims pass through mothers as through fathers, so a daughter married into another
    /// house carries her father's claims to her children.
    /// </summary>
    private void GiveAncestralClaims(HeroInfo? deceased, IReadOnlyList<string> titleIds, FamilySnapshot family, List<TitleEvent> events)
    {
        if (deceased == null || titleIds.Count == 0)
            return;
        var day = Clock();
        foreach (var child in deceased.ChildIds.Select(family.Get).Where(c => c is { IsAlive: true }).Select(c => c!))
            foreach (var titleId in titleIds)
                if (Catalog.Get(titleId) != null && Ledger.AddClaim(new TitleClaim(titleId, child.Id, ClaimKind.Ancestral, day)))
                    events.Add(new TitleEvent(TitleEventKind.ClaimGained, TitleEventReason.Ancestry, titleId, child.Id, Ledger.HolderOf(titleId)));
    }

    /// <summary>A title without an heir goes to its strongest claimant (<see cref="StrongestClaimant"/>), else falls vacant.</summary>
    private void VacateOrClaim(string titleId, string? previousHolderId, IClaimWorld? world, List<TitleEvent> events)
    {
        var claimant = StrongestClaimant(titleId, world);
        Ledger.SetHolder(titleId, claimant);
        events.Add(Changed(claimant == null ? TitleEventReason.Vacated : TitleEventReason.Claim, titleId, claimant, previousHolderId));
    }

    /// <summary>Hands waiting titles to the clan leader or ruler the game has since settled on.</summary>
    /// <param name="world">The live campaign, so a bought claim on a vacated duchy or higher is weighed (<see cref="StrongestClaimant"/>).</param>
    public IReadOnlyList<TitleEvent> ResolvePending(IRealmLeaders leaders, IClaimWorld? world = null)
    {
        var events = new List<TitleEvent>();
        foreach (var pending in Ledger.Pending)
        {
            var isClan = pending.Kind == PendingKind.ClanLeader;
            var heir = isClan ? leaders.ClanLeaderOf(pending.ReferenceId) : leaders.RulerOf(pending.ReferenceId);
            var gone = isClan ? !leaders.ClanExists(pending.ReferenceId) : !leaders.KingdomExists(pending.ReferenceId);
            if (heir == pending.PreviousHolderId)
                heir = null;
            if (heir == null && !gone)
                continue;

            Ledger.RemovePending(pending.TitleId);
            if (heir == null)
            {
                VacateOrClaim(pending.TitleId, pending.PreviousHolderId, world, events);
                continue;
            }

            Ledger.SetHolder(pending.TitleId, heir);
            events.Add(Changed(isClan ? TitleEventReason.ClanSuccession : TitleEventReason.RealmSuccession, pending.TitleId, heir, pending.PreviousHolderId));
        }

        return events;
    }

    /// <summary>A new ruler of an elective realm takes up the realm's titles that follow its rulers.</summary>
    public IReadOnlyList<TitleEvent> OnRealmRulerChanged(string kingdomId, string? newRulerId)
    {
        var events = new List<TitleEvent>();
        if (newRulerId == null)
            return events;
        // A vacant throne is not taken up by whoever rules: Gondor's Stewards never become its kings.
        var realmTitles = Catalog.All.Where(t => t.Rank == TitleRank.Kingdom && t.KingdomId == kingdomId
                                                 && !(IsRestoreOnly(t) && Ledger.HolderOf(t.Id) == null));
        foreach (var title in realmTitles.Where(t => EffectiveLaws(t.Id).Succession == SuccessionLaw.Elective))
        {
            var previous = Ledger.HolderOf(title.Id);
            if (previous == newRulerId)
                continue;
            Ledger.RemovePending(title.Id);
            Ledger.SetHolder(title.Id, newRulerId);
            events.Add(Changed(TitleEventReason.RealmSuccession, title.Id, newRulerId, previous));
        }

        return events;
    }

    /// <summary>
    /// Clan leaders hand lesser titles to their grown children, so a lord's sons hold land by right although the game
    /// gives every fief to the clan leader. Each leader keeps their primary title, their seat (the capital in
    /// <paramref name="capitals"/> of a realm they hold, else the title of <see cref="ClanHead.SeatSettlementId"/>, else
    /// their highest county or barony) and the seat's lieges they hold.
    /// Their other duchies, counties and baronies are grants, each with the titles below it the leader holds (lordships
    /// included). Recipients are the leader's living children of <see cref="SuccessionResolver.AdultAge"/> or more, in
    /// the leader's clan, who hold no title yet, in the order and under the gender law of the leader's primary title.
    /// The heir (first in line of all grown children in the clan) takes the highest grant inside the seat's duchy, else
    /// the highest grant; each other child the highest left (rank, then id). Kingdoms and high titles are never granted. Running it again changes nothing until a
    /// child comes of age or loses every title.
    /// </summary>
    public IReadOnlyList<TitleEvent> GrantToChildren(IEnumerable<ClanHead> heads, FamilySnapshot family,
        IReadOnlyDictionary<string, string>? capitals = null)
    {
        var events = new List<TitleEvent>();
        foreach (var head in heads.OrderBy(h => h.LeaderId, System.StringComparer.Ordinal))
            GrantFrom(head, family, capitals, events);
        return events;
    }

    private void GrantFrom(ClanHead head, FamilySnapshot family, IReadOnlyDictionary<string, string>? capitals, List<TitleEvent> events)
    {
        var leader = family.Get(head.LeaderId);
        var primary = leader is { IsAlive: true } ? PrimaryTitleOf(leader.Id) : null;
        if (leader == null || primary == null)
            return;

        var laws = EffectiveLaws(primary.Id);
        // In line order; the first is the heir even when they already hold a title, so a younger child who comes of age
        // later is not given the heir's grant.
        var children = SuccessionResolver.Order(
                leader.ChildIds.Select(family.Get).Where(c => c != null).Select(c => c!)
                    .Where(c => c.IsAlive && c.Age >= SuccessionResolver.AdultAge && c.ClanId != null && c.ClanId == leader.ClanId),
                laws.Gender, eldestFirst: laws.Succession != SuccessionLaw.Ultimogeniture)
            .ToList();
        if (!children.Any(c => Ledger.TitlesHeldBy(c.Id).Count == 0))
            return;

        var held = Ledger.TitlesHeldBy(leader.Id).Select(Catalog.Get).Where(t => t != null).Select(t => t!).ToList();
        var seat = SeatOf(head, held, capitals);
        var kept = new HashSet<string>(System.StringComparer.Ordinal) { primary.Id };
        if (seat != null)
        {
            kept.Add(seat.Id);
            foreach (var liege in Catalog.AncestorsOf(seat.Id).Where(t => Ledger.HolderOf(t.Id) == leader.Id))
                kept.Add(liege.Id);
        }

        bool Grantable(TitleDefinition t) =>
            t.Rank >= TitleRank.Barony && t.Rank <= TitleRank.Duchy && !kept.Contains(t.Id) && Ledger.HolderOf(t.Id) == leader.Id;

        // A grant is a grantable title with no grantable title above it; whatever the leader holds below it goes along.
        var grants = held.Where(t => Grantable(t) && !Catalog.AncestorsOf(t.Id).Any(Grantable))
            .OrderByDescending(t => t.Rank).ThenBy(t => t.Id, System.StringComparer.Ordinal).ToList();
        var seatDuchy = seat == null ? null
            : seat.Rank == TitleRank.Duchy ? seat : Catalog.AncestorsOf(seat.Id).FirstOrDefault(t => t.Rank == TitleRank.Duchy);

        for (var i = 0; i < children.Count && grants.Count > 0; i++)
        {
            if (Ledger.TitlesHeldBy(children[i].Id).Count > 0)
                continue;
            var grant = i == 0 && seatDuchy != null
                ? grants.FirstOrDefault(t => Catalog.AncestorsOf(t.Id).Any(a => a.Id == seatDuchy.Id)) ?? grants[0]
                : grants[0];
            grants.Remove(grant);
            Give(grant, children[i].Id, leader.Id, events);
        }
    }

    /// <summary>
    /// The leader's seat, the first they hold of: the capital of a realm whose title they hold, the clan's home
    /// settlement, else their highest county or barony.
    /// </summary>
    private TitleDefinition? SeatOf(ClanHead head, IReadOnlyList<TitleDefinition> held, IReadOnlyDictionary<string, string>? capitals)
    {
        bool IsSeat(TitleDefinition? t) => t != null && t.Rank >= TitleRank.Barony && t.Rank <= TitleRank.County && Ledger.HolderOf(t.Id) == head.LeaderId;

        if (capitals != null)
        {
            var realms = held.Where(t => t.Rank == TitleRank.Kingdom && t.KingdomId != null).OrderBy(t => t.Id, System.StringComparer.Ordinal)
                .Select(t => t.KingdomId!);
            if (head.RuledKingdomId != null)
                realms = new[] { head.RuledKingdomId }.Concat(realms);
            foreach (var realm in realms)
            {
                var capital = capitals.TryGetValue(realm, out var settlement) ? Catalog.ForSettlement(settlement) : null;
                if (IsSeat(capital))
                    return capital;
            }
        }

        var home = Catalog.ForSettlement(head.SeatSettlementId);
        if (IsSeat(home))
            return home;
        return held.Where(t => t.Rank == TitleRank.County || t.Rank == TitleRank.Barony)
            .OrderByDescending(t => t.Rank).ThenBy(t => t.Id, System.StringComparer.Ordinal).FirstOrDefault();
    }

    /// <summary>Gives the title and every title below it that the giver holds.</summary>
    private void Give(TitleDefinition title, string heroId, string giverId, List<TitleEvent> events)
    {
        if (Ledger.HolderOf(title.Id) != giverId)
            return;
        Ledger.RemovePending(title.Id);
        Ledger.SetHolder(title.Id, heroId);
        events.Add(Changed(TitleEventReason.Grant, title.Id, heroId, giverId));
        foreach (var child in Catalog.ChildrenOf(title.Id))
            Give(child, heroId, giverId, events);
    }

    /// <summary>
    /// A settlement changed owner. <paramref name="oldOwnerClanIds"/> are the members of the old owner's clan: a lawful
    /// transfer also moves a title that one of them held (a fief granted to the leader's son leaves with the fief).
    /// </summary>
    public IReadOnlyList<TitleEvent> OnFiefOwnerChanged(string settlementId, string? newOwnerId, string? oldOwnerId, TransferKind kind, double day,
        IReadOnlyCollection<string>? oldOwnerClanIds = null)
    {
        var events = new List<TitleEvent>();
        var title = Catalog.ForSettlement(settlementId);
        if (title == null || newOwnerId == null)
            return events;

        bool HeldByGiver(string? holder) =>
            holder != null && (holder == oldOwnerId || (oldOwnerClanIds != null && oldOwnerClanIds.Contains(holder)));

        ApplyOwnerChange(title, newOwnerId, kind, day, HeldByGiver, events);

        // Lordships of the settlement's villages move with a lawful transfer; the game may or may not raise their own events.
        if (kind == TransferKind.Transfer)
            foreach (var village in Catalog.ChildrenOf(title.Id).Where(c => c.Rank == TitleRank.Lordship && HeldByGiver(Ledger.HolderOf(c.Id))))
                ApplyOwnerChange(village, newOwnerId, kind, day, HeldByGiver, events);
        return events;
    }

    private void ApplyOwnerChange(TitleDefinition title, string newOwnerId, TransferKind kind, double day,
        System.Func<string?, bool> heldByGiver, List<TitleEvent> events)
    {
        var holder = Ledger.HolderOf(title.Id);
        if (holder == newOwnerId)
            return;

        if (holder == null || (kind == TransferKind.Transfer && heldByGiver(holder)))
        {
            Ledger.RemovePending(title.Id);
            Ledger.SetHolder(title.Id, newOwnerId);
            events.Add(Changed(holder == null ? TitleEventReason.Possession : TitleEventReason.Transfer, title.Id, newOwnerId, holder));
        }
        else if (kind == TransferKind.Conquest && Ledger.AddClaim(new TitleClaim(title.Id, newOwnerId, ClaimKind.Conquest, day)))
            events.Add(new TitleEvent(TitleEventKind.ClaimGained, TitleEventReason.Conquest, title.Id, newOwnerId, holder));
    }

    /// <summary>A change of holder, written into the title's chronicle.</summary>
    private TitleEvent Changed(TitleEventReason reason, string titleId, string? heroId, string? previous)
    {
        Ledger.Record(titleId, new ChronicleEntry(Clock(), reason, heroId, previous), Config.Feudal.ChronicleEntries);
        return new TitleEvent(TitleEventKind.HolderChanged, reason, titleId, heroId, previous);
    }
}
