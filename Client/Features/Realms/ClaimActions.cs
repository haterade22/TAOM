using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.Realms;
using TAOM.Features.Realms.Domain.Services;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Client.Features.Realms;

/// <summary>One claim action on a title for the player: buy a claim or usurp, its price and why it cannot be done now.</summary>
internal sealed class ClaimOption
{
    public ClaimOption(string label, string cost, string reason, Action run)
    {
        Label = label;
        Cost = cost;
        Reason = reason;
        Run = run;
    }

    /// <summary>"Buy a claim" or "Usurp".</summary>
    public string Label { get; }

    /// <summary>"80 Castar" or "100 influence"; empty when the action is refused before it has a price.</summary>
    public string Cost { get; }

    /// <summary>Why the player cannot do it now; empty when they can.</summary>
    public string Reason { get; }

    public bool Enabled => Reason.Length == 0;

    /// <summary>Asks for confirmation, then acts.</summary>
    public Action Run { get; }
}

/// <summary>
/// The player's claim actions on a title, shared by the encyclopedia and the titles window. "Buy a claim" is paid in the
/// player's special resource and "Usurp", once the player claims the title, in influence. An action that does not apply
/// to the title (not for sale, held or claimed already, no claim to press) is left out; a refused one carries its reason.
/// </summary>
internal static class ClaimActions
{
    public static List<ClaimOption> For(RealmsCampaign b, TitleDefinition title, Action? afterYes, Action? afterNo)
    {
        var options = new List<ClaimOption>();
        var hero = Hero.MainHero;
        if (hero?.Clan == null || !hero.IsAlive)
            return options;
        var name = TitleNames.Of(title, b);

        var buy = b.QuoteBuyClaim(hero, title.Id);
        if (buy.Refusal is not (ClaimRefusal.NotForSale or ClaimRefusal.Held or ClaimRefusal.Claimed))
        {
            var balance = SpecialResources.Balance(hero);
            var reason = Refusal(buy.Refusal);
            var cost = string.Empty;
            if (buy.Allowed)
            {
                var text = new TextObject("{=realms_ui_cost}{AMOUNT} {RESOURCE}");
                text.SetTextVariable("AMOUNT", buy.Cost);
                text.SetTextVariable("RESOURCE", balance?.Name ?? string.Empty);
                cost = text.ToString();
                if (balance == null)
                    reason = Text("{=realms_ui_no_resource}Your people have no resource to buy claims with.");
                else if (balance.Value.Amount < buy.Cost)
                {
                    var low = new TextObject("{=realms_ui_low_resource}You have {AMOUNT} {RESOURCE}.");
                    low.SetTextVariable("AMOUNT", (int)balance.Value.Amount);
                    low.SetTextVariable("RESOURCE", balance.Value.Name);
                    reason = low.ToString();
                }
            }

            options.Add(new ClaimOption(Text("{=realms_ui_buy_claim}Buy a claim"), cost, reason, () => Confirm(
                Text("{=realms_ui_buy_claim}Buy a claim"),
                Ask("{=realms_ui_buy_claim_ask}Buy a claim on the {TITLE} for {COST}?", name, cost),
                () => { if (!b.PlayerBuyClaim(title.Id)) Message(Failed()); },
                afterYes, afterNo)));
        }

        var usurp = b.QuoteUsurp(hero, title.Id);
        if (usurp.Refusal is ClaimRefusal.NotForSale or ClaimRefusal.Held or ClaimRefusal.NoClaim)
        {
            options.AddRange(FeudalActions.For(b, title, afterYes, afterNo));
            return options;
        }

        var usurpReason = Refusal(usurp.Refusal);
        var usurpCost = string.Empty;
        if (usurp.Allowed)
        {
            var text = new TextObject("{=realms_ui_influence_cost}{AMOUNT} influence");
            text.SetTextVariable("AMOUNT", usurp.Cost);
            usurpCost = text.ToString();
            if (hero.Clan.Influence < usurp.Cost)
            {
                var low = new TextObject("{=realms_ui_low_influence}Your clan has {AMOUNT} influence.");
                low.SetTextVariable("AMOUNT", (int)hero.Clan.Influence);
                usurpReason = low.ToString();
            }
        }

        options.Add(new ClaimOption(Text("{=realms_ui_usurp}Usurp"), usurpCost, usurpReason, () => Confirm(
            Text("{=realms_ui_usurp}Usurp"),
            Ask("{=realms_ui_usurp_ask}Press your claim and take the {TITLE} for {COST}?", name, usurpCost),
            () => { if (!b.PlayerUsurp(title.Id)) Message(Failed()); },
            afterYes, afterNo)));
        options.AddRange(FeudalActions.For(b, title, afterYes, afterNo));
        return options;
    }

    internal static void Confirm(string header, string text, Action yes, Action? afterYes, Action? afterNo) =>
        InformationManager.ShowInquiry(new InquiryData(header, text, true, true,
            Text("{=realms_ui_yes}Yes"), Text("{=realms_ui_no}No"),
            () => { yes(); UiQueue.Post(afterYes); }, () => UiQueue.Post(afterNo)), true);

    internal static string Ask(string template, string title, string cost)
    {
        var text = new TextObject(template);
        text.SetTextVariable("TITLE", title);
        text.SetTextVariable("COST", cost);
        return text.ToString();
    }

    internal static string Failed() => Text("{=realms_ui_claim_failed}That is no longer possible.");

    internal static string Refusal(ClaimRefusal refusal) => refusal switch
    {
        ClaimRefusal.None => string.Empty,
        ClaimRefusal.OwnClan => Text("{=realms_ui_refuse_clan}Your own clan holds it."),
        ClaimRefusal.OwnRealm => Text("{=realms_ui_refuse_realm}It lies in your own realm."),
        ClaimRefusal.TooFar => Text("{=realms_ui_refuse_far}It does not border your fiefs."),
        ClaimRefusal.NotOwned => Text("{=realms_ui_refuse_owned}Your clan must hold its fief first."),
        ClaimRefusal.TooFewVassals => Text("{=realms_ui_refuse_vassals}You, your clan and your vassals must hold more than half of its lands."),
        ClaimRefusal.NoFoothold => Text("{=realms_ui_refuse_foothold}You, your clan or your vassals must first hold one of its lands."),
        ClaimRefusal.OutsideRealm => Text("{=realms_ui_refuse_outside}Its holder does not serve your realm."),
        ClaimRefusal.NotServing => Text("{=realms_ui_refuse_serving}The claimant must serve its realm."),
        ClaimRefusal.RestoreOnly => Text("{=realms_ui_refuse_restore_only}Only the heir of its old kings can take up this throne."),
        ClaimRefusal.NotVacant => Text("{=realms_ui_refuse_not_vacant}It is not vacant."),
        ClaimRefusal.WrongCulture => Text("{=realms_ui_refuse_culture}Only its own people can take up this title."),
        _ => Text("{=realms_ui_refuse}Not possible."),
    };

    internal static string Influence(int amount)
    {
        var text = new TextObject("{=realms_ui_influence_cost}{AMOUNT} influence");
        text.SetTextVariable("AMOUNT", amount);
        return text.ToString();
    }

    /// <summary>"Your clan has 40 influence." when the clan cannot pay <paramref name="cost"/>, else the refusal's reason.</summary>
    internal static string InfluenceReason(Clan clan, ClaimQuote quote)
    {
        if (!quote.Allowed || clan.Influence >= quote.Cost)
            return Refusal(quote.Refusal);
        var low = new TextObject("{=realms_ui_low_influence}Your clan has {AMOUNT} influence.");
        low.SetTextVariable("AMOUNT", (int)clan.Influence);
        return low.ToString();
    }

    internal static string Text(string id) => new TextObject(id).ToString();

    internal static void Message(string text) => InformationManager.DisplayMessage(new InformationMessage(text, Colors.Yellow));
}
