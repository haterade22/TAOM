using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Services;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Features.Realms;

/// <summary>
/// Shows title changes in the message log: realm titles always, lesser titles only when the
/// player is involved. Every change also goes to the game log for testing.
/// </summary>
internal static class TitleEventReporter
{
    public static void Report(IEnumerable<TitleEvent> events, TitleCatalog catalog, RealmsConfig config)
    {
        var player = Hero.MainHero?.StringId;
        foreach (var e in events)
        {
            Debug.Print($"[TAOM.Realms] {e.Kind}/{e.Reason} {e.TitleId}: {e.PreviousHolderId ?? "-"} -> {e.HeroId ?? "-"}");
            var involvesPlayer = player != null && (e.HeroId == player || e.PreviousHolderId == player);

            if (e.Kind == TitleEventKind.OfficeChanged)
            {
                var office = config.Offices.Find(o => o.Id == e.TitleId);
                var playerRealm = Hero.MainHero?.Clan?.Kingdom?.StringId;
                if (office != null && (office.Rank >= TitleRank.Kingdom || involvesPlayer || office.KingdomId == playerRealm))
                    InformationManager.DisplayMessage(new InformationMessage(DescribeOffice(e, new TextObject(office.Name).ToString()), Colors.Yellow));
                continue;
            }

            var title = catalog.Get(e.TitleId);
            if (title == null)
                continue;
            if (title.Rank >= TitleRank.Kingdom || involvesPlayer)
                InformationManager.DisplayMessage(new InformationMessage(Describe(e, TitleNames.Of(title, config, catalog)), Colors.Yellow));
        }
    }

    private static string DescribeOffice(TitleEvent e, string officeName)
    {
        TextObject text;
        if (e.HeroId == null)
            text = new TextObject("{=realms_msg_office_vacant}{OLD} is no longer {OFFICE}.");
        else if (e.Reason == TitleEventReason.RealmSuccession)
            text = new TextObject("{=realms_msg_office_follows}{HERO} takes up the office of {OFFICE}.");
        else
            text = new TextObject("{=realms_msg_office_appointed}{HERO} is appointed {OFFICE}.");
        text.SetTextVariable("HERO", TitleNames.Hero(e.HeroId));
        text.SetTextVariable("OLD", TitleNames.Hero(e.PreviousHolderId));
        text.SetTextVariable("OFFICE", officeName);
        return text.ToString();
    }

    private static string Describe(TitleEvent e, string titleName)
    {
        TextObject text;
        if (e.Kind == TitleEventKind.ClaimGained && e.Reason == TitleEventReason.Purchase)
            text = new TextObject("{=realms_msg_claim_bought}{HERO} has bought a claim on the {TITLE}.");
        else if (e.Kind == TitleEventKind.ClaimGained && e.Reason == TitleEventReason.Ancestry)
            text = new TextObject("{=realms_msg_claim_blood}{HERO} inherits a claim on the {TITLE}.");
        else if (e.Kind == TitleEventKind.ClaimGained)
            text = new TextObject("{=realms_msg_claim}{HERO} now has a claim on the {TITLE}.");
        else if (e.Reason == TitleEventReason.Usurpation)
            text = new TextObject("{=realms_msg_usurp}{HERO} has usurped the {TITLE}.");
        else if (e.Reason == TitleEventReason.Revocation && e.HeroId != null)
            text = new TextObject("{=realms_msg_revoke}{HERO} has taken the {TITLE} back from {OLD}.");
        else if (e.Reason == TitleEventReason.Formation && e.HeroId != null)
            text = new TextObject("{=realms_msg_form}{HERO} has formed the {TITLE}.");
        else if (e.Reason == TitleEventReason.Restoration && e.HeroId != null)
            text = new TextObject("{=realms_msg_restore}{HERO} has restored the {TITLE} by right of blood.");
        else if (e.Reason == TitleEventReason.Claim && e.HeroId != null)
            text = new TextObject("{=realms_msg_by_claim}{HERO} takes up the {TITLE} by claim.");
        else if (e.HeroId == null)
            text = new TextObject("{=realms_msg_vacant}The {TITLE} has fallen vacant.");
        else if (e.Reason == TitleEventReason.Inheritance)
            text = new TextObject("{=realms_msg_inherit}{HERO} inherits the {TITLE}.");
        else if (e.Reason == TitleEventReason.Grant)
            text = new TextObject("{=realms_msg_grant}{HERO} is granted the {TITLE}.");
        else if (e.Reason == TitleEventReason.Transfer)
            text = new TextObject("{=realms_msg_transfer}The {TITLE} passes to {HERO}.");
        else
            text = new TextObject("{=realms_msg_holder}{HERO} now holds the {TITLE}.");

        text.SetTextVariable("HERO", TitleNames.Hero(e.HeroId));
        text.SetTextVariable("OLD", TitleNames.Hero(e.PreviousHolderId));
        text.SetTextVariable("TITLE", titleName);
        return text.ToString();
    }
}
