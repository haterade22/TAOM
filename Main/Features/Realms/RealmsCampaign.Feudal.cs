using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Services;

namespace TAOM.Features.Realms;

/// <summary>
/// What titles do on the weekly tick (resentment of fiefs held without their title, a liege's pull, office
/// influence, the AI's appointments) and the player's own acts with titles: grant, revoke, form, restore, appoint and
/// dismiss. Nothing here runs per tick or per frame; the army discount reads a set built at most once a week or after a
/// title changes hands.
/// </summary>
public sealed partial class RealmsCampaign
{
    private HashSet<(string, string)>? _bonds;

    private void ForgetBonds() => _bonds = null;

    /// <summary>Whether the liege holds a title directly above one the vassal holds, in another clan (see <see cref="RealmsService.LiegeBonds"/>).</summary>
    internal bool IsLiegeOf(string liegeId, string vassalId)
    {
        if (!_service.IsInitialized || IsDisabled)
            return false;
        _bonds ??= new HashSet<(string, string)>(_service.LiegeBonds(_claimWorld, Config.Feudal));
        return _bonds.Contains((liegeId, vassalId));
    }

    /// <summary>
    /// Resentment over fiefs held without their title and the liege's pull. A few hundred lookups a week. Tribute is daily,
    /// in each clan's gold change (RealmsCampaign.Tribute.cs).
    /// </summary>
    private void WeeklyConsequences()
    {
        var config = Config.Feudal;
        if (_settings.Resentment && (config.ResentmentRelation != 0 || config.ResentmentLoyalty != 0))
            foreach (var gap in _service.TitleGaps(_claimWorld))
            {
                var holder = Hero.Find(gap.HolderId);
                var owner = Hero.Find(gap.OwnerId);
                if (holder?.IsAlive == true && owner?.IsAlive == true)
                    ChangeRelation(holder, owner, config.ResentmentRelation);
                var settlement = Settlement.Find(gap.SettlementId);
                if (config.ResentmentLoyalty != 0 && settlement?.IsTown == true && settlement.Town != null)
                    settlement.Town.Loyalty += config.ResentmentLoyalty;
            }

        if (_settings.LiegePull && config.LiegeRelation != 0)
            foreach (var (liegeId, vassalId) in _service.LiegeBonds(_claimWorld, config))
            {
                var liege = Hero.Find(liegeId);
                var vassal = Hero.Find(vassalId);
                if (liege?.IsAlive == true && vassal?.IsAlive == true && vassal.GetRelation(liege) < config.LiegeRelationCap)
                    ChangeRelation(vassal, liege, config.LiegeRelation);
            }
    }

    /// <summary>
    /// AI rulers fill their vacant offices from their realm's clan leaders, the ones they like best first; the player's
    /// realm keeps its vacancies for the player. Then each holder's clan gains the office's influence.
    /// </summary>
    private void WeeklyOffices()
    {
        if (Config.Offices.Count == 0)
            return;
        var candidates = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var kingdomId in Config.Offices.Select(o => o.KingdomId).Distinct())
        {
            var kingdom = FindKingdom(kingdomId);
            var ruler = kingdom?.Leader;
            if (kingdom == null || kingdom.IsEliminated || ruler == null || ruler == Hero.MainHero)
                continue;
            candidates[kingdomId] = kingdom.Clans
                .Where(c => c != null && !c.IsEliminated && !c.IsUnderMercenaryService && c.Leader != null && c.Leader.IsAlive)
                .Select(c => c.Leader)
                .OrderByDescending(h => ruler.GetRelation(h))
                .ThenByDescending(h => h.Clan.Renown)
                .Select(h => h.StringId)
                .ToList();
        }

        var events = _service.UpdateOffices(_claimWorld, candidates);
        var share = _settings.OfficeInfluencePercent / 100f;
        foreach (var office in Config.Offices.Where(o => o.Influence != 0 && share > 0))
        {
            var holder = _service.Ledger.OfficeHolderOf(office.Id);
            var clan = holder != null ? Hero.Find(holder)?.Clan : null;
            if (clan != null && !clan.IsEliminated)
                ChangeClanInfluenceAction.Apply(clan, office.Influence * share);
        }

        Report(events);
    }

    // ---- The player's acts. Each checks again, pays, then acts; false when it is no longer possible. ----

    public GrantQuote QuoteGrant(Hero recipient, string titleId)
    {
        var hero = Hero.MainHero;
        return hero == null || recipient?.StringId == null
            ? GrantQuote.Refused(ClaimRefusal.Recipient)
            : _service.QuoteGrant(hero.StringId, hero.Clan?.StringId, recipient.StringId, titleId, _claimWorld);
    }

    /// <summary>
    /// Heroes the player could grant a title to: the grown members and companions of the player's clan and the leaders
    /// of the other clans of the player's realm.
    /// </summary>
    public IReadOnlyList<Hero> GrantCandidates()
    {
        var hero = Hero.MainHero;
        var clan = hero?.Clan;
        if (clan == null)
            return new List<Hero>();
        var people = ClanPeople(clan).Where(h => h != hero).ToList();
        if (clan.Kingdom != null)
            people.AddRange(clan.Kingdom.Clans
                .Where(c => c != null && c != clan && !c.IsEliminated && c.Leader != null && c.Leader.IsAlive)
                .Select(c => c.Leader));
        return people;
    }

    /// <summary>The player grants the title. A town or castle given to another clan goes with its fief, as the game's gift.</summary>
    public bool PlayerGrant(Hero recipient, string titleId)
    {
        if (!MayWrite())
            return false;
        var quote = QuoteGrant(recipient, titleId);
        if (!quote.Allowed)
            return false;
        if (quote.Mode == GrantMode.ByFief)
        {
            var settlementId = _service.Catalog.Get(titleId)?.SettlementId;
            var settlement = settlementId != null ? Settlement.Find(settlementId) : null;
            if (settlement == null)
                return false;
            ChangeOwnerOfSettlementAction.ApplyByGift(settlement, recipient); // the title follows through OnSettlementOwnerChanged
        }
        else
        {
            var events = _service.Grant(Hero.MainHero.StringId, Hero.MainHero.Clan?.StringId, recipient.StringId, titleId, _claimWorld);
            if (events.Count == 0)
                return false;
            Report(events);
        }

        if (Config.Feudal.GrantRelation != 0 && recipient.IsAlive)
            ChangeRelationAction.ApplyPlayerRelation(recipient, Config.Feudal.GrantRelation);
        return true;
    }

    public ClaimQuote QuoteRevoke(string titleId)
    {
        var hero = Hero.MainHero;
        return hero == null
            ? ClaimQuote.Refused(ClaimRefusal.NotLiege)
            : _service.QuoteRevoke(hero.StringId, hero.Clan?.StringId, titleId, _claimWorld, Config.Feudal);
    }

    /// <summary>The player takes the title back from its holder, paying influence when the holder is of another clan.</summary>
    public bool PlayerRevoke(string titleId)
    {
        var hero = Hero.MainHero;
        var clan = hero?.Clan;
        if (hero == null || clan == null || !MayWrite())
            return false;
        var quote = QuoteRevoke(titleId);
        if (!quote.Allowed || clan.Influence < quote.Cost)
            return false;
        var previous = _service.Ledger.HolderOf(titleId);
        var events = _service.Revoke(hero.StringId, clan.StringId, titleId, _claimWorld, Config.Feudal);
        if (events.Count == 0)
            return false;
        if (quote.Cost > 0)
            ChangeClanInfluenceAction.Apply(clan, -quote.Cost);
        var old = previous != null ? Hero.Find(previous) : null;
        if (old != null && old.IsAlive && old != hero && Config.Feudal.RevokeRelation != 0)
            ChangeRelationAction.ApplyPlayerRelation(old, Config.Feudal.RevokeRelation);
        Report(events);
        return true;
    }

    public ClaimQuote QuoteForm(string titleId)
    {
        var hero = Hero.MainHero;
        return hero == null ? ClaimQuote.Refused(ClaimRefusal.TooFewVassals) : _service.QuoteForm(hero.StringId, titleId, _claimWorld, Config.Claims);
    }

    /// <summary>The player forms a vacant title from the lands below it, paying influence.</summary>
    public bool PlayerForm(string titleId)
    {
        var hero = Hero.MainHero;
        var clan = hero?.Clan;
        if (hero == null || clan == null || !MayWrite())
            return false;
        var quote = QuoteForm(titleId);
        if (!quote.Allowed || clan.Influence < quote.Cost)
            return false;
        var events = _service.Form(hero.StringId, titleId, _claimWorld, Config.Claims);
        if (events.Count == 0)
            return false;
        ChangeClanInfluenceAction.Apply(clan, -quote.Cost);
        Report(events);
        return true;
    }

    public ClaimQuote QuoteRestore(Hero hero, string titleId) =>
        _service.QuoteRestore(hero.StringId, titleId, _claimWorld, Config.Claims);

    /// <summary>
    /// The player, or a member or companion of the player's clan with an ancestral claim (Aragorn), restores a vacant
    /// title; the player's clan pays the influence.
    /// </summary>
    public bool PlayerRestore(Hero claimant, string titleId)
    {
        var clan = Hero.MainHero?.Clan;
        if (clan == null || claimant?.Clan != clan || !claimant.IsAlive || !MayWrite())
            return false;
        var quote = QuoteRestore(claimant, titleId);
        if (!quote.Allowed || clan.Influence < quote.Cost)
            return false;
        var coronation = CoronationFor(claimant, titleId);
        var events = _service.Restore(claimant.StringId, titleId, _claimWorld, Config.Claims);
        if (events.Count == 0)
            return false;
        ChangeClanInfluenceAction.Apply(clan, -quote.Cost);
        Report(events);
        // Aragorn taking up Gondor's crown founds his own ruling house (<Coronations>).
        if (coronation != null)
            Crown(claimant, coronation, quote.Cost);
        return true;
    }

    /// <summary>The grown, living members and companions of a clan.</summary>
    public static IEnumerable<Hero> ClanPeople(Clan clan) =>
        clan.Heroes.Concat(clan.Companions).Where(h => h != null && h.IsAlive && !h.IsChild).Distinct();

    public bool CanPlayerAppoint(OfficeConfig office) =>
        Hero.MainHero != null && _service.CanAppointTo(Hero.MainHero.StringId, office, _claimWorld);

    /// <summary>Who the player could appoint: the clan leaders and grown clan members of the office's realm who may hold it.</summary>
    public IReadOnlyList<Hero> OfficeCandidates(OfficeConfig office)
    {
        var kingdom = FindKingdom(office.KingdomId);
        if (kingdom == null)
            return new List<Hero>();
        return kingdom.Clans.Where(c => c != null && !c.IsEliminated)
            .SelectMany(c => c.Heroes.Where(h => h != null && h.IsAlive && !h.IsChild))
            .Where(h => _service.CanHoldOffice(office, h.StringId, _claimWorld))
            .ToList();
    }

    public bool PlayerAppoint(string officeId, Hero hero)
    {
        if (Hero.MainHero == null || hero?.StringId == null || !MayWrite())
            return false;
        var events = _service.Appoint(Hero.MainHero.StringId, officeId, hero.StringId, _claimWorld);
        Report(events);
        return events.Count > 0;
    }

    public bool PlayerDismiss(string officeId)
    {
        if (Hero.MainHero == null || !MayWrite())
            return false;
        var events = _service.Dismiss(Hero.MainHero.StringId, officeId, _claimWorld);
        Report(events);
        return events.Count > 0;
    }

    /// <summary>Changes how <paramref name="hero"/> and <paramref name="other"/> regard each other, quietly.</summary>
    private static void ChangeRelation(Hero hero, Hero other, int change)
    {
        if (hero == other || change == 0)
            return;
        if (hero == Hero.MainHero)
            ChangeRelationAction.ApplyPlayerRelation(other, change, false, false);
        else if (other == Hero.MainHero)
            ChangeRelationAction.ApplyPlayerRelation(hero, change, false, false);
        else
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(hero, other, change, false);
    }

    private static Kingdom? FindKingdom(string kingdomId) =>
        TaleWorlds.CampaignSystem.Campaign.Current?.CampaignObjectManager.Find<Kingdom>(kingdomId);
}
