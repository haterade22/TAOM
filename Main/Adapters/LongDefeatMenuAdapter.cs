using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>
/// Thin wrapper over the engine's initial-state options, multi-selection inquiry and <c>MBGameManager</c> calls. Resolves each raw
/// <c>{=KEY}English</c> text through <see cref="TextObject"/> when it is shown, so a language change between boot and click is honoured.
/// </summary>
internal sealed class LongDefeatMenuAdapter : ILongDefeatMenuAdapter
{
    // The engine's own "Ok" row (Native global_strings.xml str_ok), already translated, as SubModule's patch-failure notice uses.
    private const string OkText = "{=oHaWR73d}Ok";
    private const string ChooseText = "{=TAOM_LD_Prompt_Choose}Choose";
    private const string CancelText = "{=TAOM_LD_Prompt_Cancel}Cancel";
    private const string NoticeTitle = "{=TAOM_LD_MenuOption}The Long Defeat";

    public bool HasOption(string id) => Module.CurrentModule?.GetInitialStateOptionWithId(id) != null;

    public void AddOption(string id, string label, string hint, int orderIndex, Func<bool> isHidden, Action onChosen) =>
        Module.CurrentModule.AddInitialStateOption(new InitialStateOption(
            id, new TextObject(label), orderIndex, onChosen, () => (false, new TextObject("")), new TextObject(hint), isHidden));

    public void Choose(string title, string description, IReadOnlyList<LongDefeatChoice> choices, Action<string> onPicked)
    {
        var elements = choices.Select(c => new InquiryElement(c.Id, Resolve(c), null, true, c.Hint == null ? null : new TextObject(c.Hint).ToString())).ToList();
        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            titleText: new TextObject(title).ToString(),
            descriptionText: new TextObject(description).ToString(),
            inquiryElements: elements,
            isExitShown: true,
            minSelectableOptionCount: 1,
            maxSelectableOptionCount: 1,
            affirmativeText: new TextObject(ChooseText).ToString(),
            negativeText: new TextObject(CancelText).ToString(),
            affirmativeAction: chosen =>
            {
                if (chosen != null && chosen.Count > 0 && chosen[0].Identifier is string id)
                    onPicked(id);
            },
            negativeAction: _ => { }));
    }

    public void Notify(string message) =>
        InformationManager.ShowInquiry(new InquiryData(
            new TextObject(NoticeTitle).ToString(), new TextObject(message).ToString(), true, false,
            new TextObject(OkText).ToString(), string.Empty, null, null));

    public void EndGame() => MBGameManager.EndGame();

    private static string Resolve(LongDefeatChoice choice)
    {
        var text = new TextObject(choice.Label);
        if (choice.Variables != null)
        {
            foreach (var pair in choice.Variables)
                text.SetTextVariable(pair.Key, new TextObject(pair.Value));
        }

        return text.ToString();
    }
}
