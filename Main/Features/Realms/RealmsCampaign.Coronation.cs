using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Hooks;

namespace TAOM.Features.Realms;

/// <summary>
/// A coronation (&lt;Coronations&gt;), run once when the player's companion restores a realm title: Aragorn leaves the
/// player's clan, founds the House of Telcontar under the banner of the house that ruled, takes Minas Tirith with its
/// titles and becomes Gondor's ruling clan, while the Steward keeps his office. It follows the game's own way of turning a
/// companion into a lord (CompanionRolesCampaignBehavior), except that nothing is taken from the player.
/// </summary>
public sealed partial class RealmsCampaign
{
    /// <summary>The coronation the hero's restoring of the title would start, if any.</summary>
    public CoronationConfig? CoronationFor(Hero hero, string titleId) =>
        hero.Clan == Clan.PlayerClan && hero != Hero.MainHero ? _service.CoronationFor(titleId, hero.StringId) : null;

    private bool Crown(Hero hero, CoronationConfig coronation, int influencePaid)
    {
        var kingdomId = _service.Catalog.Get(coronation.TitleId)?.KingdomId;
        var kingdom = kingdomId != null ? FindKingdom(kingdomId) : null;
        var playerClan = hero.Clan;
        if (kingdom == null || playerClan == null || kingdom.IsEliminated || hero.IsPrisoner)
            return false;

        var former = kingdom.RulingClan;
        var formerLeader = former?.Leader;
        var bannerOf = coronation.BannerOfClanId != null ? FindClan(coronation.BannerOfClanId) : null;
        bannerOf ??= former;
        // The former ruling house gives up the seat; so does the house whose banner he takes, when the player ruled.
        bool OfFormerHouse(Clan? c) => c != null && (c == former || c == bannerOf);
        var fiefs = coronation.FiefIds.Select(Settlement.Find)
            .Where(s => s != null && s.IsFortification && OfFormerHouse(s.OwnerClan)).ToList();
        var seat = fiefs.FirstOrDefault() ?? hero.HomeSettlement;

        RemoveCompanionAction.ApplyByByTurningToLord(playerClan, hero);
        hero.SetNewOccupation(Occupation.Lord);

        var clan = Clan.CreateClan(CrownedClanId(coronation));
        var name = new TextObject(coronation.ClanName);
        clan.ChangeClanName(name, coronation.InformalName != null ? new TextObject(coronation.InformalName) : name);
        clan.Culture = bannerOf?.Culture ?? hero.Culture;
        // The house's own banner (<Coronation banner>), else the one the former house is seen with: a ruling clan shows
        // its realm's banner.
        if (coronation.Banner != null)
            clan.Banner = new Banner(coronation.Banner);
        else if ((bannerOf?.Banner ?? kingdom.Banner) is { } banner)
            clan.Banner = new Banner(banner);
        clan.Kingdom = kingdom;
        RaiseTier(clan, coronation.Tier);
        if (seat != null)
            clan.SetInitialHomeSettlement(seat);
        hero.Clan = clan;
        clan.SetLeader(hero);
        clan.IsNoble = true;

        // A lawful gift: each fief's title and its village lordships move with it (OnFiefOwnerChanged).
        foreach (var fief in fiefs)
            ChangeOwnerOfSettlementAction.ApplyByGift(fief, hero);
        CampaignEventDispatcher.Instance.OnClanCreated(clan, isCompanion: true);

        LeadParty(hero, clan, seat, coronation.Troops);
        if (coronation.Gold > 0)
            GiveGoldAction.ApplyBetweenCharacters(null, hero, coronation.Gold, disableNotification: true);

        ChangeRulingClanAction.Apply(kingdom, clan);
        ApplyCrownedBanner(kingdom, clan, coronation);

        // Titles the gift did not move (held by right by someone of the former house who did not own the fief).
        var formerHouse = new System.Collections.Generic.HashSet<string>(
            new[] { former, bannerOf }.Where(OfFormerHouse).SelectMany(c => c!.Heroes).Select(h => h.StringId));
        Report(_service.SeatKing(coronation, hero.StringId, id => id != null && formerHouse.Contains(id)));

        // The Steward stays at his post under the King (he keeps it by himself; this fills it when it fell vacant).
        if (coronation.OfficeId != null && formerLeader != null && formerLeader.IsAlive
            && _service.Ledger.OfficeHolderOf(coronation.OfficeId) == null)
            Report(_service.Appoint(hero.StringId, coronation.OfficeId, formerLeader.StringId, _claimWorld));

        if (coronation.Relation > 0)
            ChangeRelationAction.ApplyPlayerRelation(hero, coronation.Relation);

        var news = new TextObject("{=realms_news_coronation}{HERO} is crowned in {SEAT}. The {CLAN} rules {KINGDOM}.");
        news.SetTextVariable("HERO", hero.Name);
        news.SetTextVariable("SEAT", seat?.Name ?? kingdom.Name);
        news.SetTextVariable("CLAN", clan.Name);
        news.SetTextVariable("KINGDOM", kingdom.Name);
        InformationManager.DisplayMessage(new InformationMessage(news.ToString(), Colors.Yellow));
        PartyNames.Refresh();
        ShowCoronation(hero, clan, kingdom, seat, coronation, influencePaid);
        return true;
    }

    /// <summary>
    /// The coronation's event window, after the confirmation and the page refresh it is started from: the story from the
    /// &lt;Coronation&gt; entry (or the placeholder below) and what actually changed.
    /// </summary>
    private void ShowCoronation(Hero hero, Clan clan, Kingdom kingdom, Settlement? seat, CoronationConfig coronation, int influencePaid)
    {
        TextObject Fill(string text)
        {
            var o = new TextObject(text);
            o.SetTextVariable("HERO", hero.Name);
            o.SetTextVariable("CLAN", clan.Name);
            o.SetTextVariable("REALM", kingdom.Name);
            o.SetTextVariable("SEAT", seat?.Name ?? kingdom.Name);
            return o;
        }

        var e = coronation.Event;
        var heading = Fill(e.Heading ?? "{=realms_coronation_heading}The Return of the King").ToString();
        var text = Fill(e.Text ?? "{=realms_coronation_text_default}{HERO} is crowned in {SEAT}, and the {CLAN} rules {REALM}.").ToString();
        var button = Fill(e.Button ?? "{=realms_coronation_button}Long live the King!").ToString();

        var lines = new System.Collections.Generic.List<string>
        {
            Fill("{=realms_coronation_line_house}{HERO} founds the {CLAN}, the ruling house of {REALM}.").ToString(),
        };

        var held = _service.Ledger.TitlesHeldBy(hero.StringId).Select(_service.Catalog.Get).Where(t => t != null).Select(t => t!)
            .OrderByDescending(t => t.Rank).ThenBy(t => t.Id, System.StringComparer.Ordinal).ToList();
        var named = held.Where(t => t.Rank != Domain.Titles.TitleRank.Lordship).Select(t => TitleNames.Of(t, this)).ToList();
        var lordships = held.Count(t => t.Rank == Domain.Titles.TitleRank.Lordship);
        if (named.Count > 0)
        {
            var titles = new TextObject(lordships > 0
                ? "{=realms_coronation_line_titles_lordships}He holds by right: {TITLES} and {COUNT} lordships."
                : "{=realms_coronation_line_titles}He holds by right: {TITLES}.");
            titles.SetTextVariable("TITLES", string.Join(", ", named));
            titles.SetTextVariable("COUNT", lordships);
            lines.Add(titles.ToString());
        }

        var office = coronation.OfficeId != null ? Config.Offices.FirstOrDefault(o => o.Id == coronation.OfficeId) : null;
        var officerId = office != null ? _service.Ledger.OfficeHolderOf(office.Id) : null;
        var officer = officerId != null ? Hero.Find(officerId) : null;
        if (office != null && officer != null)
        {
            var stays = new TextObject("{=realms_coronation_line_office}{NAME} remains {OFFICE} under the King.");
            stays.SetTextVariable("NAME", officer.Name);
            stays.SetTextVariable("OFFICE", new TextObject(office.Name));
            lines.Add(stays.ToString());
        }

        if (coronation.Relation > 0)
        {
            var relation = Fill("{=realms_coronation_line_relation}{HERO} likes you more (+{AMOUNT}).");
            relation.SetTextVariable("AMOUNT", coronation.Relation);
            lines.Add(relation.ToString());
        }

        if (influencePaid > 0)
        {
            var paid = new TextObject("{=realms_coronation_line_paid}Your clan paid {AMOUNT} influence.");
            paid.SetTextVariable("AMOUNT", influencePaid);
            lines.Add(paid.ToString());
        }

        var banner = clan.Banner ?? kingdom.Banner;
        _popups.ShowCoronation(new CoronationPopup(heading, text, e.Image, button, banner?.Serialize(), lines));
    }

    /// <summary>
    /// A house that loses the rule of a coronation's realm takes its own banner (&lt;Coronation former_banner&gt;): as ruling
    /// house it showed the realm's banner, which the crowned house keeps. Also run on load, for a save crowned before.
    /// </summary>
    private void ApplyFormerBanners(Kingdom? kingdom, Clan? formerRulingClan)
    {
        if (IsDisabled || formerRulingClan == null || formerRulingClan.Kingdom?.RulingClan == formerRulingClan)
            return;
        foreach (var coronation in Config.Coronations)
        {
            if (coronation.FormerBanner == null || coronation.BannerOfClanId != formerRulingClan.StringId)
                continue;
            var kingdomId = _service.Catalog.Get(coronation.TitleId)?.KingdomId;
            if (kingdomId == null || (kingdom != null && kingdom.StringId != kingdomId))
                continue;
            if (formerRulingClan.Banner?.Serialize() == coronation.FormerBanner)
                return;
            formerRulingClan.Banner = new Banner(coronation.FormerBanner);
            foreach (var party in formerRulingClan.WarPartyComponents.Select(w => w.MobileParty).Where(p => p?.Party != null))
                party.Party.SetVisualAsDirty();
            foreach (var settlement in formerRulingClan.Settlements.Where(s => s.Party != null))
                settlement.Party.SetVisualAsDirty();
            return;
        }
    }

    /// <summary>On load: the former house of a coronation whose crowned house rules the realm, for saves crowned before.</summary>
    private void ApplyFormerBannersOnLoad()
    {
        foreach (var coronation in Config.Coronations)
        {
            var kingdomId = coronation.FormerBanner != null && coronation.BannerOfClanId != null
                ? _service.Catalog.Get(coronation.TitleId)?.KingdomId : null;
            var kingdom = kingdomId != null ? FindKingdom(kingdomId) : null;
            if (kingdom?.RulingClan?.Leader?.StringId == coronation.HeroId)
                ApplyFormerBanners(kingdom, FindClan(coronation.BannerOfClanId!));
        }
    }

    /// <summary>The id of the house a coronation founds ("realms_clan_named_companion_aragorn").</summary>
    private static string CrownedClanId(CoronationConfig coronation) => "realms_clan_" + coronation.HeroId;

    /// <summary>
    /// The crowned house's banner (&lt;Coronation banner&gt;) on the realm as well: a ruling clan is seen with its realm's
    /// banner (Clan.Banner returns Kingdom.Banner for it), so its own alone would never show. Nothing to do without one,
    /// or when the realm already has it.
    /// </summary>
    private static void ApplyCrownedBanner(Kingdom kingdom, Clan clan, CoronationConfig coronation)
    {
        if (coronation.Banner == null || kingdom.RulingClan != clan || kingdom.Banner?.Serialize() == coronation.Banner)
            return;
        kingdom.Banner = new Banner(coronation.Banner);
        foreach (var party in clan.WarPartyComponents.Select(w => w.MobileParty).Where(p => p?.Party != null))
            party.Party.SetVisualAsDirty();
        foreach (var settlement in clan.Settlements.Where(s => s.Party != null))
            settlement.Party.SetVisualAsDirty();
    }

    /// <summary>
    /// On load: the banner of each crowned house that still rules its realm. BannerInjection sets every realm back to its
    /// banner_key when a game loads, and this runs after it (session launch follows the game load).
    /// </summary>
    private void ApplyCrownedBannersOnLoad()
    {
        foreach (var coronation in Config.Coronations.Where(c => c.Banner != null))
        {
            var kingdomId = _service.Catalog.Get(coronation.TitleId)?.KingdomId;
            var kingdom = kingdomId != null ? FindKingdom(kingdomId) : null;
            var clan = kingdom?.RulingClan;
            if (kingdom != null && clan != null && clan.StringId == CrownedClanId(coronation))
                ApplyCrownedBanner(kingdom, clan, coronation);
        }
    }

    /// <summary>Gives the clan the renown of the tier, as Clan.Tier cannot be set from outside.</summary>
    private static void RaiseTier(Clan clan, int tier)
    {
        var model = TaleWorlds.CampaignSystem.Campaign.Current?.Models?.ClanTierModel;
        if (model == null || clan.Tier >= tier)
            return;
        var renown = model.GetRequiredRenownForTier(tier);
        if (clan.Renown < renown)
            clan.AddRenown(renown - clan.Renown, shouldNotify: false);
    }

    /// <summary>As the game does for a companion made lord: out of the player's party into his own, filled with troops.</summary>
    private static void LeadParty(Hero hero, Clan clan, Settlement? seat, int troops)
    {
        if (hero.PartyBelongedTo == MobileParty.MainParty)
            MobileParty.MainParty.MemberRoster.AddToCounts(hero.CharacterObject, -1);

        var party = hero.PartyBelongedTo;
        if (party == null)
        {
            var home = seat ?? hero.HomeSettlement;
            if (home == null)
                return;
            party = LordPartyComponent.CreateLordParty(hero.CharacterObject.StringId, hero, MobileParty.MainParty.Position, 3f, home, hero);
        }
        else
        {
            party.ActualClan = clan;
            party.Party.SetVisualAsDirty();
        }

        if (party == null || party.MemberRoster.TotalManCount >= troops)
            return;
        var missing = troops - party.MemberRoster.TotalManCount;
        var basic = clan.Culture?.BasicTroop;
        var elite = clan.Culture?.EliteBasicTroop ?? basic;
        if (basic != null)
            party.MemberRoster.AddToCounts(basic, missing / 2);
        if (elite != null)
            party.MemberRoster.AddToCounts(elite, missing - missing / 2);
    }

    private static Clan? FindClan(string clanId) =>
        TaleWorlds.CampaignSystem.Campaign.Current?.CampaignObjectManager.Find<Clan>(clanId);
}
