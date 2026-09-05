using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Adapters;

public class InquiryPresenterAdapter : IInquiryPresenterAdapter
{
    public void ShowPromotionOffer(string troopName, Action onAccept, Action onDecline)
    {
        var title = new TextObject("{=taom_fc_offer_title}Battlefield Promotion");
        var body = new TextObject("{=taom_fc_offer_body}{TROOP_NAME} has distinguished themselves in battle. Promote them to a companion?");
        body.SetTextVariable("TROOP_NAME", troopName ?? string.Empty);

        InformationManager.ShowInquiry(new InquiryData(
            title.ToString(),
            body.ToString(),
            true,
            true,
            new TextObject("{=taom_fc_accept}Promote").ToString(),
            new TextObject("{=taom_fc_decline}Not Yet").ToString(),
            onAccept,
            onDecline),
            true,
            false);
    }

    public void ShowNoCompanionRoom(string troopName, Action onAcknowledge)
    {
        var title = new TextObject("{=taom_fc_offer_title}Battlefield Promotion");
        var body = new TextObject("{=taom_fc_no_room_body}There is no room for another companion right now. {TROOP_NAME}'s promotion will be offered again later.");
        body.SetTextVariable("TROOP_NAME", troopName ?? string.Empty);

        InformationManager.ShowInquiry(new InquiryData(
            title.ToString(),
            body.ToString(),
            true,
            false,
            new TextObject("{=taom_fc_acknowledge}Understood").ToString(),
            null,
            onAcknowledge,
            null),
            true,
            false);
    }

    public void ShowRenamePrompt(string troopName, Action<string> onConfirm, Action onSkip)
    {
        var title = new TextObject("{=taom_fc_rename_title}Name the New Companion");
        var body = new TextObject("{=taom_fc_rename_body}Give {TROOP_NAME} a name, or keep the one they carried into battle.");
        body.SetTextVariable("TROOP_NAME", troopName ?? string.Empty);

        // Verified 1.4.7 ctor order — see IInquiryPresenterAdapter's doc comment (bug fix (b)).
        InformationManager.ShowTextInquiry(new TextInquiryData(
            title.ToString(),
            body.ToString(),
            true,
            true,
            new TextObject("{=taom_fc_confirm_name}Confirm").ToString(),
            new TextObject("{=taom_fc_keep_name}Keep Default Name").ToString(),
            onConfirm,
            onSkip,
            shouldInputBeObfuscated: false,
            textCondition: null,
            soundEventPath: string.Empty,
            defaultInputText: troopName ?? string.Empty),
            false,
            false);
    }

    public void ShowDismissPicker(IReadOnlyList<DismissCandidate> candidates, Action<string> onChosen)
    {
        var elements = new List<InquiryElement>(candidates?.Count ?? 0);
        if (candidates != null)
        {
            foreach (var candidate in candidates)
            {
                var entry = new TextObject("{=taom_fc_dismiss_entry}{HERO_NAME} (was {TROOP_NAME})");
                entry.SetTextVariable("HERO_NAME", candidate.HeroName ?? string.Empty);
                entry.SetTextVariable("TROOP_NAME", candidate.TroopName ?? string.Empty);
                elements.Add(new InquiryElement(candidate.HeroId, entry.ToString(), null));
            }
        }

        if (elements.Count == 0)
            return;

        // Named arguments against the verified 1.4.8 ctor, the same shape RefugeMenuController uses.
        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            titleText: new TextObject("{=taom_fc_dismiss_picker_title}Discharge a Promoted Companion").ToString(),
            descriptionText: new TextObject("{=taom_fc_dismiss_picker_desc}Choose who returns to the ranks. They rejoin your party as the soldier they were promoted from; the gear they carry as a companion is lost.").ToString(),
            inquiryElements: elements,
            isExitShown: true,
            minSelectableOptionCount: 1,
            maxSelectableOptionCount: 1,
            affirmativeText: new TextObject("{=taom_fc_dismiss_pick}Choose").ToString(),
            negativeText: new TextObject("{=taom_fc_cancel}Cancel").ToString(),
            affirmativeAction: chosen =>
            {
                if (chosen == null || chosen.Count == 0 || !(chosen[0].Identifier is string heroId))
                    return;
                onChosen?.Invoke(heroId);
            },
            negativeAction: _ => { }));
    }

    public void ShowDismissConfirm(string heroName, string troopName, Action onConfirm, Action onCancel)
    {
        var title = new TextObject("{=taom_fc_dismiss_title}Return to the Ranks");
        var body = new TextObject("{=taom_fc_dismiss_confirm_body}{HERO_NAME} will be discharged and one {TROOP_NAME} added to your party in their place. Everything they carry as a companion is lost with the commission. This cannot be undone.");
        body.SetTextVariable("HERO_NAME", heroName ?? string.Empty);
        body.SetTextVariable("TROOP_NAME", troopName ?? string.Empty);

        InformationManager.ShowInquiry(new InquiryData(
            title.ToString(),
            body.ToString(),
            true,
            true,
            new TextObject("{=taom_fc_dismiss_yes}Discharge").ToString(),
            new TextObject("{=taom_fc_cancel}Cancel").ToString(),
            onConfirm,
            onCancel),
            true,
            false);
    }

    public void ShowDismissed(string heroName, string troopName)
    {
        var text = new TextObject("{=taom_fc_dismissed}{HERO_NAME} has returned to the ranks as a {TROOP_NAME}.");
        text.SetTextVariable("HERO_NAME", heroName ?? string.Empty);
        text.SetTextVariable("TROOP_NAME", troopName ?? string.Empty);
        InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
    }

    public void ShowDismissFailed(string heroName)
    {
        var text = string.IsNullOrWhiteSpace(heroName)
            ? new TextObject("{=taom_fc_dismiss_failed_unnamed}That companion could not be discharged right now.")
            : new TextObject("{=taom_fc_dismiss_failed}{HERO_NAME} could not be discharged right now.");
        text.SetTextVariable("HERO_NAME", heroName ?? string.Empty);
        InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
    }
}
