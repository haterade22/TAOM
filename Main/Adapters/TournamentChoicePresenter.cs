using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Adapters;

public sealed class TournamentChoicePresenter : ITournamentChoicePresenter
{
    public void ShowPrizeChoice(IReadOnlyList<string> itemIds, Action<string> onPicked, Action onCancel)
    {
        var elements = new List<InquiryElement>();
        for (var i = 0; i < itemIds.Count; i++)
        {
            var item = Game.Current?.ObjectManager?.GetObject<ItemObject>(itemIds[i]);
            if (item == null)
                continue;
            var name = item.Name?.ToString() ?? itemIds[i];
            var title = i == 0
                ? new TextObject("{=taom_tr_prize_advertised}{ITEM_NAME} (the advertised prize)").SetTextVariable("ITEM_NAME", name).ToString()
                : name;
            var hint = new TextObject("{=taom_tr_prize_hint}Worth about {VALUE} denars.").SetTextVariable("VALUE", item.Value).ToString();
            elements.Add(new InquiryElement(itemIds[i], title, new ItemImageIdentifier(item), true, hint));
        }
        if (elements.Count == 0)
        {
            onCancel();
            return;
        }

        Show(new TextObject("{=taom_tr_prize_title}Choose Your Prize").ToString(),
            new TextObject("{=taom_tr_prize_desc}The tournament master offers a choice of prizes. Choose the one you will fight for; it is yours if you win.").ToString(),
            elements, onPicked, onCancel);
    }

    public void ShowSkillChoice(IReadOnlyList<string> skillIds, Action<string> onPicked, Action onCancel)
    {
        var elements = new List<InquiryElement>();
        foreach (var id in skillIds)
            elements.Add(new InquiryElement(id, SkillName(id), null));

        Show(new TextObject("{=taom_tr_skill_title}Choose Your Training").ToString(),
            new TextObject("{=taom_tr_skill_desc}Choose the skill this tournament trains. You gain experience in it for every round you win, and more for winning the tournament.").ToString(),
            elements, onPicked, onCancel);
    }

    public void ShowSkillTrained(string skillId)
    {
        var text = new TextObject("{=taom_tr_skill_gained}The tournament trained your {SKILL_NAME}.")
            .SetTextVariable("SKILL_NAME", SkillName(skillId));
        InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
    }

    private static string SkillName(string skillId) =>
        Game.Current?.ObjectManager?.GetObject<SkillObject>(skillId)?.Name?.ToString() ?? skillId;

    private static void Show(string title, string description, List<InquiryElement> elements, Action<string> onPicked, Action onCancel) =>
        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            titleText: title,
            descriptionText: description,
            inquiryElements: elements,
            isExitShown: true,
            minSelectableOptionCount: 1,
            maxSelectableOptionCount: 1,
            affirmativeText: new TextObject("{=taom_tr_choose}Choose").ToString(),
            negativeText: new TextObject("{=taom_tr_cancel}Cancel").ToString(),
            affirmativeAction: chosen =>
            {
                if (chosen == null || chosen.Count == 0 || !(chosen[0].Identifier is string id))
                {
                    onCancel();
                    return;
                }
                onPicked(id);
            },
            negativeAction: _ => onCancel()));
}
