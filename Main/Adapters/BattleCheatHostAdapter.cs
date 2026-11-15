using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Features.CheatMenu;
using TAOM.Features.CheatMenu.Battle;

namespace TAOM.Adapters;

public sealed class BattleCheatHostAdapter : IBattleCheatHostAdapter
{
    public void ShowList(IReadOnlyList<BattleCheatAction> actions, Action<BattleCheatAction> onPicked)
    {
        var elements = actions.Select(a => new InquiryElement(a, BattleCheatTexts.Label(a).ToString(), null)).ToList();
        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            titleText: new TextObject("{=taom_cheat_battle_title}Battle cheats").ToString(),
            descriptionText: new TextObject("{=taom_cheat_battle_text}Cheats change this battle. New troops are copies of soldiers already on that side.").ToString(),
            inquiryElements: elements,
            isExitShown: true,
            minSelectableOptionCount: 1,
            maxSelectableOptionCount: 1,
            affirmativeText: new TextObject("{=taom_cheat_pick_ok}Choose").ToString(),
            negativeText: new TextObject("{=taom_cheat_pick_cancel}Cancel").ToString(),
            affirmativeAction: chosen =>
            {
                if (chosen != null && chosen.Count > 0 && chosen[0].Identifier is BattleCheatAction action)
                    onPicked(action);
            },
            negativeAction: _ => { }));
    }

    public void ShowOutcome(BattleCheatAction action, CheatOutcome outcome) => BattleCheatTexts.Show(action, outcome);
}
