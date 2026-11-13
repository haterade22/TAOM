using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.Realms;
using TAOM.Features.Realms.Domain.Services;
using TAOM.Features.Realms.Domain.Titles;
using static TAOM.Client.Features.Realms.ClaimActions;

namespace TAOM.Client.Features.Realms;

/// <summary>
/// The player's acts with a title, next to the claim actions: grant it to a vassal, revoke it, form or restore it, and on
/// a realm's title appoint or dismiss its officers. As with claims, an act that does not apply to the title is left out
/// and a refused one carries its reason.
/// </summary>
internal static class FeudalActions
{
    public static List<ClaimOption> For(RealmsCampaign b, TitleDefinition title, Action? afterYes, Action? afterNo)
    {
        var options = new List<ClaimOption>();
        var hero = Hero.MainHero;
        var clan = hero?.Clan;
        if (hero == null || clan == null || !hero.IsAlive)
            return options;
        var name = TitleNames.Of(title, b);
        var ledger = b.Service.Ledger;

        // Grant: a title the player holds by right, to a member of their clan or a lord of their realm.
        if (ledger.HolderOf(title.Id) == hero.StringId && title.Rank <= TitleRank.Duchy && !ledger.IsPending(title.Id))
        {
            var recipients = b.GrantCandidates()
                .Select(h => (Hero: h, Quote: b.QuoteGrant(h, title.Id)))
                .Where(r => r.Quote.Allowed)
                .ToList();
            var reason = recipients.Count > 0 ? string.Empty : Text(title.Rank == TitleRank.Lordship
                ? "{=realms_ui_grant_village}A village passes to another clan only with its town or castle."
                : "{=realms_ui_grant_none}No one can receive it.");
            options.Add(new ClaimOption(Text("{=realms_ui_grant}Grant"), string.Empty, reason, () => Pick(
                Text("{=realms_ui_grant}Grant"),
                Ask("{=realms_ui_grant_pick}Who receives the {TITLE}?", name, string.Empty),
                recipients.Select(r => (r.Hero, GrantHint(r.Quote.Mode))).ToList(),
                recipient => Confirm(
                    Text("{=realms_ui_grant}Grant"),
                    GrantQuestion(name, recipient, b.QuoteGrant(recipient, title.Id).Mode),
                    () => { if (!b.PlayerGrant(recipient, title.Id)) Message(Failed()); },
                    afterYes, afterNo),
                afterNo)));
        }

        // Revoke: from a vassal below a title the player holds, or from a member of the player's clan (free).
        var revoke = b.QuoteRevoke(title.Id);
        if (revoke.Refusal is not (ClaimRefusal.NotForSale or ClaimRefusal.Vacant or ClaimRefusal.Held or ClaimRefusal.NotLiege))
        {
            var cost = !revoke.Allowed ? string.Empty
                : revoke.Cost > 0 ? Influence(revoke.Cost) : Text("{=realms_ui_free}no influence");
            options.Add(new ClaimOption(Text("{=realms_ui_revoke}Revoke"), cost, InfluenceReason(clan, revoke), () => Confirm(
                Text("{=realms_ui_revoke}Revoke"),
                Ask("{=realms_ui_revoke_ask}Take the {TITLE} back from its holder for {COST}? They will not forget it.", name, cost),
                () => { if (!b.PlayerRevoke(title.Id)) Message(Failed()); },
                afterYes, afterNo)));
        }

        // Restore: the player or anyone of their clan (Aragorn) with an ancestral claim on a vacant title.
        var restorers = new[] { hero }.Concat(RealmsCampaign.ClanPeople(clan).Where(h => h != hero))
            .Select(h => (Hero: h, Quote: b.QuoteRestore(h, title.Id)))
            .Where(r => r.Quote.Refusal is not (ClaimRefusal.NotForSale or ClaimRefusal.NotVacant or ClaimRefusal.NoClaim))
            .ToList();
        foreach (var (claimant, quote) in restorers)
        {
            var cost = quote.Allowed ? Influence(quote.Cost) : string.Empty;
            string label;
            if (claimant == hero)
                label = Text("{=realms_ui_restore}Restore");
            else
            {
                var text = new TextObject("{=realms_ui_restore_for}Restore for {HERO}");
                text.SetTextVariable("HERO", claimant.Name);
                label = text.ToString();
            }

            var ask = new TextObject("{=realms_ui_restore_ask}Spend {COST} so that {HERO} takes up the {TITLE} by right of blood?");
            ask.SetTextVariable("COST", cost);
            ask.SetTextVariable("HERO", claimant.Name);
            ask.SetTextVariable("TITLE", name);
            var question = ask.ToString();
            var coronation = b.CoronationFor(claimant, title.Id);
            if (coronation != null)
                question += "\n\n" + CoronationWarning(claimant, title, coronation);
            options.Add(new ClaimOption(label, cost, InfluenceReason(clan, quote), () => Confirm(
                Text("{=realms_ui_restore}Restore"), question,
                () => { if (!b.PlayerRestore(claimant, title.Id)) Message(Failed()); },
                afterYes, afterNo)));
        }

        // Form: a vacant duchy, realm or high title, from most of the lands below it.
        var form = b.QuoteForm(title.Id);
        if (form.Refusal is not (ClaimRefusal.NotForSale or ClaimRefusal.NotVacant)
            && !(form.Refusal == ClaimRefusal.RestoreOnly && restorers.Count > 0))
        {
            var cost = form.Allowed ? Influence(form.Cost) : string.Empty;
            options.Add(new ClaimOption(Text("{=realms_ui_form}Form"), cost, InfluenceReason(clan, form), () => Confirm(
                Text("{=realms_ui_form}Form"),
                Ask("{=realms_ui_form_ask}Form the {TITLE} for {COST}?", name, cost),
                () => { if (!b.PlayerForm(title.Id)) Message(Failed()); },
                afterYes, afterNo)));
        }

        if (title.Rank == TitleRank.Kingdom && title.KingdomId != null)
            AddOfficeActions(b, title.KingdomId, options, afterYes, afterNo);
        return options;
    }

    /// <summary>A ruler appoints and dismisses the officers of their realm.</summary>
    private static void AddOfficeActions(RealmsCampaign b, string kingdomId, List<ClaimOption> options, Action? afterYes, Action? afterNo)
    {
        foreach (var office in b.Service.OfficesOfRealm(kingdomId).Where(b.CanPlayerAppoint))
        {
            var officeName = new TextObject(office.Name).ToString();
            var candidates = b.OfficeCandidates(office);
            var appoint = new TextObject("{=realms_ui_appoint}Appoint the {OFFICE}");
            appoint.SetTextVariable("OFFICE", officeName);
            options.Add(new ClaimOption(appoint.ToString(), string.Empty,
                candidates.Count > 0 ? string.Empty : Text("{=realms_ui_appoint_none}No lord of your realm can take it up."),
                () => Pick(appoint.ToString(), Ask("{=realms_ui_appoint_pick}Who will be the {TITLE}?", officeName, string.Empty),
                    candidates.Select(h => (h, string.Empty)).ToList(),
                    chosen => { if (!b.PlayerAppoint(office.Id, chosen)) Message(Failed()); UiQueue.Post(afterYes); },
                    afterNo)));

            var holder = b.Service.Ledger.OfficeHolderOf(office.Id);
            if (holder == null)
                continue;
            var dismiss = new TextObject("{=realms_ui_dismiss}Dismiss {HERO} as {OFFICE}");
            dismiss.SetTextVariable("HERO", TitleNames.Hero(holder));
            dismiss.SetTextVariable("OFFICE", officeName);
            options.Add(new ClaimOption(dismiss.ToString(), string.Empty, string.Empty, () => Confirm(
                Text("{=realms_ui_dismiss_header}Dismiss"), dismiss + "?",
                () => { if (!b.PlayerDismiss(office.Id)) Message(Failed()); },
                afterYes, afterNo)));
        }
    }

    private static string GrantHint(GrantMode mode) => Text(mode switch
    {
        GrantMode.ByFief => "{=realms_ui_grant_fief}With its fief: the town or castle passes to their clan.",
        GrantMode.WithVassals => "{=realms_ui_grant_vassals}With the titles below it that you hold.",
        _ => "{=realms_ui_grant_title}The title alone: its lands stay with their lords.",
    });

    private static string GrantQuestion(string titleName, Hero recipient, GrantMode mode)
    {
        var text = new TextObject(mode == GrantMode.ByFief
            ? "{=realms_ui_grant_ask_fief}Give the {TITLE} and its fief to {HERO}?"
            : "{=realms_ui_grant_ask}Grant the {TITLE} to {HERO}?");
        text.SetTextVariable("TITLE", titleName);
        text.SetTextVariable("HERO", recipient.Name);
        return text.ToString();
    }

    /// <summary>What the coronation does, said before the player agrees to it; a player who rules the realm loses its crown.</summary>
    private static string CoronationWarning(Hero claimant, TitleDefinition title, global::TAOM.Features.Realms.Domain.Config.CoronationConfig coronation)
    {
        var kingdom = title.KingdomId != null
            ? TaleWorlds.CampaignSystem.Campaign.Current?.CampaignObjectManager.Find<Kingdom>(title.KingdomId)
            : null;
        var seat = coronation.FiefIds.Select(TaleWorlds.CampaignSystem.Settlements.Settlement.Find).FirstOrDefault(s => s != null);
        // Crowned as the player: your own clan becomes the house.
        var isPlayer = claimant == Hero.MainHero;
        var text = new TextObject(isPlayer
            ? seat != null
                ? "{=realms_ui_coronation_ask_player}Your clan will become the {CLAN} and rule {KINGDOM} from {SEAT}."
                : "{=realms_ui_coronation_ask_player_no_seat}Your clan will become the {CLAN} and rule {KINGDOM}."
            : seat != null
                ? "{=realms_ui_coronation_ask}{HERO} will leave your clan, found the {CLAN} and rule {KINGDOM} from {SEAT}."
                : "{=realms_ui_coronation_ask_no_seat}{HERO} will leave your clan, found the {CLAN} and rule {KINGDOM}.");
        text.SetTextVariable("HERO", claimant.Name);
        text.SetTextVariable("CLAN", new TextObject(coronation.ClanName));
        text.SetTextVariable("KINGDOM", kingdom?.Name ?? new TextObject(title.Name));
        if (seat != null)
            text.SetTextVariable("SEAT", seat.Name);
        var warning = text.ToString();
        if (!isPlayer && kingdom != null && kingdom.RulingClan == Clan.PlayerClan)
        {
            var crown = new TextObject("{=realms_ui_coronation_player}You will no longer rule {KINGDOM}.");
            crown.SetTextVariable("KINGDOM", kingdom.Name);
            warning += " " + crown;
        }

        return warning;
    }

    /// <summary>A list of heroes to choose one from; Back runs <paramref name="afterNo"/>.</summary>
    private static void Pick(string header, string description, IReadOnlyList<(Hero Hero, string Hint)> heroes, Action<Hero> chosen, Action? afterNo)
    {
        var items = heroes.Select(h => new InquiryElement(h.Hero, h.Hero.Name.ToString(), null, true, h.Hint)).ToList();
        var data = new MultiSelectionInquiryData(
            header, description, items, true,
            items.Count > 0 ? 1 : 0, 1,
            Text("{=realms_ui_choose}Choose"), Text("{=realms_ui_back}Back"),
            selected =>
            {
                if (selected.FirstOrDefault()?.Identifier is Hero hero)
                    UiQueue.Post(() => chosen(hero));
            },
            _ => UiQueue.Post(afterNo),
            string.Empty, items.Count > 12);
        MBInformationManager.ShowMultiSelectionInquiry(data, true);
    }
}
